using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.UI;
using CatSanguo.Generals;
using CatSanguo.WorldMap;

namespace CatSanguo.Scenes;

public class WorldMapScene : Scene
{
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private SpriteFontBase _smallFont;

    private List<CityNode> _cities = new();
    private List<CityData> _allCityData = new();
    private List<GeneralData> _allGenerals = new();
    private List<StageData> _allStages = new();
    private List<TerrainFeatureData> _terrainFeatures = new();

    // Camera
    private Camera2D _camera;

    // Renderers
    private MapBackgroundRenderer _bgRenderer = new();
    private ProvinceRenderer _provinceRenderer = new();
    private CityRenderer _cityRenderer = new();
    private RoadRenderer _roadRenderer = new();
    private TerrainRenderer _terrainRenderer = new();
    private FogOfWarManager _fogOfWar;
    private ArmyManager _armyManager = new();

    // Virtual world map bounds (16x10 grid → 2000x1400 world pixels)
    private const float MapLeft = 140;
    private const float MapTop = 100;
    private const float MapRight = 1860;
    private const float MapBottom = 1300;
    private const float GridMaxX = 15f;
    private const float GridMaxY = 9f;

    // 城池操作对话框
    private CityActionDialog _cityDialog = null!;

    // 回合制管理器
    private TurnManager _turnManager = null!;

    private string _statusText = "点击城池选择军事/内政操作";
    private float _notifyTimer = 0f;
    private string _notifyText = "";
    private float _sceneTime = 0f;

    // 左键拖拽平移
    private bool _isDragging = false;
    private Vector2 _dragStartScreen;
    private const float DragThreshold = 5f; // 超过5像素判定为拖拽

    // 势力图例面板
    private FactionLegendPanel _factionPanel = new();
    private Vector2 _cameraTarget;
    private bool _cameraAnimating = false;

    // 敌方城池信息面板
    private EnemyCityInfoPanel _enemyCityPanel = new();
    private List<ScenarioFaction> _scenarioFactions = new();

    // 存档面板
    private SaveLoadPanel _saveLoadPanel = new();

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.NotifyFont;
        _smallFont = Game.SmallFont;

        // Load data — 使用 DataManager 中已被 ScenarioManager 修改过的数据
        // 不从文件重新加载，否则 ScenarioManager.StartGame() 设置的城池归属会丢失
        _allCityData = DataManager.Instance.AllCities;
        _allGenerals = DataManager.Instance.AllGenerals;
        _allStages = DataManager.Instance.AllStages;

        // Load terrain features (非核心数据，可从文件加载)
        string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        string terrainPath = Path.Combine(dataPath, "terrain_features.json");
        if (File.Exists(terrainPath))
            _terrainFeatures = DataLoader.LoadList<TerrainFeatureData>(terrainPath);

        // Initialize GameState
        GameState.Instance.Initialize(_allGenerals);

        // Sync city ownership:
        // ScenarioManager.StartGame() 已正确设置了 CityData.Owner 和 GameState.OwnedCityIds
        // 这里只需要处理战斗后保存的归属变化（从存档恢复时）
        foreach (var city in _allCityData)
        {
            if (GameState.Instance.OwnsCity(city.Id))
            {
                city.Owner = "player";
                city.Garrison.Clear();
            }
        }

        CreateCityNodes();
        CreateButtons();

        // 强制刷新领地渲染缓存（ScenarioManager.StartGame 修改了城市归属）
        _provinceRenderer.Invalidate();

        // Initialize camera
        _camera = new Camera2D(GraphicsDevice);
        // 向左扩展世界边界，使得在 fitZoom 下地图自动右移，为左侧图例面板留出空间
        _camera.WorldBounds = new Rectangle(-480, 0, 2480, 1400);

        // 计算让整个地图恰好铺满屏幕的缩放值（基于实际地图宽度 2000）
        float fitZoomX = (float)GameSettings.ScreenWidth / 2000f;   // 1280/2000 = 0.64
        float fitZoomY = (float)GameSettings.ScreenHeight / 1400f;  // 720/1400  = 0.514
        float fitZoom = MathF.Min(fitZoomX, fitZoomY);

        _camera.MinZoom = fitZoom;
        _camera.MaxZoom = 1.5f;

        // 初始显示完整地图，auto-center 会偏右以避开图例面板
        _camera.Position = new Vector2(760, 700);
        _camera.SetZoom(fitZoom);
        _camera.ClampPosition();

        // Initialize fog of war (16x10 grid matching new city grid)
        _fogOfWar = new FogOfWarManager(16, 10);

        // Initialize army manager
        _armyManager = new ArmyManager();
        _armyManager.Initialize(_cities, _allGenerals);
        _armyManager.OnArmyArrived += HandleArmyArrived;

        // 恢复行军状态（从存档加载后）
        RestoreArmyMarchState();

        // Initialize faction legend panel
        var scenario = GameRoot.Instance.ScenarioManager.SelectedScenario;
        _scenarioFactions = scenario?.Factions ?? new List<ScenarioFaction>();
        _factionPanel.Build(_cities, _scenarioFactions, _allGenerals);
        _factionPanel.OnCityClicked = (worldPos) =>
        {
            _cameraTarget = worldPos;
            _cameraAnimating = true;
        };

        // Pre-render background
        _bgRenderer.Invalidate();
        _bgRenderer.EnsureCache(GraphicsDevice, SpriteBatch, _pixel, _cities);

        // 初始化城池操作对话框
        _cityDialog = new CityActionDialog();
        _cityDialog.Initialize(GetGeneralName, _font, _titleFont,
            () => Game.SceneManager.ChangeScene(new GeneralRosterScene()),
            () => {
                _armyManager.AdvanceAllArmies(10);

                // 行军粮草消耗：每回合对行军中的玩家军队扣粮
                foreach (var army in _armyManager.ArmiesList)
                {
                    if (army.Team != "player" || !army.IsMoving) continue;
                    int totalSoldiers = army.GeneralEntries.Sum(e => e.SoldierCount);
                    int grainCost = Math.Max(1, totalSoldiers / 2);
                    if (!string.IsNullOrEmpty(army.OriginCityId))
                    {
                        var originCity = GameState.Instance.GetCityProgress(army.OriginCityId);
                        if (originCity != null)
                            originCity.Grain = Math.Max(0, originCity.Grain - grainCost);
                    }
                }

                _turnManager?.EndTurn();
                _notifyText = $"第{GameState.Instance.TurnNumber}回合开始 - {GameState.Instance.CurrentDate.ToDisplayString()}";
                _notifyTimer = 2.5f;
            },
            () => {
                _notifyText = "游戏已保存";
                _notifyTimer = 1.5f;
            });

        // 初始化回合制管理器
        var eventBus = new EventBus();
        _turnManager = new TurnManager(eventBus);

        // 初始化存档面板
        _saveLoadPanel.OnOperationComplete = (slot, isLoad) =>
        {
            if (isLoad)
            {
                // 加载存档后重建场景
                Game.SceneManager.ChangeScene(new WorldMapScene());
            }
            else
            {
                ShowNotify($"已保存到档位 #{slot}");
            }
        };

        // Initial fog update
        UpdateFog();
    }

    private string GetGeneralName(string genId)
    {
        var gen = _allGenerals.FirstOrDefault(g => g.Id == genId);
        return gen?.Name ?? genId;
    }

    private void CreateCityNodes()
    {
        _cities.Clear();
        float mapW = MapRight - MapLeft;
        float mapH = MapBottom - MapTop;

        foreach (var cityData in _allCityData)
        {
            float x = MapLeft + (cityData.GridX / GridMaxX) * mapW;
            float y = MapTop + (cityData.GridY / GridMaxY) * mapH;
            var node = new CityNode(cityData, new Vector2(x, y));
            _cities.Add(node);
        }
    }

    private void CreateButtons()
    {
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _sceneTime += dt;

        // 存档面板激活时，优先处理存档面板，阻止其他交互
        if (_saveLoadPanel.IsActive)
        {
            _saveLoadPanel.Update(Input, dt);
            return;
        }

        // 返回主菜单按钮点击检测（同时存档）
        var returnRect = new Rectangle(GameSettings.ScreenWidth - 140, 10, 130, 35);
        // 存档按钮（在返回按钮左边）
        var saveRect = new Rectangle(GameSettings.ScreenWidth - 280, 10, 130, 35);
        if (Input.IsMouseClicked() && !_armyManager.IsAnimPhase)
        {
            if (returnRect.Contains(Input.MousePosition.ToPoint()))
            {
                GameState.Instance.Save();
                _notifyText = "游戏已保存";
                _notifyTimer = 1.5f;
                Game.SceneManager.ChangeScene(new MainMenuScene());
                return;
            }
            if (saveRect.Contains(Input.MousePosition.ToPoint()))
            {
                _saveLoadPanel.Open(SaveLoadMode.Save);
                return;
            }
        }

        // 势力图例面板更新
        _factionPanel.Update(Input);

        // 敌方城池信息面板更新（保存帧开始时的状态，防止关闭同帧点击穿透）
        bool wasEnemyPanelActive = _enemyCityPanel.IsActive;
        if (_enemyCityPanel.IsActive)
        {
            _enemyCityPanel.CityScreenPos = _camera.WorldToScreen(_enemyCityPanel.CityWorldPos);
            _enemyCityPanel.Update(Input);
        }

        // 镜头平滑跳转动画
        if (_cameraAnimating)
        {
            _camera.Position = Vector2.Lerp(_camera.Position, _cameraTarget, 0.15f);
            if (Vector2.Distance(_camera.Position, _cameraTarget) < 1f)
            {
                _camera.Position = _cameraTarget;
                _cameraAnimating = false;
            }
            _camera.ClampPosition();
        }

        // Camera zoom (mouse wheel)
        int scroll = Input.ScrollWheelDelta;
        if (scroll != 0)
        {
            _camera.SetZoom(_camera.Zoom + scroll * 0.001f);
            _camera.ClampPosition();
        }

        // Camera pan (right mouse drag)
        if (Input.IsRightMouseHeld())
        {
            Vector2 delta = Input.MouseDelta;
            if (delta.LengthSquared() > 0.1f)
            {
                _camera.Position -= delta / _camera.Zoom;
                _camera.ClampPosition();
            }
        }

        // 左键拖拽平移 + 点击区分（面板区域内不触发）
        if (!_factionPanel.IsMouseInPanel && Input.IsMouseClicked())
        {
            // 左键刚按下，记录起始位置
            _isDragging = false;
            _dragStartScreen = Input.MousePosition;
        }
        else if (Input.IsLeftMouseHeld())
        {
            // 左键持续按住
            Vector2 delta = Input.MouseDelta;
            float distFromStart = (Input.MousePosition - _dragStartScreen).Length();

            if (!_isDragging && distFromStart > DragThreshold)
                _isDragging = true;

            if (_isDragging && delta.LengthSquared() > 0.1f)
            {
                _camera.Position -= delta / _camera.Zoom;
                _camera.ClampPosition();
            }
        }

        // Convert mouse to world space
        Vector2 worldMouse = _camera.ScreenToWorld(Input.MousePosition);

        // 如果对话框激活，处理对话框输入
        if (_cityDialog.IsActive)
        {
            // 每帧更新城池屏幕坐标（因相机可能移动/缩放）
            _cityDialog.CityScreenPos = _camera.WorldToScreen(_cityDialog.CityWorldPos);
            _cityDialog.WorldMousePos = worldMouse;
            _cityDialog.Update(Input, _cities);
            UpdateStatusText();
            return;
        }

        // Update army manager (handles movement + click input)
        _armyManager.Update(dt, Input, worldMouse);

        // Update fog of war
        UpdateFog();

        // 左键释放时，如果不是拖拽且不在面板内且不在动画阶段，才当作点击处理
        if (Input.IsLeftMouseReleased() && !_isDragging && !_factionPanel.IsMouseInPanel && !wasEnemyPanelActive && !_armyManager.IsAnimPhase)
        {
            HandleCityLeftClick(worldMouse);
        }

        // Update status text
        UpdateStatusText();

        // Update notification timer
        if (_notifyTimer > 0)
            _notifyTimer -= dt;
    }

    private void HandleCityLeftClick(Vector2 worldMouse)
    {
        foreach (var city in _cities)
        {
            if (city.Bounds.Contains(worldMouse.ToPoint()))
            {
                string owner = city.Data.Owner.ToLower();
                if (owner == "player")
                {
                    // 玩家城池：打开完整操作对话框（内政、经济、编队等）
                    _enemyCityPanel.Close();
                    _cityDialog.CityWorldPos = city.Center;
                    _cityDialog.CityScreenPos = _camera.WorldToScreen(city.Center);
                    _cityDialog.Open(city.Data, _allGenerals,
                        onClose: () => { },
                        onLaunchArmy: OnLaunchArmyFromDialog
                    );
                }
                else
                {
                    // 敌方/中立城池：只读信息面板
                    _cityDialog.Close();
                    _enemyCityPanel.Open(city.Data,
                        _camera.WorldToScreen(city.Center),
                        city.Center,
                        _scenarioFactions, _allGenerals);
                }
                return;
            }
        }
    }

    private void UpdateFog()
    {
        var playerCityIds = GameState.Instance.OwnedCityIds.ToList();
        var playerArmies = _armyManager.GetPlayerArmies();
        _fogOfWar.Update(_cities, playerCityIds, playerArmies);
    }

    private void UpdateStatusText()
    {
        if (_cityDialog.IsActive && _cityDialog.Phase != CityActionPhase.CategorySelect)
        {
            _statusText = _cityDialog.Phase switch
            {
                CityActionPhase.MilitaryDeploy => "编队出征",
                CityActionPhase.SelectGeneral => "选择武将",
                CityActionPhase.MilitarySelectTarget => "点击选择目标城池",
                CityActionPhase.MilitaryConfirm => "确认行军路线",
                CityActionPhase.TalentManage => "人才管理",
                _ => "城池操作中"
            };
            return;
        }

        var selected = _armyManager.SelectedArmy;
        if (selected != null)
        {
            if (selected.IsMoving)
            {
                _statusText = $"{selected.LeadGeneralName} 行军中...";
            }
            else
            {
                _statusText = $"已选择 {selected.LeadGeneralName} 的部队";
            }
        }
        else
        {
            _statusText = "点击城池选择军事/内政操作 | 滚轮缩放 右键拖拽";
        }
    }

    private void OnLaunchArmyFromDialog(List<string> generalIds, List<GeneralDeployEntry> deployConfigs, CityData targetCity)
    {
        // 从对话框获取源城池
        var sourceCity = _cityDialog.SourceCity;
        if (sourceCity == null || generalIds.Count == 0) return;

        // 获取主武将名称
        var leadGen = _allGenerals.FirstOrDefault(g => g.Id == generalIds[0]);
        string leadName = leadGen?.Name ?? "部队";

        // 计算出征兵力和粮草消耗
        int totalSoldiers = deployConfigs.Sum(c => c.SoldierCount);
        int grainCost = totalSoldiers * 2;
        var cityProgress = GameState.Instance.GetOrCreateCityProgress(sourceCity);

        if (cityProgress.CurrentTroops < totalSoldiers)
        {
            ShowNotify($"兵力不足！需要{totalSoldiers}，当前{cityProgress.CurrentTroops}");
            return;
        }
        if (cityProgress.Grain < grainCost)
        {
            ShowNotify($"粮草不足！需要{grainCost}，当前{cityProgress.Grain}");
            return;
        }

        // 扣除兵力和粮草
        cityProgress.CurrentTroops -= totalSoldiers;
        cityProgress.Grain -= grainCost;

        // 更新或创建军队令牌
        var existingArmy = _armyManager.ArmiesList.FirstOrDefault(a => a.Team == "player" && a.CurrentCityId == sourceCity.Id);
        if (existingArmy != null)
        {
            existingArmy.GeneralIds = generalIds;
            existingArmy.LeadGeneralName = leadName;
            existingArmy.OriginCityId = sourceCity.Id;

            // 应用出征配置
            foreach (var config in deployConfigs)
            {
                existingArmy.SetGeneralDeployConfig(config);
            }

            // 开始移动
            var path = MapPathfinder.FindPath(sourceCity.Id, targetCity.Id, _cities, "player");
            if (path.Count >= 2)
            {
                existingArmy.StartMove(path, _armyManager.CityLookup);
            }
        }
        else
        {
            // 创建新军队
            var newArmy = new ArmyToken
            {
                Id = $"player_army_{Guid.NewGuid():N}".Substring(0, 16),
                GeneralIds = generalIds,
                LeadGeneralName = leadName,
                Team = "player",
                CurrentCityId = sourceCity.Id,
                OriginCityId = sourceCity.Id
            };

            // 应用出征配置
            foreach (var config in deployConfigs)
            {
                newArmy.SetGeneralDeployConfig(config);
            }

            newArmy.UpdateStationaryPosition(_cities.ToDictionary(c => c.Data.Id, c => c));
            _armyManager.ArmiesList.Add(newArmy);

            // 开始移动
            var path = MapPathfinder.FindPath(sourceCity.Id, targetCity.Id, _cities, "player");
            if (path.Count >= 2)
            {
                newArmy.StartMove(path, _armyManager.CityLookup);
            }
        }

        ShowNotify($"{leadName} 出征目标: {targetCity.Name}（兵力-{totalSoldiers} 粮草-{grainCost}）");
    }

    private void HandleCityRightClick(Vector2 worldMouse)
    {
        CityNode? clicked = null;
        foreach (var city in _cities)
        {
            if (city.Bounds.Contains(worldMouse.ToPoint()))
            {
                clicked = city;
                break;
            }
        }

        if (clicked != null && clicked.Data.Owner.ToLower() == "player")
        {
            GameState.Instance.GetOrCreateCityProgress(clicked.Data);
            Game.SceneManager.ChangeScene(new CityDetailScene(clicked.Data.Id, clicked.Data.Name));
        }
    }

    private void HandleArmyArrived(ArmyToken army, string cityId)
    {
        var cityNode = _cities.FirstOrDefault(c => c.Data.Id == cityId);
        if (cityNode == null) return;

        string owner = cityNode.Data.Owner.ToLower();
        bool hasGarrison = cityNode.Data.Garrison.Count > 0;

        if (owner == "player")
        {
            StationArmyAtCity(army, cityId);
            GameState.Instance.Save();
            ShowNotify($"{army.LeadGeneralName} 抵达 {cityNode.Data.Name}");
            return;
        }

        if (owner == "neutral" && !hasGarrison)
        {
            cityNode.Data.Owner = "player";
            GameState.Instance.AddOwnedCity(cityId);
            StationArmyAtCity(army, cityId);
            if (cityNode.Data.UnlockReward.Count > 0)
                GameState.Instance.UnlockGenerals(cityNode.Data.UnlockReward);
            GameState.Instance.AddBattleMerit(50);
            GameState.Instance.Save();
            // 刷新领地渲染和图例面板
            _provinceRenderer.Invalidate();
            _factionPanel.Build(_cities, _scenarioFactions, _allGenerals);
            ShowNotify($"成功占领 {cityNode.Data.Name}！");
            return;
        }

        // Enemy city or city with garrison - trigger auto battle
        if (army.Team == "player")
        {
            LaunchAutoBattle(army, cityNode.Data);
        }
    }

    private void LaunchAutoBattle(ArmyToken playerArmy, CityData targetCity)
    {
        SaveArmyState();

        // 捕获军队武将ID和出发城池ID，战斗结束后用于驻扎和兵力回写
        var armyGeneralIds = playerArmy.GeneralIds.ToList();
        string originCityId = playerArmy.OriginCityId;

        // 使用三国群英传风格战斗场景
        var battleScene = new SangoFieldBattleScene(armyGeneralIds, targetCity.Garrison, () =>
        {
            // 简化结果判定: 回到世界地图后检查
            var simResult = SimulateBattleResult(armyGeneralIds, targetCity);
            OnAutoBattleComplete(simResult, targetCity, armyGeneralIds, originCityId);
        });
        Game.SceneManager.ChangeScene(battleScene);
    }

    /// <summary>根据武将属性简单判定战斗结果 (临时，后续由战场实际结果决定)</summary>
    private AutoBattleResult SimulateBattleResult(List<string> playerGenIds, CityData targetCity)
    {
        float playerPower = 0;
        foreach (var id in playerGenIds)
        {
            var gen = _allGenerals.FirstOrDefault(g => g.Id == id);
            if (gen != null) playerPower += gen.Strength + gen.Intelligence + gen.Command;
        }
        float enemyPower = 0;
        foreach (var sq in targetCity.Garrison)
        {
            var gen = _allGenerals.FirstOrDefault(g => g.Id == sq.GeneralId);
            if (gen != null) enemyPower += gen.Strength + gen.Intelligence + gen.Command;
        }
        return new AutoBattleResult
        {
            IsVictory = playerPower >= enemyPower * 0.7f,
            PlayerLost = 0,
            EnemyLost = targetCity.Garrison.Count
        };
    }

    private void OnAutoBattleComplete(AutoBattleResult result, CityData targetCity, List<string> armyGeneralIds, string originCityId)
    {
        var gs = GameState.Instance;

        if (result.IsVictory)
        {
            int garrisonCount = targetCity.Garrison.Count;
            targetCity.Owner = "player";
            targetCity.Garrison = new();
            gs.AddOwnedCity(targetCity.Id);
            gs.UnlockGenerals(targetCity.UnlockReward);

            // 将出征武将驻扎到占领的城池
            foreach (var genId in armyGeneralIds)
            {
                var gp = gs.GetGeneralProgress(genId);
                if (gp == null) continue;
                if (!string.IsNullOrEmpty(gp.CurrentCityId) && gp.CurrentCityId != targetCity.Id)
                    gs.RemoveGeneralFromCity(gp.CurrentCityId, genId);
                gp.CurrentCityId = targetCity.Id;
                gp.IsOnExpedition = false;
                gs.AddGeneralToCity(targetCity.Id, genId);
            }

            // 战后幸存兵力写入目标城池
            var targetProgress = gs.GetOrCreateCityProgress(targetCity);
            int totalSurvivors = result.SurvivingSoldiers.Values.Sum();
            targetProgress.CurrentTroops += totalSurvivors;

            // 发放战功
            gs.AddBattleMerit(100 + garrisonCount * 50 + result.MeritReward);

            // 发放资源奖励到城池
            if (!string.IsNullOrEmpty(result.PerformanceRating))
            {
                targetProgress.AddResource(ResourceType.Gold, result.GoldReward);
                targetProgress.AddResource(ResourceType.Food, result.FoodReward);
                targetProgress.AddResource(ResourceType.Wood, result.WoodReward);
                targetProgress.AddResource(ResourceType.Iron, result.IronReward);
            }

            // 添加俘虏武将
            foreach (var genId in result.CapturedGenerals)
            {
                gs.AddCaptive(genId);
            }
            if (result.CapturedGenerals.Count > 0)
            {
                ShowNotify($"胜利！获得 {result.PerformanceRating} 级评价，俘虏{result.CapturedGenerals.Count}名武将");
            }
            else
            {
                ShowNotify($"胜利！获得 {result.PerformanceRating} 级评价");
            }
        }
        else
        {
            // 战败：幸存兵力返回出发城池
            if (!string.IsNullOrEmpty(originCityId))
            {
                var originProgress = gs.GetCityProgress(originCityId);
                if (originProgress != null)
                {
                    int totalSurvivors = result.SurvivingSoldiers.Values.Sum();
                    originProgress.CurrentTroops += totalSurvivors;
                }
            }

            // 战败武将返回出发城
            foreach (var genId in armyGeneralIds)
            {
                var gp = gs.GetGeneralProgress(genId);
                if (gp == null) continue;
                // 阵亡武将不返回
                if (!result.SurvivingSoldiers.ContainsKey(genId)) continue;
                if (!string.IsNullOrEmpty(gp.CurrentCityId) && gp.CurrentCityId != originCityId)
                    gs.RemoveGeneralFromCity(gp.CurrentCityId, genId);
                gp.CurrentCityId = originCityId;
                gp.IsOnExpedition = false;
                if (!string.IsNullOrEmpty(originCityId))
                    gs.AddGeneralToCity(originCityId, genId);
            }

            ShowNotify("战斗失败...");
        }

        gs.Save();
        Game.SceneManager.ChangeScene(new WorldMapScene());
    }

    public void OnBattleVictory(CityData? targetCity, List<string>? capturedGenerals = null)
    {
        if (targetCity != null)
        {
            int garrisonCount = targetCity.Garrison.Count;
            targetCity.Owner = "player";
            targetCity.Garrison = new();
            GameState.Instance.AddOwnedCity(targetCity.Id);
            GameState.Instance.UnlockGenerals(targetCity.UnlockReward);
            GameState.Instance.AddBattleMerit(100 + garrisonCount * 50);

            // 处理俘虏
            if (capturedGenerals != null)
            {
                foreach (var genId in capturedGenerals)
                {
                    GameState.Instance.AddCaptive(genId);
                }
                if (capturedGenerals.Count > 0)
                {
                    ShowNotify($"胜利！俘虏了{capturedGenerals.Count}名武将");
                }
            }

            GameState.Instance.Save();
            // 刷新领地渲染和图例面板
            _provinceRenderer.Invalidate();
            _factionPanel.Build(_cities, _scenarioFactions, _allGenerals);
        }
    }

    private void SaveArmyState()
    {
        var entries = _armyManager.Armies.Select(a => new ArmySaveEntry
        {
            Id = a.Id,
            GeneralIds = a.GeneralIds.ToList(),
            CurrentCityId = a.CurrentCityId ?? "",
            Team = a.Team,
            // 行军状态
            TargetCityId = a.TargetCityId,
            MovePath = a.MovePath?.ToList(),
            CurrentSegmentIndex = a.CurrentSegmentIndex,
            DaysPerSegment = a.DaysPerSegment?.ToArray(),
            DaysElapsedInSegment = a.DaysElapsedInSegment,
            TotalDaysRemaining = a.TotalDaysRemaining,
            OriginCityId = a.OriginCityId
        }).ToList();
        GameState.Instance.SaveArmyState(entries);
    }

    private void RestoreArmyMarchState()
    {
        var savedArmies = GameState.Instance.GetSavedArmies();
        if (savedArmies.Count == 0) return;

        foreach (var saved in savedArmies)
        {
            if (saved.MovePath == null || saved.MovePath.Count < 2) continue;

            var army = _armyManager.ArmiesList.FirstOrDefault(a => a.Id == saved.Id);
            if (army == null) continue;

            // 恢复行军路径和天数状态
            army.MovePath = saved.MovePath.ToList();
            army.TargetCityId = saved.TargetCityId;
            army.CurrentSegmentIndex = saved.CurrentSegmentIndex;
            army.DaysPerSegment = saved.DaysPerSegment?.ToArray();
            army.DaysElapsedInSegment = saved.DaysElapsedInSegment;
            army.TotalDaysRemaining = saved.TotalDaysRemaining;
            army.OriginCityId = saved.OriginCityId;
            army.CurrentCityId = null;

            // 更新视觉位置到行军中的位置
            army.ScreenPosition = army.ComputeMarchPosition(_armyManager.CityLookup);
        }
    }

    /// <summary>
    /// 将军队中的武将驻扎到目标城池：
    /// 1. 从原城池移除武将
    /// 2. 更新武将的 CurrentCityId
    /// 3. 添加武将到新城池的 GeneralIds
    /// </summary>
    private void StationArmyAtCity(ArmyToken army, string cityId)
    {
        var gs = GameState.Instance;
        foreach (var genId in army.GeneralIds)
        {
            var progress = gs.GetGeneralProgress(genId);
            if (progress == null) continue;

            // 从旧城池移除
            if (!string.IsNullOrEmpty(progress.CurrentCityId) && progress.CurrentCityId != cityId)
            {
                gs.RemoveGeneralFromCity(progress.CurrentCityId, genId);
            }

            // 更新武将所在城池
            progress.CurrentCityId = cityId;
            progress.IsOnExpedition = false;

            // 添加到新城池
            gs.AddGeneralToCity(cityId, genId);
        }
    }

    private void ShowNotify(string text)
    {
        _notifyText = text;
        _notifyTimer = 3f;
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(35, 30, 22));

        // === 缓存阶段：在 SpriteBatch 外重建 RenderTarget 缓存 ===
        _provinceRenderer.EnsureCache(GraphicsDevice, SpriteBatch, _pixel, _cities);
        _fogOfWar.EnsureCache(GraphicsDevice, SpriteBatch, _pixel, _cities);

        // === World space (camera-transformed) ===
        SpriteBatch.Begin(transformMatrix: _camera.GetTransformMatrix());

        // 1. Cached painted background
        _bgRenderer.Draw(SpriteBatch);

        // 2. Enhanced terrain features
        _terrainRenderer.Draw(SpriteBatch, _pixel, _smallFont, _terrainFeatures,
            MapLeft, MapTop, MapRight, MapBottom);

        // 3. Styled roads
        _roadRenderer.Draw(SpriteBatch, _pixel, _cities, _fogOfWar);

        // 4. Province ownership shading (cached texture)
        _provinceRenderer.Draw(SpriteBatch);

        // 5. Smooth fog overlay (cached texture)
        _fogOfWar.Draw(SpriteBatch);

        // 6. Fortress city icons
        _cityRenderer.Draw(SpriteBatch, _pixel, _font, _smallFont, _cities,
            _armyManager.SelectedArmy, _sceneTime);

        // 7. Army tokens + path preview
        _armyManager.Draw(SpriteBatch, _pixel, _font);

        SpriteBatch.End();

        // === Screen space (HUD, buttons, status) ===
        SpriteBatch.Begin();

        // 8. Top HUD
        DrawHUD();

        // 8.5 Faction legend panel
        _factionPanel.Draw(SpriteBatch, _pixel, _font, _smallFont);

        // 8.6 Enemy city info panel
        if (_enemyCityPanel.IsActive)
        {
            _enemyCityPanel.Draw(SpriteBatch, _pixel, _font, _smallFont);
        }

        // 9. Status bar
        SpriteBatch.DrawString(_font, _statusText,
            new Vector2(30, GameSettings.ScreenHeight - 35), new Color(180, 160, 120));

        // 10. Notification text (center, fading)
        if (_notifyTimer > 0 && !string.IsNullOrEmpty(_notifyText))
        {
            float alpha = Math.Min(1f, _notifyTimer);
            var notifySize = _titleFont.MeasureString(_notifyText);
            SpriteBatch.DrawString(_titleFont, _notifyText,
                new Vector2((GameSettings.ScreenWidth - notifySize.X) / 2, GameSettings.ScreenHeight / 2 - 50),
                new Color(255, 220, 100) * alpha);
        }

        // 城池操作对话框
        if (_cityDialog.IsActive)
        {
            // 绘制行军路线预览（在世界空间）
            if (_cityDialog.Phase == CityActionPhase.MilitarySelectTarget && _cityDialog.MovePath != null)
            {
                SpriteBatch.Begin(transformMatrix: _camera.GetTransformMatrix());
                DrawMovePathPreview(_cityDialog.MovePath);
                SpriteBatch.End();
            }

            _cityDialog.Draw(SpriteBatch, _pixel, _font, _titleFont, Input);
        }

        // 存档面板（最顶层绘制）
        if (_saveLoadPanel.IsActive)
        {
            _saveLoadPanel.Draw(SpriteBatch, _pixel, _font, _smallFont);
        }

        SpriteBatch.End();
    }

    private void DrawMovePathPreview(List<string> path)
    {
        if (path == null || path.Count < 2) return;

        var cityLookup = _cities.ToDictionary(c => c.Data.Id, c => c);
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (!cityLookup.TryGetValue(path[i], out var from) ||
                !cityLookup.TryGetValue(path[i + 1], out var to))
                continue;

            // 绘制连接线
            Vector2 fromPos = from.Center;
            Vector2 toPos = to.Center;

            // 使用虚线效果
            int segments = 10;
            for (int j = 0; j < segments; j++)
            {
                float t1 = (float)j / segments;
                float t2 = (float)(j + 1) / segments;
                Vector2 p1 = Vector2.Lerp(fromPos, toPos, t1);
                Vector2 p2 = Vector2.Lerp(fromPos, toPos, t2);

                Color lineColor = new Color((byte)255, (byte)200, (byte)80, (byte)(180 * (1 - t1)));
                DrawLine(SpriteBatch, _pixel, p1, p2, lineColor, 2);
            }
        }
    }

    private void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
    {
        Vector2 diff = end - start;
        float length = diff.Length();
        if (length < 1) return;
        float angle = MathF.Atan2(diff.Y, diff.X);
        sb.Draw(pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
    }

    private void DrawHUD()
    {
        // Top bar background
        SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, 55), new Color(25, 20, 14, 235));
        SpriteBatch.Draw(_pixel, new Rectangle(0, 55, GameSettings.ScreenWidth, 2), new Color(100, 80, 50, 160));

        // Title
        SpriteBatch.DrawString(_font, "猫三国 · 战略地图", new Vector2(15, 15), new Color(220, 190, 130));

        // Stats
        int playerCities = _cities.Count(c => c.Data.Owner.ToLower() == "player");
        int totalCities = _cities.Count;
        SpriteBatch.DrawString(_font, $"城池: {playerCities}/{totalCities}", new Vector2(250, 15), new Color(100, 160, 230));

        // 当前年份
        string yearText = $"公元{GameState.Instance.CurrentDate.Year}年";
        SpriteBatch.DrawString(_font, yearText, new Vector2(420, 15), new Color(220, 190, 130));

        // 返回主菜单按钮
        var returnRect = new Rectangle(GameSettings.ScreenWidth - 140, 10, 130, 35);
        bool returnHover = Input.MousePosition.ToPoint().ToVector2().X >= returnRect.X &&
                           Input.MousePosition.ToPoint().ToVector2().X <= returnRect.Right &&
                           Input.MousePosition.ToPoint().ToVector2().Y >= returnRect.Y &&
                           Input.MousePosition.ToPoint().ToVector2().Y <= returnRect.Bottom;
        SpriteBatch.Draw(_pixel, returnRect, returnHover ? new Color(80, 50, 50) : new Color(60, 40, 40));
        DrawBorder(SpriteBatch, _pixel, returnRect, new Color(150, 100, 100), 1);
        var returnSize = _font.MeasureString("返回主菜单");
        SpriteBatch.DrawString(_font, "返回主菜单",
            new Vector2(returnRect.X + (returnRect.Width - returnSize.X) / 2, returnRect.Y + (returnRect.Height - returnSize.Y) / 2),
            new Color(220, 190, 130));

        // 存档按钮
        var saveRect = new Rectangle(GameSettings.ScreenWidth - 280, 10, 130, 35);
        bool saveHover = saveRect.Contains(Input.MousePosition.ToPoint());
        SpriteBatch.Draw(_pixel, saveRect, saveHover ? new Color(60, 55, 40) : new Color(45, 40, 30));
        DrawBorder(SpriteBatch, _pixel, saveRect, new Color(130, 110, 70), 1);
        var saveSize = _font.MeasureString("存档管理");
        SpriteBatch.DrawString(_font, "存档管理",
            new Vector2(saveRect.X + (saveRect.Width - saveSize.X) / 2, saveRect.Y + (saveRect.Height - saveSize.Y) / 2),
            new Color(220, 200, 150));

    }

    private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
