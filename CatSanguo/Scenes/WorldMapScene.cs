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
    private const float MapLeft = 80;
    private const float MapTop = 80;
    private const float MapRight = 1920;
    private const float MapBottom = 1320;
    private const float GridMaxX = 15f;
    private const float GridMaxY = 9f;

    // 城池操作对话框
    private CityActionDialog _cityDialog = null!;

    private string _statusText = "点击城池选择军事/内政操作";
    private float _productionTimer = 0f;
    private float _notifyTimer = 0f;
    private string _notifyText = "";
    private float _sceneTime = 0f;

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.NotifyFont;
        _smallFont = Game.SmallFont;

        // Load data
        string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _allCityData = DataLoader.LoadList<CityData>(Path.Combine(dataPath, "cities.json"));
        _allGenerals = DataLoader.LoadList<GeneralData>(Path.Combine(dataPath, "generals.json"));
        _allStages = DataLoader.LoadList<StageData>(Path.Combine(dataPath, "stages.json"));

        // Load terrain features
        string terrainPath = Path.Combine(dataPath, "terrain_features.json");
        if (File.Exists(terrainPath))
            _terrainFeatures = DataLoader.LoadList<TerrainFeatureData>(terrainPath);

        // Initialize GameState
        GameState.Instance.Initialize(_allGenerals);

        // Sync city ownership: register initial player cities from JSON into GameState,
        // then apply saved state back to city data + fix garrison persistence
        foreach (var city in _allCityData)
        {
            // Register JSON-defined player cities into GameState (first launch fix)
            if (city.Owner.ToLower() == "player")
                GameState.Instance.AddOwnedCity(city.Id);

            // Apply saved ownership (captures post-battle state)
            if (GameState.Instance.OwnsCity(city.Id))
            {
                city.Owner = "player";
                city.Garrison.Clear(); // Player-owned cities should not have hostile garrisons
            }
        }

        CreateCityNodes();
        CreateButtons();

        // Initialize camera
        _camera = new Camera2D(GraphicsDevice);
        _camera.WorldBounds = new Rectangle(0, 0, 2000, 1400);
        _camera.MinZoom = 0.5f;
        _camera.MaxZoom = 1.5f;

        // Center camera on player starting area (Chengdu region)
        var playerCity = _cities.FirstOrDefault(c => c.Data.Owner.ToLower() == "player");
        if (playerCity != null)
            _camera.Position = playerCity.Center;
        else
            _camera.Position = new Vector2(1000, 700);

        _camera.SetZoom(0.7f);
        _camera.ClampPosition();

        // Initialize fog of war (16x10 grid matching new city grid)
        _fogOfWar = new FogOfWarManager(16, 10);

        // Initialize army manager
        _armyManager = new ArmyManager();
        _armyManager.Initialize(_cities, _allGenerals);
        _armyManager.OnArmyArrived += HandleArmyArrived;

        // Pre-render background
        _bgRenderer.Invalidate();
        _bgRenderer.EnsureCache(GraphicsDevice, SpriteBatch, _pixel, _cities);

        // 初始化城池操作对话框
        _cityDialog = new CityActionDialog();
        _cityDialog.Initialize(GetGeneralName, _font, _titleFont, () => Game.SceneManager.ChangeScene(new GeneralRosterScene()));

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

        // Production tick every 30 seconds
        _productionTimer += dt;
        if (_productionTimer >= 30f)
        {
            _productionTimer = 0f;
            GameState.Instance.RunProductionTick();
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

        // Convert mouse to world space
        Vector2 worldMouse = _camera.ScreenToWorld(Input.MousePosition);

        // 如果对话框激活，处理对话框输入
        if (_cityDialog.IsActive)
        {
            _cityDialog.WorldMousePos = worldMouse;
            _cityDialog.Update(Input, _cities);
            UpdateStatusText();
            return;
        }

        // Update army manager (handles movement + click input)
        _armyManager.Update(dt, Input, worldMouse);

        // Update fog of war
        UpdateFog();

        // Handle left-click on cities to open action dialog
        if (Input.IsMouseClicked())
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
                _cityDialog.Open(city.Data, _allGenerals,
                    onClose: () => { },
                    onLaunchArmy: OnLaunchArmyFromDialog
                );
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
        if (_cityDialog.IsActive && _cityDialog.Phase != CityActionPhase.Main)
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

        // 更新或创建军队令牌
        var existingArmy = _armyManager.ArmiesList.FirstOrDefault(a => a.Team == "player" && a.CurrentCityId == sourceCity.Id);
        if (existingArmy != null)
        {
            existingArmy.GeneralIds = generalIds;
            existingArmy.LeadGeneralName = leadName;

            // 应用出征配置
            foreach (var config in deployConfigs)
            {
                existingArmy.SetGeneralDeployConfig(config);
            }

            // 开始移动
            var path = MapPathfinder.FindPath(sourceCity.Id, targetCity.Id, _cities, "player");
            if (path.Count >= 2)
            {
                existingArmy.StartMove(path);
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
                CurrentCityId = sourceCity.Id
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
                newArmy.StartMove(path);
            }
        }

        ShowNotify($"{leadName} 出征目标: {targetCity.Name}");
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
            ShowNotify($"{army.LeadGeneralName} 抵达 {cityNode.Data.Name}");
            return;
        }

        if (owner == "neutral" && !hasGarrison)
        {
            cityNode.Data.Owner = "player";
            GameState.Instance.AddOwnedCity(cityId);
            if (cityNode.Data.UnlockReward.Count > 0)
                GameState.Instance.UnlockGenerals(cityNode.Data.UnlockReward);
            GameState.Instance.AddBattleMerit(50);
            GameState.Instance.Save();
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

        var autoBattle = new AutoBattleScene(playerArmy, targetCity, _allGenerals, result =>
        {
            OnAutoBattleComplete(result, targetCity);
        });
        Game.SceneManager.ChangeScene(autoBattle);
    }

    private void OnAutoBattleComplete(AutoBattleResult result, CityData targetCity)
    {
        if (result.IsVictory)
        {
            int garrisonCount = targetCity.Garrison.Count;
            targetCity.Owner = "player";
            targetCity.Garrison = new();
            GameState.Instance.AddOwnedCity(targetCity.Id);
            GameState.Instance.UnlockGenerals(targetCity.UnlockReward);

            // 发放战功
            GameState.Instance.AddBattleMerit(100 + garrisonCount * 50 + result.MeritReward);

            // 发放资源奖励到城池
            if (!string.IsNullOrEmpty(result.PerformanceRating))
            {
                var progress = GameState.Instance.GetOrCreateCityProgress(targetCity);
                progress.AddResource(ResourceType.Gold, result.GoldReward);
                progress.AddResource(ResourceType.Food, result.FoodReward);
                progress.AddResource(ResourceType.Wood, result.WoodReward);
                progress.AddResource(ResourceType.Iron, result.IronReward);
            }

            // 添加俘虏武将
            foreach (var genId in result.CapturedGenerals)
            {
                GameState.Instance.AddCaptive(genId);
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
            ShowNotify("战斗失败...");
        }

        GameState.Instance.Save();
        Game.SceneManager.ChangeScene(new WorldMapScene());
    }

    public void OnBattleVictory(CityData? targetCity)
    {
        if (targetCity != null)
        {
            int garrisonCount = targetCity.Garrison.Count;
            targetCity.Owner = "player";
            targetCity.Garrison = new();
            GameState.Instance.AddOwnedCity(targetCity.Id);
            GameState.Instance.UnlockGenerals(targetCity.UnlockReward);
            GameState.Instance.AddBattleMerit(100 + garrisonCount * 50);
            GameState.Instance.Save();
        }
    }

    private void SaveArmyState()
    {
        var entries = _armyManager.Armies.Select(a => new ArmySaveEntry
        {
            Id = a.Id,
            GeneralIds = a.GeneralIds.ToList(),
            CurrentCityId = a.CurrentCityId ?? "",
            Team = a.Team
        }).ToList();
        GameState.Instance.SaveArmyState(entries);
    }

    private void ShowNotify(string text)
    {
        _notifyText = text;
        _notifyTimer = 3f;
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(35, 30, 22));

        // === World space (camera-transformed) ===
        SpriteBatch.Begin(transformMatrix: _camera.GetTransformMatrix());

        // 1. Cached painted background
        _bgRenderer.Draw(SpriteBatch);

        // 2. Province ownership shading
        _provinceRenderer.Draw(SpriteBatch, _pixel, _cities);

        // 3. Enhanced terrain features
        _terrainRenderer.Draw(SpriteBatch, _pixel, _smallFont, _terrainFeatures,
            MapLeft, MapTop, MapRight, MapBottom);

        // 4. Styled roads
        _roadRenderer.Draw(SpriteBatch, _pixel, _cities, _fogOfWar);

        // 5. Smooth fog overlay
        _fogOfWar.Draw(SpriteBatch, _pixel, _cities);

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
            _cityDialog.Draw(SpriteBatch, _pixel, _font, _titleFont, Input);

            // 绘制行军路线预览（在世界空间）
            if (_cityDialog.Phase == CityActionPhase.MilitarySelectTarget && _cityDialog.MovePath != null)
            {
                DrawMovePathPreview(_cityDialog.MovePath);
            }
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
        SpriteBatch.DrawString(_font, $"城池: {playerCities}/{totalCities}", new Vector2(300, 15), new Color(100, 160, 230));

        // Battle merit
        SpriteBatch.DrawString(_font, $"战功: {GameState.Instance.BattleMerit}", new Vector2(480, 15), new Color(255, 200, 80));

        // Current squad
        var squad = GameState.Instance.CurrentSquad;
        string squadText = "编队: " + string.Join(", ", squad.Select(id =>
        {
            var gen = _allGenerals.FirstOrDefault(g => g.Id == id);
            return gen?.Name ?? "?";
        }).Take(3));
        SpriteBatch.DrawString(_smallFont, squadText,
            new Vector2(650, 33),
            new Color(180, 160, 120));
    }
}
