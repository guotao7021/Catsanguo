using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Core.Animation;
using CatSanguo.Battle;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.Generals;
using CatSanguo.Skills;
using CatSanguo.AI;
using CatSanguo.UI;
using CatSanguo.UI.Battle;

namespace CatSanguo.Scenes;

public enum BattlePhase
{
    Deploy,
    Countdown,
    Fighting,
    Result
}

public class BattleScene : Scene
{
    // Data
    private StageData _stageData;
    private List<string> _playerGeneralIds;
    private List<GeneralData> _allGeneralData;
    private List<SkillData> _allSkillData = new();

    // Battle state
    private BattlePhase _phase = BattlePhase.Deploy;
    private List<Squad> _allSquads = new();
    private List<Squad> _playerSquads = new();
    private List<Squad> _enemySquads = new();
    private MoraleSystem _moraleSystem = new();
    private BattleAI _enemyAI;
    private FloatingTextManager _floatingTexts = new();
    private float _battleTime;
    private float _countdownTimer = 3f;
    private float _speedMultiplier = 1f;
    private string _resultText = "";
    private bool _isVictory;
    private List<string> _capturedGenerals = new();

    // Deploy state
    private int _deployingSquadIndex = -1;
    private List<Squad> _deployableSquads = new();
    private Rectangle _deployZone;

    // UI
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private int _selectedPlayerSquadIndex = -1;
    private Button _startBattleButton;
    private BattleUIManager _uiManager = null!;

    // Visual effects
    private List<SkillVFX> _activeVFX = new();
    private float _shakeTimer;
    private float _shakeIntensity;

    // Death notifications
    private DeathNotificationManager _deathNotifications = new();
    private SpriteFontBase _smallFont;
    private SpriteFontBase _notifyFont;

    // Track previous HP for damage numbers
    private Dictionary<Squad, float> _prevHP = new();
    
    // Battle result tracking
    private Dictionary<string, int> _generalKillCounts = new();
    private Dictionary<string, float> _generalDamageDealt = new();
    private Dictionary<string, int> _skillsUsedThisBattle = new();

    // World map context
    private CityData? _targetCity;

    // City defense bonus (applied to enemy squads when defending)
    private float _garrisonDefenseBonus = 0f;

    // ===== 核心战斗三件套 =====
    private EventBus _eventBus = null!;
    private BuffSystem _buffSystem = null!;
    private SkillTriggerSystem _skillTriggerSystem = null!;
    private BattleContext _battleContext = null!;

    public BattleScene(StageData stageData, List<string> playerGeneralIds, List<GeneralData> allGeneralData, CityData? targetCity = null)
    {
        _stageData = stageData;
        _playerGeneralIds = playerGeneralIds;
        _allGeneralData = allGeneralData;
        _targetCity = targetCity;
    }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;
        _smallFont = Game.SmallFont;
        _notifyFont = Game.NotifyFont;

        // Load skill data
        string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _allSkillData = DataLoader.LoadList<SkillData>(Path.Combine(dataPath, "skills.json"));
        var formations = DataLoader.LoadList<FormationData>(Path.Combine(dataPath, "formations.json"));

        _deployZone = new Rectangle(30, 60, 500, GameSettings.ScreenHeight - 120);

        // Apply city garrison defense bonus if targeting a city
        if (_targetCity != null)
        {
            _garrisonDefenseBonus = _targetCity.GarrisonDefenseBonus;
        }

        // Create player squads
        int slotY = 200;
        foreach (string genId in _playerGeneralIds)
        {
            var genData = _allGeneralData.FirstOrDefault(g => g.Id == genId);
            if (genData == null) continue;

            var squad = CreateSquad(genData, Team.Player, formations);
            squad.Position = new Vector2(200, slotY);
            _deployableSquads.Add(squad);
            slotY += 150;
        }

        // Create enemy squads - use city garrison if available, otherwise use stage data
        List<StageSquadData> enemySquadData;
        if (_targetCity != null && _targetCity.Garrison.Count > 0)
        {
            enemySquadData = _targetCity.Garrison;
        }
        else
        {
            enemySquadData = _stageData.EnemySquads;
        }

        foreach (var es in enemySquadData)
        {
            var genData = _allGeneralData.FirstOrDefault(g => g.Id == es.GeneralId);
            if (genData == null) continue;

            FormationType ft = Enum.TryParse<FormationType>(es.FormationType, true, out var parsed)
                ? parsed : FormationType.Vanguard;

            var squad = CreateSquad(genData, Team.Enemy, formations);
            squad.Formation = ft;
            squad.Position = new Vector2(es.PositionX, es.PositionY);
            squad.FacingDirection = -1;
            squad.SoldierCount = es.SoldierCount;
            squad.MaxSoldierCount = es.SoldierCount;

            // Apply city garrison defense bonus (使用新属性系统)
            if (_garrisonDefenseBonus > 0f)
            {
                squad.Attributes.AddModifier(new Modifier("garrison", AttrType.Defense, ModifierOp.Multiply, _garrisonDefenseBonus));
            }

            squad.InitializeSoldierOffsets();
            _enemySquads.Add(squad);
            _allSquads.Add(squad);
            _prevHP[squad] = squad.HP;
        }

        // Set enemy positions in a line if no position specified
        int enemyIdx = 0;
        foreach (var squad in _enemySquads)
        {
            if (squad.Position.X == 0 && squad.Position.Y == 0)
            {
                squad.Position = new Vector2(GameSettings.ScreenWidth - 150 - enemyIdx * 80, 200 + enemyIdx * 120);
            }
            enemyIdx++;
        }

        _enemyAI = new BattleAI(Team.Enemy, _stageData.Difficulty);

        // UI Buttons
        _startBattleButton = new Button("开 战 !", new Rectangle(GameSettings.ScreenWidth / 2 - 80, GameSettings.ScreenHeight - 60, 160, 45));
        _startBattleButton.NormalColor = new Color(140, 50, 30);
        _startBattleButton.HoverColor = new Color(180, 70, 40);
        _startBattleButton.OnClick = StartBattle;

        // ===== 初始化战斗UI管理器 =====
        _uiManager = new BattleUIManager(BattleUIMode.Manual);
        _uiManager.Initialize(_pixel, _font, _titleFont, _smallFont);
        _uiManager.SetSquadLists(_playerSquads, _enemySquads);
        _uiManager.HUD.StageName = _stageData.Name;
        _uiManager.HUD.OnSpeedToggled = ToggleSpeed;
        _uiManager.SkillPanel.OnSquadSelected = (idx) => { _selectedPlayerSquadIndex = idx; };
        _uiManager.SkillPanel.OnSkillActivated = (squad) => TryUseSkill(squad);
        _uiManager.ResultPanel.OnContinue = () =>
        {
            if (_targetCity != null)
            {
                var worldMap = new WorldMapScene();
                if (_isVictory)
                    worldMap.OnBattleVictory(_targetCity, _capturedGenerals);
                Game.SceneManager.ChangeScene(worldMap);
            }
            else
            {
                Game.SceneManager.ChangeScene(new StageSelectScene());
            }
        };

        // ===== 初始化核心战斗系统 =====
        _eventBus = new EventBus();
        _buffSystem = new BuffSystem(_eventBus);
        _skillTriggerSystem = new SkillTriggerSystem(_eventBus, _buffSystem, _allSquads, new Random());
        _battleContext = new BattleContext(_eventBus, _buffSystem, _skillTriggerSystem);

        // 加载Buff配置
        string buffsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "buffs.json");
        _buffSystem.LoadConfigs(buffsPath);

        // 设置UI管理器的BuffSystem引用
        _uiManager.SetBuffSystem(_buffSystem);

        // 为所有已创建的Squad初始化AttributeSet
        InitializeSquadAttributes();
    }

    private void InitializeSquadAttributes()
    {
        // Context在Enter()中已创建，遍历所有Squad设置Context
        // Attributes在CreateSquad()中已初始化
        var allSquads = _deployableSquads.Concat(_enemySquads).ToList();
        foreach (var squad in allSquads)
        {
            squad.Context = _battleContext;
        }
    }

    private Squad CreateSquad(GeneralData genData, Team team, List<FormationData> formations)
    {
        var general = General.FromData(genData);
        FormationType ft = Enum.TryParse<FormationType>(genData.PreferredFormation, true, out var parsed)
            ? parsed : FormationType.Vanguard;

        var formData = formations.FirstOrDefault(f =>
            f.Type.Equals(ft.ToString(), StringComparison.OrdinalIgnoreCase));

        float baseHP = formData?.BaseHP ?? 500;
        float baseAtk = formData?.BaseAttack ?? 30;
        float baseDef = formData?.BaseDefense ?? 30;
        float baseSpd = formData?.BaseSpeed ?? 1f;
        float atkRange = formData?.AttackRange ?? 40;

        var squad = new Squad
        {
            General = general,
            Formation = ft,
            Team = team,
            HP = baseHP + general.Leadership * 5,
            MaxHP = baseHP + general.Leadership * 5,
            BaseAttack = baseAtk,
            BaseDefense = baseDef,
            BaseSpeed = baseSpd,
            AttackRange = atkRange,
            SoldierCount = 30,
            MaxSoldierCount = 30,
            Morale = 100,
            // Initialize AttributeSet with formation base values
            Attributes = new AttributeSet()
        };

        // Set base values for AttributeSet (同步到新属性系统)
        squad.Attributes.SetBase(AttrType.MaxHP, baseHP + general.Leadership * 5);
        squad.Attributes.SetBase(AttrType.Attack, baseAtk);
        squad.Attributes.SetBase(AttrType.Defense, baseDef);
        squad.Attributes.SetBase(AttrType.Speed, baseSpd * 100); // 速度缩放
        squad.Attributes.SetBase(AttrType.CritRate, 5); // 默认5%暴击
        squad.Attributes.SetBase(AttrType.CritDamage, 150); // 默认150%暴伤
        squad.Attributes.SetBase(AttrType.AttackRange, atkRange);

        // 添加General属性加成
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_str", AttrType.Attack, ModifierOp.Add, general.Strength * 2));
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_lvl", AttrType.Defense, ModifierOp.Add, general.Leadership * 1.5f));
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_spd", AttrType.Speed, ModifierOp.Add, general.Speed * 1.2f));

        // Attach skills
        var activeSkillData = _allSkillData.FirstOrDefault(s => s.Id == genData.ActiveSkillId);
        if (activeSkillData != null)
            squad.ActiveSkill = Skill.FromData(activeSkillData);

        var passiveSkillData = _allSkillData.FirstOrDefault(s => s.Id == genData.PassiveSkillId);
        if (passiveSkillData != null)
        {
            squad.PassiveSkill = Skill.FromData(passiveSkillData);
            ApplyPassive(squad, squad.PassiveSkill);
        }

        squad.InitializeSoldierOffsets();

        // Initialize animators
        string soldierKey = "soldier_" + squad.Formation.ToString().ToLower();
        squad.SoldierAnimator = Game.SpriteSheets.CreateAnimator(soldierKey);
        squad.GeneralAnimator = Game.SpriteSheets.CreateAnimator("general_default");

        return squad;
    }

    private void ApplyPassive(Squad squad, Skill passive)
    {
        switch (passive.EffectType)
        {
            case "buff":
                if (passive.BuffStat == "attack")
                {
                    squad.Attributes.AddModifier(new Modifier($"passive_{passive.Id}", AttrType.Attack, ModifierOp.Multiply, passive.BuffPercent));
                    squad.AttackBuffPercent += passive.BuffPercent;
                }
                else if (passive.BuffStat == "defense")
                {
                    squad.Attributes.AddModifier(new Modifier($"passive_{passive.Id}", AttrType.Defense, ModifierOp.Multiply, passive.BuffPercent));
                    squad.DefenseBuffPercent += passive.BuffPercent;
                }
                break;
        }
    }

    private void StartBattle()
    {
        if (_deployableSquads.Count == 0) return;

        // Move any undeployed squads to default positions
        int idx = 0;
        foreach (var squad in _deployableSquads)
        {
            if (!_playerSquads.Contains(squad))
            {
                squad.Position = new Vector2(150, 200 + idx * 150);
                _playerSquads.Add(squad);
                _allSquads.Add(squad);
                _prevHP[squad] = squad.HP;
            }
            idx++;
        }

        // ===== 初始化技能触发系统 =====
        _battleContext.AllSquads = _allSquads;

        // Register all squads for skill triggers
        foreach (var squad in _allSquads)
        {
            _skillTriggerSystem.RegisterSquad(squad);
        }
        _skillTriggerSystem.Initialize();

        // Publish OnBattleStart event (triggers passive skills)
        _eventBus.Publish(new GameEvent(GameEventType.OnBattleStart));

        _phase = BattlePhase.Countdown;
        _countdownTimer = 3f;
    }

    private void ToggleSpeed()
    {
        _speedMultiplier = _speedMultiplier >= 2f ? 1f : 2f;
    }

    public override void Update(GameTime gameTime)
    {
        float rawDt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        switch (_phase)
        {
            case BattlePhase.Deploy:
                UpdateDeploy(rawDt);
                break;
            case BattlePhase.Countdown:
                UpdateCountdown(rawDt);
                break;
            case BattlePhase.Fighting:
                float dt = rawDt * _speedMultiplier;
                UpdateFighting(dt);
                break;
            case BattlePhase.Result:
                break;
        }

        // 更新UI管理器
        bool paused = false;
        _uiManager.Update(rawDt, Input, _battleTime, _speedMultiplier, paused);

        // Floating texts always update
        _floatingTexts.Update(rawDt);

        // Death notifications update
        _deathNotifications.Update(rawDt);

        // VFX update
        for (int i = _activeVFX.Count - 1; i >= 0; i--)
        {
            _activeVFX[i].Update(rawDt);
            if (_activeVFX[i].IsExpired)
                _activeVFX.RemoveAt(i);
        }

        // Screen shake
        if (_shakeTimer > 0)
            _shakeTimer -= rawDt;
    }

    private void UpdateDeploy(float dt)
    {
        _startBattleButton.Update(Input);

        // Click on deployable squad to select
        if (Input.IsMouseClicked())
        {
            Vector2 mp = Input.MousePosition;

            // Check if clicking on a deployable squad in the tray
            for (int i = 0; i < _deployableSquads.Count; i++)
            {
                Rectangle trayRect = new Rectangle(30, GameSettings.ScreenHeight - 100, 80, 80);
                trayRect.X += i * 90;
                if (trayRect.Contains(mp.ToPoint()))
                {
                    _deployingSquadIndex = i;
                    return;
                }
            }

            // Place selected squad in deploy zone
            if (_deployingSquadIndex >= 0 && _deployZone.Contains(mp.ToPoint()))
            {
                var squad = _deployableSquads[_deployingSquadIndex];
                squad.Position = mp;
                squad.FacingDirection = 1;
                if (!_playerSquads.Contains(squad))
                {
                    _playerSquads.Add(squad);
                    _allSquads.Add(squad);
                    _prevHP[squad] = squad.HP;
                }
                _deployingSquadIndex = -1;
            }
        }
    }

    private void UpdateCountdown(float dt)
    {
        _countdownTimer -= dt;
        if (_countdownTimer <= 0)
        {
            _phase = BattlePhase.Fighting;
        }
    }

    private void UpdateFighting(float dt)
    {
        _battleTime += dt;

        // 更新Buff系统
        _buffSystem.Update(dt);

        // Update all squads
        foreach (var squad in _allSquads)
        {
            float prevHP = squad.HP;
            squad.Update(dt, _allSquads);

            // Update animation state
            string clipName = squad.State switch
            {
                SquadState.Idle => "Idle",
                SquadState.Moving => "Walk",
                SquadState.Engaging => "Attack",
                SquadState.UsingSkill => "Attack",
                SquadState.Fleeing => "Walk",
                SquadState.Dead => "Death",
                _ => "Idle"
            };
            squad.SoldierAnimator?.Play(clipName);
            squad.GeneralAnimator?.Play(clipName);
            squad.SoldierAnimator?.Update(dt);
            squad.GeneralAnimator?.Update(dt);

            // Generate damage numbers
            if (squad.HP < prevHP)
            {
                float dmg = prevHP - squad.HP;
                Color dmgColor = squad.Team == Team.Player ? Color.Red : Color.White;
                _floatingTexts.AddText($"-{(int)dmg}", squad.Position - new Vector2(0, 30), dmgColor);
                
                // Track damage dealt by attacker
                if (squad.TargetSquad != null && squad.TargetSquad.General != null)
                {
                    string attackerId = squad.TargetSquad.General.Id;
                    if (!_generalDamageDealt.ContainsKey(attackerId))
                        _generalDamageDealt[attackerId] = 0f;
                    _generalDamageDealt[attackerId] += dmg;
                }
            }

            // Check for death
            if (squad.IsDead && _prevHP.ContainsKey(squad) && _prevHP[squad] > 0)
            {
                if (squad.General != null)
                {
                    _moraleSystem.OnGeneralDeath(squad, _allSquads);
                    _floatingTexts.AddText($"{squad.General.Name} 阵亡!", squad.Position - new Vector2(0, 50), Color.Yellow);
                    _deathNotifications.AddNotification(squad.General.Name, squad.Team == Team.Player);
                    _shakeTimer = 0.5f;
                    _shakeIntensity = 8f;
                    
                    // Track kill for attacker
                    if (squad.TargetSquad != null && squad.TargetSquad.General != null)
                    {
                        string killerId = squad.TargetSquad.General.Id;
                        if (!_generalKillCounts.ContainsKey(killerId))
                            _generalKillCounts[killerId] = 0;
                        _generalKillCounts[killerId]++;
                    }
                }
            }
            _prevHP[squad] = squad.HP;
        }

        // Update morale
        _moraleSystem.Update(dt, _allSquads);

        // Update enemy AI
        _enemyAI.Update(dt, _allSquads);

        // Player skill input
        UpdatePlayerSkillInput();

        // Check win/lose
        bool playerAlive = _playerSquads.Any(s => s.IsActive);
        bool enemyAlive = _enemySquads.Any(s => s.IsActive);

        if (!enemyAlive)
        {
            _phase = BattlePhase.Result;
            _isVictory = true;
            _resultText = "胜 利 !";
            AwardBattleRewards();
            ShowResultPanel();
        }
        else if (!playerAlive)
        {
            _phase = BattlePhase.Result;
            _isVictory = false;
            _resultText = "败 北 ...";
            AwardBattleRewards();
            ShowResultPanel();
        }

        // Select player squad by clicking on battlefield
        if (Input.IsMouseClicked())
        {
            Vector2 mp = Input.MousePosition;
            // Only handle battlefield clicks (not bottom UI area)
            if (mp.Y < GameSettings.ScreenHeight - 95)
            {
                for (int i = 0; i < _playerSquads.Count; i++)
                {
                    if (_playerSquads[i].IsActive &&
                        Vector2.Distance(mp, _playerSquads[i].Position) < 40)
                    {
                        _selectedPlayerSquadIndex = i;
                        _uiManager.SkillPanel.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        // Pause
        if (Input.IsKeyPressed(Keys.Space))
        {
            // Simple pause - just skip this frame's update when unpaused next
        }
    }

    private void UpdatePlayerSkillInput()
    {
        // Quick skill hotkeys: 1, 2 for selected squad's skills
        if (_selectedPlayerSquadIndex >= 0 && _selectedPlayerSquadIndex < _playerSquads.Count)
        {
            var squad = _playerSquads[_selectedPlayerSquadIndex];
            if (Input.IsKeyPressed(Keys.D1) || Input.IsKeyPressed(Keys.NumPad1))
            {
                TryUseSkill(squad);
            }
        }
    }

    private void TryUseSkill(Squad squad)
    {
        if (squad.ActiveSkill == null || !squad.ActiveSkill.IsReady || !squad.IsActive) return;

        var targets = GetPlayerSkillTargets(squad);
        if (targets.Count > 0)
        {
            squad.UseSkill(targets);
            _floatingTexts.AddText(squad.ActiveSkill.Name, squad.Position - new Vector2(0, 45), new Color(255, 230, 100));
            AddSkillVFX(squad);
            _shakeTimer = 0.15f;
            _shakeIntensity = 3f;
            
            // Track skill usage
            if (squad.General != null)
            {
                string key = $"{squad.General.Id}:{squad.ActiveSkill.Id}";
                if (!_skillsUsedThisBattle.ContainsKey(key))
                    _skillsUsedThisBattle[key] = 0;
                _skillsUsedThisBattle[key]++;
            }
        }
    }

    private List<Squad> GetPlayerSkillTargets(Squad caster)
    {
        if (caster.ActiveSkill == null) return new();
        var skill = caster.ActiveSkill;
        var targets = new List<Squad>();

        switch (skill.TargetMode)
        {
            case SkillTargetMode.SingleTarget:
                if (caster.TargetSquad != null && !caster.TargetSquad.IsDead)
                    targets.Add(caster.TargetSquad);
                else
                {
                    var nearest = _enemySquads.Where(s => s.IsActive)
                        .OrderBy(s => Vector2.Distance(s.Position, caster.Position)).FirstOrDefault();
                    if (nearest != null) targets.Add(nearest);
                }
                break;
            case SkillTargetMode.AOE_Circle:
                var center = caster.TargetSquad?.Position ?? caster.Position;
                targets.AddRange(_enemySquads.Where(s => s.IsActive &&
                    Vector2.Distance(s.Position, center) <= skill.Radius));
                if (targets.Count == 0)
                    targets.AddRange(_enemySquads.Where(s => s.IsActive));
                break;
            case SkillTargetMode.Self:
                targets.AddRange(_playerSquads.Where(s => s.IsActive));
                break;
            case SkillTargetMode.AOE_Line:
                if (caster.TargetSquad != null)
                {
                    Vector2 dir = Vector2.Normalize(caster.TargetSquad.Position - caster.Position);
                    targets.AddRange(_enemySquads.Where(s => s.IsActive).Where(s =>
                    {
                        Vector2 toT = s.Position - caster.Position;
                        float dist = toT.Length();
                        if (dist < 1) return true;
                        float dot = Vector2.Dot(Vector2.Normalize(toT), dir);
                        return dot > 0.5f && dist < skill.Radius;
                    }));
                }
                if (targets.Count == 0)
                    targets.AddRange(_enemySquads.Where(s => s.IsActive));
                break;
        }
        return targets;
    }

    private void AddSkillVFX(Squad caster)
    {
        var skill = caster.ActiveSkill;
        if (skill == null) return;

        Color vfxColor = skill.EffectType switch
        {
            "damage" => new Color(255, 100, 50),
            "buff" => new Color(100, 200, 255),
            "morale" => new Color(200, 50, 50),
            _ => Color.White
        };

        _activeVFX.Add(new SkillVFX(caster.Position, skill.Radius > 0 ? skill.Radius : 60, vfxColor, 0.6f));
    }

    private void ShowResultPanel()
    {
        var xpMap = CalculateBattleXp();

        // 使用CaptureManager处理撤退和俘获
        var eventBus = new EventBus();
        var captureManager = new CaptureManager(eventBus);
        var capturedGenerals = new List<string>();

        if (_isVictory)
        {
            // 对每个撤退状态的敌方武将进行俘获判定
            foreach (var enemySquad in _enemySquads.Where(s => s.IsDead && s.General != null))
            {
                var pursuer = _playerSquads.FirstOrDefault(s => s.IsActive && s.General != null);
                if (pursuer != null && pursuer.General != null && enemySquad.General != null)
                {
                    bool retreated = captureManager.TryRetreat(enemySquad.General, pursuer.General);
                    if (!retreated)
                    {
                        bool captured = captureManager.TryCapture(enemySquad.General, pursuer.General);
                        if (captured)
                        {
                            capturedGenerals.Add(enemySquad.General.Id);
                        }
                    }
                }
            }
        }

        var resultData = new BattleResultData
        {
            IsVictory = _isVictory,
            PerformanceRating = _isVictory ? "B" : "D",
            BattleTime = _battleTime,
            PlayerLost = _playerSquads.Count(s => s.IsDead),
            EnemyLost = _enemySquads.Count(s => s.IsDead),
            TotalXp = xpMap.Values.Sum(),
            GoldReward = 0,
            FoodReward = 0,
            WoodReward = 0,
            IronReward = 0,
            MeritReward = 0,
            KeyEvents = new List<string>(),
            // 俘虏判定结果
            CapturedGenerals = capturedGenerals
        };
        _uiManager.ShowResult(resultData);
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(50, 45, 35));
        SpriteBatch.Begin();

        // Screen shake offset
        Vector2 shakeOffset = Vector2.Zero;
        if (_shakeTimer > 0)
        {
            shakeOffset = new Vector2(
                (float)(Random.Shared.NextDouble() - 0.5) * _shakeIntensity * 2,
                (float)(Random.Shared.NextDouble() - 0.5) * _shakeIntensity * 2
            );
        }

        // Draw battlefield background
        DrawBattlefield(shakeOffset);

        // Draw deploy zone during deploy phase
        if (_phase == BattlePhase.Deploy)
        {
            SpriteBatch.Draw(_pixel, _deployZone, new Color(50, 80, 120, 40));
            UIHelper.DrawBorder(SpriteBatch, _pixel, _deployZone, new Color(80, 120, 180, 100), 2);
            SpriteBatch.DrawString(_font, "部署区域 - 点击放置部队", new Vector2(_deployZone.X + 10, _deployZone.Y + 5),
                new Color(120, 160, 200));
        }

        // Draw all squads
        foreach (var squad in _allSquads)
        {
            if (!squad.IsDead)
                DrawSquad(squad, shakeOffset);
        }

        // Draw VFX
        foreach (var vfx in _activeVFX)
        {
            vfx.Draw(SpriteBatch, _pixel);
        }

        // Draw floating texts
        _floatingTexts.Draw(SpriteBatch, _font);

        // ===== 使用BattleUIManager绘制UI =====
        _uiManager.Draw(SpriteBatch, shakeOffset);

        // Phase-specific overlays (非UI管理器处理的部分)
        switch (_phase)
        {
            case BattlePhase.Deploy:
                DrawDeployUI();
                break;
            case BattlePhase.Countdown:
                DrawCountdown();
                break;
        }

        // Death notifications (on top of everything)
        _deathNotifications.Draw(SpriteBatch, _notifyFont, _smallFont, _pixel);

        // Fade overlay from SceneManager
        if (Game.SceneManager.IsFading)
        {
            SpriteBatch.Draw(_pixel,
                new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
                Color.Black * Game.SceneManager.FadeAlpha);
        }

        SpriteBatch.End();
    }

    private void DrawBattlefield(Vector2 offset)
    {
        // Ground gradient
        for (int y = 0; y < GameSettings.ScreenHeight; y += 4)
        {
            float t = (float)y / GameSettings.ScreenHeight;
            byte r = (byte)MathHelper.Lerp(55, 45, t);
            byte g = (byte)MathHelper.Lerp(50, 40, t);
            byte b = (byte)MathHelper.Lerp(40, 30, t);
            SpriteBatch.Draw(_pixel,
                new Rectangle((int)offset.X, y + (int)offset.Y, GameSettings.ScreenWidth, 4),
                new Color(r, g, b));
        }

        // Center divider line
        int cx = GameSettings.ScreenWidth / 2;
        SpriteBatch.Draw(_pixel, new Rectangle(cx + (int)offset.X, 50 + (int)offset.Y, 1, GameSettings.ScreenHeight - 100),
            new Color(80, 70, 50, 60));
    }

    private void DrawSquad(Squad squad, Vector2 offset)
    {
        Vector2 pos = squad.Position + offset;
        bool isSelected = squad.Team == Team.Player && _selectedPlayerSquadIndex >= 0 &&
            _selectedPlayerSquadIndex < _playerSquads.Count &&
            _playerSquads[_selectedPlayerSquadIndex] == squad;

        // --- Selection circle (pulsing) ---
        if (isSelected)
        {
            float pulse = 0.7f + 0.3f * MathF.Sin((float)_battleTime * 4f);
            DrawCircle(pos, 38, new Color(255, 220, 100) * (0.3f * pulse), 20);
            DrawCircleOutline(pos, 38, new Color(255, 220, 100) * (0.6f * pulse), 24);
        }

        // --- Attack line to target ---
        if (_phase == BattlePhase.Fighting && squad.TargetSquad != null && !squad.TargetSquad.IsDead &&
            squad.State == SquadState.Engaging)
        {
            Color lineColor = squad.Team == Team.Player
                ? new Color(100, 180, 255, 60)
                : new Color(255, 100, 100, 40);
            DrawLine(pos, squad.TargetSquad.Position + offset, lineColor, 1);
        }

        // --- Draw soldiers ---
        Color soldierColor = squad.Team == Team.Player
            ? new Color(60, 100, 180)
            : new Color(180, 60, 60);

        if (squad.State == SquadState.Fleeing)
            soldierColor = Color.Gray;

        SpriteEffects soldierFlip = squad.FacingDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        int visibleSoldiers = Math.Min(squad.SoldierCount, squad.SoldierOffsets.Count);

        if (squad.SoldierAnimator != null && squad.SoldierAnimator.HasTexture)
        {
            float soldierScale = squad.Formation == FormationType.Cavalry ? 0.10f : 0.08f;
            for (int i = 0; i < visibleSoldiers; i++)
            {
                Vector2 sPos = pos + squad.SoldierOffsets[i];
                squad.SoldierAnimator.Draw(SpriteBatch, sPos, soldierColor, soldierFlip, soldierScale);
            }
        }
        else
        {
            for (int i = 0; i < visibleSoldiers; i++)
            {
                Vector2 sPos = pos + squad.SoldierOffsets[i];
                int size = squad.Formation == FormationType.Cavalry ? 10 : 7;
                SpriteBatch.Draw(_pixel, new Rectangle((int)sPos.X - size / 2, (int)sPos.Y - size / 2, size, size), soldierColor);
            }
        }

        // --- Draw general (larger, with border) ---
        if (squad.General != null)
        {
            Color genColor = squad.Team == Team.Player
                ? new Color(80, 140, 220)
                : new Color(220, 80, 80);
            SpriteEffects genFlip = squad.FacingDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (squad.GeneralAnimator != null && squad.GeneralAnimator.HasTexture)
            {
                squad.GeneralAnimator.Draw(SpriteBatch, pos, genColor, genFlip, 0.15f);
            }
            else
            {
                Color genBorder = squad.Team == Team.Player
                    ? new Color(120, 180, 255)
                    : new Color(255, 120, 120);
                int genSize = 16;
                SpriteBatch.Draw(_pixel, new Rectangle((int)pos.X - genSize / 2 - 1, (int)pos.Y - genSize / 2 - 1, genSize + 2, genSize + 2), genBorder);
                SpriteBatch.Draw(_pixel, new Rectangle((int)pos.X - genSize / 2, (int)pos.Y - genSize / 2, genSize, genSize), genColor);
            }
        }

        // --- Name plate with background ---
        if (squad.General != null)
        {
            string name = squad.General.Name;
            Vector2 nameSize = _smallFont.MeasureString(name);
            float nameX = pos.X - nameSize.X / 2 - 4;
            float nameY = pos.Y - 58;

            Rectangle namePlate = new Rectangle((int)nameX, (int)nameY, (int)nameSize.X + 8, (int)nameSize.Y + 2);
            Color plateBg = squad.Team == Team.Player
                ? new Color(30, 50, 80, 180)
                : new Color(80, 30, 30, 180);
            SpriteBatch.Draw(_pixel, namePlate, plateBg);

            Color accentColor = squad.Team == Team.Player
                ? new Color(80, 140, 220)
                : new Color(220, 80, 80);
            SpriteBatch.Draw(_pixel, new Rectangle(namePlate.X, namePlate.Bottom - 1, namePlate.Width, 1), accentColor);

            SpriteBatch.DrawString(_smallFont, name, new Vector2(nameX + 4, nameY + 1), new Color(240, 220, 170));
        }

        // --- Formation type indicator ---
        string formIcon = squad.Formation switch
        {
            FormationType.Vanguard => "盾",
            FormationType.Archer => "弓",
            FormationType.Cavalry => "骑",
            _ => "?"
        };
        SpriteBatch.DrawString(_smallFont, formIcon, pos + new Vector2(-6, 18), new Color(200, 180, 130) * 0.8f);

        // --- Enhanced HP bar ---
        float hpRatio = squad.MaxHP > 0 ? Math.Clamp(squad.HP / squad.MaxHP, 0, 1) : 0;
        int barW = 60;
        int barH = 7;
        int barX = (int)(pos.X - barW / 2);
        int barY = (int)(pos.Y - 45);

        // Outer border
        SpriteBatch.Draw(_pixel, new Rectangle(barX - 1, barY - 1, barW + 2, barH + 2), new Color(60, 50, 40));
        // Background
        SpriteBatch.Draw(_pixel, new Rectangle(barX, barY, barW, barH), new Color(20, 15, 10));

        // HP fill with layered effect
        Color hpColor = hpRatio > 0.6f ? new Color(50, 180, 50)
                       : hpRatio > 0.3f ? new Color(220, 180, 30)
                       : new Color(200, 40, 40);
        Color hpColorBright = hpRatio > 0.6f ? new Color(80, 220, 80)
                             : hpRatio > 0.3f ? new Color(255, 220, 60)
                             : new Color(255, 70, 70);

        int fillW = (int)(barW * hpRatio);
        if (fillW > 0)
        {
            SpriteBatch.Draw(_pixel, new Rectangle(barX, barY, fillW, barH), hpColor);
            SpriteBatch.Draw(_pixel, new Rectangle(barX, barY, fillW, 2), hpColorBright * 0.6f);
            SpriteBatch.Draw(_pixel, new Rectangle(barX, barY + barH - 1, fillW, 1), new Color(0, 0, 0, 60));
        }

        // HP text
        string hpText = $"{(int)squad.HP}/{(int)squad.MaxHP}";
        Vector2 hpTextSize = _smallFont.MeasureString(hpText);
        SpriteBatch.DrawString(_smallFont, hpText,
            new Vector2(barX + (barW - hpTextSize.X) / 2, barY - 13),
            new Color(200, 190, 160) * 0.9f);

        // --- Morale bar ---
        float moraleRatio = Math.Clamp(squad.Morale / 100f, 0, 1);
        int mBarY = barY + barH + 2;
        int mBarH = 3;
        SpriteBatch.Draw(_pixel, new Rectangle(barX, mBarY, barW, mBarH), new Color(15, 12, 8));
        Color moraleColor = moraleRatio > 0.7f ? new Color(70, 120, 210)
                           : moraleRatio > 0.4f ? new Color(210, 160, 40)
                           : new Color(180, 50, 50);
        int mFillW = (int)(barW * moraleRatio);
        if (mFillW > 0)
            SpriteBatch.Draw(_pixel, new Rectangle(barX, mBarY, mFillW, mBarH), moraleColor);

        // --- Buff/State indicators ---
        if (squad.BuffTimer > 0)
        {
            Color buffColor = squad.AttackBuffPercent > 0 ? new Color(255, 100, 50) :
                              squad.DefenseBuffPercent > 0 ? new Color(50, 150, 255) :
                              new Color(100, 255, 100);
            SpriteBatch.Draw(_pixel, new Rectangle(barX + barW + 3, barY, 5, 5), buffColor);
        }

        if (squad.State == SquadState.Fleeing)
        {
            SpriteBatch.DrawString(_smallFont, "溃", pos + new Vector2(-6, -60), new Color(255, 80, 80));
        }
    }

    private void DrawCircle(Vector2 center, float radius, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float nextAngle = MathHelper.TwoPi * (i + 1) / segments;
            float x1 = center.X + MathF.Cos(angle) * radius;
            float y1 = center.Y + MathF.Sin(angle) * radius * 0.5f;
            float x2 = center.X + MathF.Cos(nextAngle) * radius;
            float y2 = center.Y + MathF.Sin(nextAngle) * radius * 0.5f;
            DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), color, 2);
        }
    }

    private void DrawCircleOutline(Vector2 center, float radius, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float x = center.X + MathF.Cos(angle) * radius;
            float y = center.Y + MathF.Sin(angle) * radius * 0.5f;
            SpriteBatch.Draw(_pixel, new Rectangle((int)x - 1, (int)y - 1, 3, 3), color);
        }
    }

    private void DrawLine(Vector2 start, Vector2 end, Color color, int thickness)
    {
        Vector2 diff = end - start;
        float length = diff.Length();
        if (length < 1) return;
        float angle = MathF.Atan2(diff.Y, diff.X);
        SpriteBatch.Draw(_pixel, start, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0);
    }

    private void DrawDeployUI()
    {
        // Bottom tray
        SpriteBatch.Draw(_pixel, new Rectangle(0, GameSettings.ScreenHeight - 120, GameSettings.ScreenWidth, 120),
            new Color(30, 25, 18, 200));

        SpriteBatch.DrawString(_font, "点击武将 → 点击部署区域放置 (或直接开战用默认位置)",
            new Vector2(15, GameSettings.ScreenHeight - 115), new Color(160, 140, 100));

        for (int i = 0; i < _deployableSquads.Count; i++)
        {
            var sq = _deployableSquads[i];
            Rectangle trayRect = new Rectangle(30 + i * 90, GameSettings.ScreenHeight - 90, 80, 75);

            Color bg = _deployingSquadIndex == i ? new Color(80, 60, 40) : new Color(50, 40, 30);
            SpriteBatch.Draw(_pixel, trayRect, bg);
            UIHelper.DrawBorder(SpriteBatch, _pixel, trayRect, _deployingSquadIndex == i ? new Color(220, 180, 80) : new Color(100, 80, 60), 2);

            string name = sq.General?.Name ?? "?";
            string form = sq.Formation switch
            {
                FormationType.Vanguard => "盾兵",
                FormationType.Archer => "弓兵",
                FormationType.Cavalry => "骑兵",
                _ => "?"
            };
            SpriteBatch.DrawString(_font, name, new Vector2(trayRect.X + 5, trayRect.Y + 5), new Color(220, 190, 130));
            SpriteBatch.DrawString(_font, form, new Vector2(trayRect.X + 5, trayRect.Y + 30), new Color(150, 130, 100));

            bool deployed = _playerSquads.Contains(sq);
            if (deployed)
            {
                SpriteBatch.DrawString(_font, "已部署", new Vector2(trayRect.X + 5, trayRect.Y + 55), Color.LimeGreen);
            }
        }

        _startBattleButton.Draw(SpriteBatch, _font, _pixel);
    }

    private void DrawCountdown()
    {
        string countText = _countdownTimer > 2 ? "3" : _countdownTimer > 1 ? "2" : "1";
        Vector2 ts = _titleFont.MeasureString(countText);
        Vector2 pos = new Vector2(GameSettings.ScreenWidth / 2 - ts.X / 2, GameSettings.ScreenHeight / 2 - ts.Y / 2);

        // Semi-transparent backdrop
        SpriteBatch.Draw(_pixel,
            new Rectangle(GameSettings.ScreenWidth / 2 - 60, GameSettings.ScreenHeight / 2 - 50, 120, 100),
            new Color(0, 0, 0, 150));

        SpriteBatch.DrawString(_titleFont, countText, pos, new Color(255, 220, 100));
    }

    // ==================== 战斗经验计算 ====================
    
    private void AwardBattleRewards()
    {
        var xpMap = CalculateBattleXp();
        
        // Award XP
        foreach (var kvp in xpMap)
        {
            GameState.Instance.AddGeneralExperience(kvp.Key, kvp.Value);
        }
        
        // Award skill points (1-3 based on performance)
        int spAward = _isVictory ? 2 : 1;
        int totalKills = _generalKillCounts.Values.Sum();
        if (_isVictory && totalKills >= 3) spAward = 3;
        
        foreach (var squad in _playerSquads.Where(s => s.General != null && !s.IsDead))
        {
            GameState.Instance.AddSkillPoints(squad.General!.Id, spAward);
        }
        
        // Award skill XP
        foreach (var kvp in _skillsUsedThisBattle)
        {
            var parts = kvp.Key.Split(':');
            if (parts.Length == 2)
            {
                string generalId = parts[0];
                string skillId = parts[1];
                int usageCount = kvp.Value;
                int skillXp = usageCount * 10;
                
                var progress = GameState.Instance.GetGeneralProgress(generalId);
                if (progress != null)
                {
                    if (!progress.SkillLevels.ContainsKey(skillId))
                        progress.SkillLevels[skillId] = 1;
                    progress.SkillLevels[skillId] += (skillXp + 29) / 30; // 向上取整
                }
            }
        }
        
        GameState.Instance.Save();
    }
    
    private Dictionary<string, int> CalculateBattleXp()
    {
        var results = new Dictionary<string, int>();
        
        if (!_isVictory)
        {
            foreach (var squad in _playerSquads.Where(s => s.General != null && !s.IsDead))
                results[squad.General!.Id] = 50;
            return results;
        }
        
        int baseXp = 200;
        int killXp = _generalKillCounts.Values.Sum() * 50;
        
        float totalEnemyMaxHP = _enemySquads.Sum(s => s.MaxHP);
        float totalDamage = _enemySquads.Sum(s => Math.Max(0, s.MaxHP - s.HP));
        float damageRatio = totalEnemyMaxHP > 0 ? totalDamage / totalEnemyMaxHP : 0;
        int damageXp = (int)(damageRatio * 100);
        
        int survivalXp = _playerSquads.Count(s => !s.IsDead) * 30;
        
        int totalXp = baseXp + killXp + damageXp + survivalXp;
        int activeGenerals = _playerSquads.Count(s => s.General != null);
        
        if (activeGenerals > 0)
        {
            int xpPerGeneral = totalXp / activeGenerals;
            foreach (var squad in _playerSquads.Where(s => s.General != null))
                results[squad.General!.Id] = xpPerGeneral;
        }
        
        return results;
    }
}

// Simple VFX class for skill effects
public class SkillVFX
{
    public Vector2 Position { get; }
    public float Radius { get; }
    public Color BaseColor { get; }
    public float Life { get; private set; }
    public float MaxLife { get; }

    public bool IsExpired => Life <= 0;

    public SkillVFX(Vector2 position, float radius, Color color, float duration)
    {
        Position = position;
        Radius = radius;
        BaseColor = color;
        Life = duration;
        MaxLife = duration;
    }

    public void Update(float dt) => Life -= dt;

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        float progress = 1f - (Life / MaxLife);
        float currentRadius = Radius * progress;
        float alpha = (1f - progress) * 0.5f;

        // Draw expanding ring as series of rectangles
        int segments = 24;
        for (int i = 0; i < segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            float x = Position.X + MathF.Cos(angle) * currentRadius;
            float y = Position.Y + MathF.Sin(angle) * currentRadius;
            spriteBatch.Draw(pixel, new Rectangle((int)x - 2, (int)y - 2, 4, 4), BaseColor * alpha);
        }
    }
}
