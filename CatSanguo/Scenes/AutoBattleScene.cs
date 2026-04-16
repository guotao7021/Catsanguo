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
using CatSanguo.WorldMap;

namespace CatSanguo.Scenes;

public class AutoBattleResult
{
    public bool IsVictory { get; set; }
    public float BattleTime { get; set; }
    public int PlayerLost { get; set; }
    public int EnemyLost { get; set; }
    public Dictionary<string, int> XpGained { get; set; } = new();

    // 资源奖励
    public int GoldReward { get; set; }
    public int FoodReward { get; set; }
    public int WoodReward { get; set; }
    public int IronReward { get; set; }
    public int MeritReward { get; set; }

    // 战斗评价
    public string PerformanceRating { get; set; } = ""; // S/A/B/C

    // 俘虏武将（战斗胜利后俘获的敌方武将）
    public List<string> CapturedGenerals { get; set; } = new();

    // 战后幸存兵力：generalId -> survivingSoldierCount
    public Dictionary<string, int> SurvivingSoldiers { get; set; } = new();
}

public enum AutoBattlePhase
{
    Fighting,
    Result
}

public class AutoBattleScene : Scene
{
    // Input data
    private readonly ArmyToken _playerArmy;
    private readonly CityData _targetCity;
    private readonly List<GeneralData> _allGeneralData;
    private readonly Action<AutoBattleResult>? _onComplete;

    // Data
    private List<SkillData> _allSkillData = new();

    // Battle state
    private AutoBattlePhase _phase = AutoBattlePhase.Fighting;
    private List<Squad> _allSquads = new();
    private List<Squad> _playerSquads = new();
    private List<Squad> _enemySquads = new();
    private MoraleSystem _moraleSystem = new();
    private BattleAI _playerAI;
    private BattleAI _enemyAI;
    private BattleEventLog _eventLog = new();
    private float _battleTime;
    private float _speedMultiplier = 4f;
    private bool _isVictory;
    private string _resultText = "";
    private float _garrisonDefenseBonus;

    // Tracking
    private Dictionary<string, int> _generalKillCounts = new();
    private Dictionary<string, float> _generalDamageDealt = new();
    private Dictionary<string, int> _skillsUsedThisBattle = new();
    private Dictionary<Squad, float> _prevHP = new();

    // ===== 核心战斗三件套 =====
    private EventBus _eventBus = null!;
    private BuffSystem _buffSystem = null!;
    private SkillTriggerSystem _skillTriggerSystem = null!;
    private BattleContext _battleContext = null!;

    // ===== 战斗表现系统 =====
    private CombatPresentationSystem _presentationSystem = null!;

    // UI
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private SpriteFontBase _smallFont;
    private BattleUIManager _uiManager = null!;

    // VFX
    private FloatingTextManager _floatingTexts = new();
    private DeathNotificationManager _deathNotifications = new();
    private float _shakeTimer;
    private float _shakeIntensity;

    public AutoBattleScene(ArmyToken playerArmy, CityData targetCity,
        List<GeneralData> allGeneralData, Action<AutoBattleResult>? onComplete = null)
    {
        _playerArmy = playerArmy;
        _targetCity = targetCity;
        _allGeneralData = allGeneralData;
        _onComplete = onComplete;
    }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;
        _smallFont = Game.SmallFont;

        // Load data
        string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _allSkillData = DataLoader.LoadList<SkillData>(Path.Combine(dataPath, "skills.json"));
        var formations = DataLoader.LoadList<FormationData>(Path.Combine(dataPath, "formations.json"));

        _garrisonDefenseBonus = _targetCity.GarrisonDefenseBonus;

        // 初始化战斗表现系统
        _presentationSystem = new CombatPresentationSystem();
        _presentationSystem.Initialize(_pixel, _font, _titleFont);

        // Create player squads (auto-placed on left)
        int slotY = 200;
        foreach (string genId in _playerArmy.GeneralIds)
        {
            var genData = _allGeneralData.FirstOrDefault(g => g.Id == genId);
            if (genData == null) continue;

            // 获取该武将的出征配置
            var deployConfig = _playerArmy.GetDeployConfig(genId);

            var squad = CreateSquad(genData, Team.Player, formations, deployConfig);
            squad.Position = new Vector2(150, slotY);
            squad.FacingDirection = 1;
            _playerSquads.Add(squad);
            _allSquads.Add(squad);
            _prevHP[squad] = squad.HP;
            slotY += 120;
        }

        // Create enemy squads (auto-placed on right)
        slotY = 200;
        foreach (var es in _targetCity.Garrison)
        {
            var genData = _allGeneralData.FirstOrDefault(g => g.Id == es.GeneralId);
            if (genData == null) continue;

            var squad = CreateSquad(genData, Team.Enemy, formations);
            squad.Position = new Vector2(GameSettings.ScreenWidth - 150, slotY);
            squad.FacingDirection = -1;
            squad.SoldierCount = es.SoldierCount;
            squad.MaxSoldierCount = es.SoldierCount;

            if (_garrisonDefenseBonus > 0f)
            {
                squad.Attributes.AddModifier(new Modifier("garrison", AttrType.Defense, ModifierOp.Multiply, _garrisonDefenseBonus));
            }

            // Pass (关隘) cities grant extra +50% defense bonus
            if (_targetCity.CityType == "pass")
            {
                squad.Attributes.AddModifier(new Modifier("pass", AttrType.Defense, ModifierOp.Multiply, 0.50f));
            }

            squad.InitializeSoldierOffsets();
            _enemySquads.Add(squad);
            _allSquads.Add(squad);
            _prevHP[squad] = squad.HP;
            slotY += 120;
        }

        // Create AI for both sides
        int difficulty = Math.Max(1, _targetCity.DefenseLevel / 2);
        _playerAI = new BattleAI(Team.Player, 2);
        _enemyAI = new BattleAI(Team.Enemy, difficulty);

        // ===== 初始化战斗UI管理器 =====
        _uiManager = new BattleUIManager(BattleUIMode.Auto);
        _uiManager.Initialize(_pixel, _font, _titleFont, _smallFont);
        _uiManager.SetSquadLists(_playerSquads, _enemySquads);
        _uiManager.HUD.StageName = $"进攻: {_targetCity.Name}";
        _uiManager.HUD.OnSpeedToggled = ToggleSpeed;
        _uiManager.HUD.OnSkipClicked = SkipBattle;
        _uiManager.ResultPanel.OnContinue = OnContinue;

        _eventLog.Add(0, $"进攻 {_targetCity.Name}!", BattleEventType.BattleEnd);

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

        // 初始化所有Squad的Attributes和Context
        foreach (var squad in _allSquads)
        {
            squad.Context = _battleContext;
        }

        // 注册所有Squad到技能触发系统并开始战斗
        foreach (var squad in _allSquads)
        {
            _skillTriggerSystem.RegisterSquad(squad);
        }
        _skillTriggerSystem.Initialize();
        _eventBus.Publish(new GameEvent(GameEventType.OnBattleStart));

        _phase = AutoBattlePhase.Fighting;
    }

    private Squad CreateSquad(GeneralData genData, Team team, List<FormationData> formations, GeneralDeployEntry? deployConfig = null)
    {
        var general = General.FromData(genData);

        // 使用配置或默认值
        BattleFormation battleFormation = deployConfig?.BattleFormation ?? BattleFormation.Vanguard;
        int soldierCount = deployConfig?.SoldierCount ?? 30;

        FormationType ft = Enum.TryParse<FormationType>(battleFormation.ToString(), true, out var parsed)
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
            SoldierCount = soldierCount,
            MaxSoldierCount = soldierCount,
            Morale = 100,
            // 初始化AttributeSet
            Attributes = new AttributeSet()
        };

        // 设置属性基础值
        squad.Attributes.SetBase(AttrType.MaxHP, baseHP + general.Leadership * 5);
        squad.Attributes.SetBase(AttrType.Attack, baseAtk);
        squad.Attributes.SetBase(AttrType.Defense, baseDef);
        squad.Attributes.SetBase(AttrType.Speed, baseSpd * 100);
        squad.Attributes.SetBase(AttrType.CritRate, 5);
        squad.Attributes.SetBase(AttrType.CritDamage, 150);
        squad.Attributes.SetBase(AttrType.AttackRange, atkRange);

        // 添加General属性加成
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_str", AttrType.Attack, ModifierOp.Add, general.Strength * 2));
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_lvl", AttrType.Defense, ModifierOp.Add, general.Leadership * 1.5f));
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_spd", AttrType.Speed, ModifierOp.Add, general.Speed * 1.2f));

        var activeSkillData = _allSkillData.FirstOrDefault(s => s.Id == genData.ActiveSkillId);
        if (activeSkillData != null)
            squad.ActiveSkill = Skill.FromData(activeSkillData);

        var passiveSkillData = _allSkillData.FirstOrDefault(s => s.Id == genData.PassiveSkillId);
        if (passiveSkillData != null)
        {
            squad.PassiveSkill = Skill.FromData(passiveSkillData);
            // 使用新属性系统应用被动技能
            if (passiveSkillData.EffectType == "buff")
            {
                if (passiveSkillData.BuffStat == "attack")
                {
                    squad.Attributes.AddModifier(new Modifier($"passive_{passiveSkillData.Id}", AttrType.Attack, ModifierOp.Multiply, passiveSkillData.BuffPercent));
                    squad.AttackBuffPercent += passiveSkillData.BuffPercent;
                }
                else if (passiveSkillData.BuffStat == "defense")
                {
                    squad.Attributes.AddModifier(new Modifier($"passive_{passiveSkillData.Id}", AttrType.Defense, ModifierOp.Multiply, passiveSkillData.BuffPercent));
                    squad.DefenseBuffPercent += passiveSkillData.BuffPercent;
                }
            }
        }

        squad.InitializeSoldierOffsets();

        string soldierKey = "soldier_" + squad.Formation.ToString().ToLower();
        squad.SoldierAnimator = Game.SpriteSheets.CreateAnimator(soldierKey);
        squad.GeneralAnimator = Game.SpriteSheets.CreateAnimator("general_default");

        return squad;
    }

    private void ToggleSpeed()
    {
        _speedMultiplier = _speedMultiplier switch
        {
            2f => 4f,
            4f => 8f,
            _ => 2f
        };
    }

    private void SkipBattle()
    {
        // Simulate the rest of the battle instantly
        for (int i = 0; i < 2000; i++)
        {
            if (_phase != AutoBattlePhase.Fighting) break;
            UpdateFighting(0.05f);
        }
    }

    public override void Update(GameTime gameTime)
    {
        float rawDt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // 获取表现系统的时间缩放
        float timeScale = _presentationSystem?.GetTimeScale() ?? 1.0f;
        float scaledDt = rawDt * _speedMultiplier * timeScale;

        switch (_phase)
        {
            case AutoBattlePhase.Fighting:
                UpdateFighting(scaledDt);
                break;
            case AutoBattlePhase.Result:
                // Escape key in result phase triggers continue
                if (Input.IsKeyPressed(Keys.Escape))
                {
                    OnContinue();
                }
                break;
        }

        // 更新UI管理器
        bool paused = false;
        _uiManager.Update(rawDt, Input, _battleTime, _speedMultiplier, paused);

        _floatingTexts.Update(rawDt);
        _deathNotifications.Update(rawDt);

        // 更新表现系统
        _presentationSystem?.Update(rawDt);

        if (_shakeTimer > 0)
            _shakeTimer -= rawDt;
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

            // Update animations
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

            // Track damage and send combat events to presentation system
            if (squad.HP < prevHP)
            {
                float dmg = prevHP - squad.HP;
                string attackerId = squad.TargetSquad?.General?.Id ?? "unknown";

                // 记录总伤害
                if (!_generalDamageDealt.ContainsKey(attackerId))
                    _generalDamageDealt[attackerId] = 0f;
                _generalDamageDealt[attackerId] += dmg;

                // 发送战斗表现事件 - 伤害飘字和屏幕震动
                if (squad.TargetSquad != null)
                {
                    // 添加飘字效果
                    Color hitColor = squad.Team == Team.Player ? Color.LightCoral : Color.LightBlue;
                    _presentationSystem?.AddFloatingText($"{(int)dmg}", squad.Position, hitColor, 1.0f, false);

                    // 触发屏幕震动
                    _presentationSystem?.TriggerScreenShake(5f, 0.15f);
                }
            }

            // Check for death
            if (squad.IsDead && _prevHP.ContainsKey(squad) && _prevHP[squad] > 0)
            {
                if (squad.General != null)
                {
                    _moraleSystem.OnGeneralDeath(squad, _allSquads);
                    string deathMsg = $"{squad.General.Name} 阵亡!";
                    _eventLog.Add(_battleTime, deathMsg, BattleEventType.GeneralDeath);
                    _floatingTexts.AddText(deathMsg, squad.Position - new Vector2(0, 50), Color.Yellow);
                    _deathNotifications.AddNotification(squad.General.Name, squad.Team == Team.Player);
                    _shakeTimer = 0.5f;
                    _shakeIntensity = 8f;

                    // 发送死亡事件到表现系统
                    _presentationSystem?.TriggerDeathEffect(squad.Position);
                    _presentationSystem?.AddFloatingText("阵亡!", squad.Position - new Vector2(0, 30), Color.Red, 1.2f, false);

                    if (squad.TargetSquad?.General != null)
                    {
                        string killerId = squad.TargetSquad.General.Id;
                        if (!_generalKillCounts.ContainsKey(killerId))
                            _generalKillCounts[killerId] = 0;
                        _generalKillCounts[killerId]++;
                    }
                }
            }

            // Track morale break
            if (squad.State == SquadState.Fleeing && _prevHP.ContainsKey(squad) && _prevHP[squad] > 0 && squad.General != null)
            {
                if (squad.Morale < 20 && _prevHP[squad] == prevHP) // just started fleeing
                {
                    _eventLog.Add(_battleTime, $"{squad.General.Name} 溃败!", BattleEventType.MoraleBreak);
                }
            }

            _prevHP[squad] = squad.HP;
        }

        // Morale system
        _moraleSystem.Update(dt, _allSquads);

        // Both AIs
        _playerAI.Update(dt, _allSquads);
        _enemyAI.Update(dt, _allSquads);

        // Track skill usage from AI
        foreach (var squad in _allSquads.Where(s => s.IsActive && s.State == SquadState.UsingSkill && s.ActiveSkill != null && s.General != null))
        {
            string key = $"{squad.General!.Id}:{squad.ActiveSkill!.Id}";
            if (!_skillsUsedThisBattle.ContainsKey(key))
            {
                _skillsUsedThisBattle[key] = 0;
                string skillMsg = $"{squad.General.Name} 释放 {squad.ActiveSkill.Name}!";
                _eventLog.Add(_battleTime, skillMsg, BattleEventType.SkillUsed);
                _floatingTexts.AddText(squad.ActiveSkill.Name, squad.Position - new Vector2(0, 45), new Color(255, 230, 100));

                // 发送技能事件到表现系统
                _presentationSystem?.AddFloatingText(squad.ActiveSkill.Name, squad.Position - new Vector2(0, 60), new Color(255, 200, 100), 1.3f, false);
                _presentationSystem?.TriggerSkillEffect(squad.Position);
                _presentationSystem?.TriggerSlowMotion(0.3f, 0.8f);
            }
            _skillsUsedThisBattle[key]++;
        }

        // Check win/lose
        bool playerAlive = _playerSquads.Any(s => s.IsActive);
        bool enemyAlive = _enemySquads.Any(s => s.IsActive);

        if (!enemyAlive)
        {
            EndBattle(true);
        }
        else if (!playerAlive)
        {
            EndBattle(false);
        }
    }

    private void EndBattle(bool victory)
    {
        _phase = AutoBattlePhase.Result;
        _isVictory = victory;
        _resultText = victory ? "胜 利 !" : "败 北 ...";
        _eventLog.Add(_battleTime, _resultText, BattleEventType.BattleEnd);
        AwardBattleRewards();

        // 构建结算数据并显示UI
        var (gold, food, wood, iron, merit, rating) = CalculateRewards();
        var xpMap = CalculateBattleXp();
        var keyEvents = _eventLog.Events
            .Where(e => e.Type == BattleEventType.GeneralDeath || e.Type == BattleEventType.SkillUsed)
            .Select(e => e.Description).Take(3).ToList();

        var resultData = new BattleResultData
        {
            IsVictory = _isVictory,
            PerformanceRating = rating,
            BattleTime = _battleTime,
            PlayerLost = _playerSquads.Count(s => s.IsDead),
            EnemyLost = _enemySquads.Count(s => s.IsDead),
            TotalXp = xpMap.Values.Sum(),
            GoldReward = gold,
            FoodReward = food,
            WoodReward = wood,
            IronReward = iron,
            MeritReward = merit,
            KeyEvents = keyEvents
        };
        _uiManager.ShowResult(resultData);
    }

    private void AwardBattleRewards()
    {
        var xpMap = CalculateBattleXp();

        foreach (var kvp in xpMap)
            GameState.Instance.AddGeneralExperience(kvp.Key, kvp.Value);

        int spAward = _isVictory ? 2 : 1;
        int totalKills = _generalKillCounts.Values.Sum();
        if (_isVictory && totalKills >= 3) spAward = 3;

        foreach (var squad in _playerSquads.Where(s => s.General != null && !s.IsDead))
            GameState.Instance.AddSkillPoints(squad.General!.Id, spAward);

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
                    progress.SkillLevels[skillId] += (skillXp + 29) / 30;
                }
            }
        }

        GameState.Instance.Save();
    }

    // ==================== 奖励计算系统 ====================
    private (int gold, int food, int wood, int iron, int merit, string rating) CalculateRewards()
    {
        int baseGold = 100;
        int baseFood = 50;
        int baseWood = 30;
        int baseIron = 20;
        int baseMerit = 50;

        if (!_isVictory)
        {
            // 失败获得部分奖励
            return (baseGold / 2, baseFood / 2, baseWood / 2, baseIron / 2, baseMerit / 3, "D");
        }

        // 计算难度系数（基于敌人数量）
        float difficultyMultiplier = 1.0f + (_enemySquads.Count - 1) * 0.2f;

        // 计算表现评分
        string rating;
        float performanceMultiplier;

        int playerSurvivors = _playerSquads.Count(s => !s.IsDead);
        int playerTotal = _playerSquads.Count;
        float survivalRate = playerTotal > 0 ? (float)playerSurvivors / playerTotal : 0;

        int enemyKills = _enemySquads.Count(s => s.IsDead);
        int enemyTotal = _enemySquads.Count;
        float killRate = enemyTotal > 0 ? (float)enemyKills / enemyTotal : 0;

        if (survivalRate >= 1.0f && killRate >= 1.0f)
        {
            rating = "S";
            performanceMultiplier = 2.0f;
        }
        else if (survivalRate >= 0.75f && killRate >= 0.75f)
        {
            rating = "A";
            performanceMultiplier = 1.5f;
        }
        else if (survivalRate >= 0.5f && killRate >= 0.5f)
        {
            rating = "B";
            performanceMultiplier = 1.2f;
        }
        else if (killRate >= 0.3f)
        {
            rating = "C";
            performanceMultiplier = 1.0f;
        }
        else
        {
            rating = "D";
            performanceMultiplier = 0.8f;
        }

        // 城池规模加成
        float cityScaleBonus = _targetCity.CityScale switch
        {
            "huge" => 2.0f,
            "large" => 1.5f,
            "medium" => 1.0f,
            "small" => 0.7f,
            _ => 1.0f
        };

        // 计算最终奖励
        float finalMultiplier = difficultyMultiplier * performanceMultiplier * cityScaleBonus;

        int gold = (int)(baseGold * finalMultiplier);
        int food = (int)(baseFood * finalMultiplier);
        int wood = (int)(baseWood * finalMultiplier);
        int iron = (int)(baseIron * finalMultiplier);
        int merit = (int)(baseMerit * finalMultiplier);

        // 歼灭加成
        if (killRate >= 1.0f)
        {
            gold = (int)(gold * 1.5f);
            merit += 30;
        }

        return (gold, food, wood, iron, merit, rating);
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

    private void OnContinue()
    {
        var (gold, food, wood, iron, merit, rating) = CalculateRewards();

        // 使用CaptureManager处理撤退和俘获
        var eventBus = new EventBus();
        var captureManager = new CaptureManager(eventBus);
        var capturedGenerals = new List<string>();

        if (_isVictory)
        {
            // 对每个撤退状态的敌方武将进行俘获判定
            foreach (var enemySquad in _enemySquads.Where(s => s.IsDead && s.General != null))
            {
                // 寻找最近的玩家武将作为追击者
                var pursuer = _playerSquads.FirstOrDefault(s => s.IsActive && s.General != null);
                if (pursuer != null && pursuer.General != null && enemySquad.General != null)
                {
                    // 先判定撤退
                    bool retreated = captureManager.TryRetreat(enemySquad.General, pursuer.General);
                    if (!retreated)
                    {
                        // 撤退失败，判定俘获
                        bool captured = captureManager.TryCapture(enemySquad.General, pursuer.General);
                        if (captured)
                        {
                            capturedGenerals.Add(enemySquad.General.Id);
                        }
                    }
                }
            }
        }

        var result = new AutoBattleResult
        {
            IsVictory = _isVictory,
            BattleTime = _battleTime,
            PlayerLost = _playerSquads.Count(s => s.IsDead),
            EnemyLost = _enemySquads.Count(s => s.IsDead),
            XpGained = CalculateBattleXp(),
            GoldReward = gold,
            FoodReward = food,
            WoodReward = wood,
            IronReward = iron,
            MeritReward = merit,
            PerformanceRating = rating,
            // 俘虏判定结果
            CapturedGenerals = capturedGenerals,
            // 战后幸存兵力
            SurvivingSoldiers = _playerSquads
                .Where(s => s.General != null && !s.IsDead)
                .ToDictionary(s => s.General!.Id, s => s.SoldierCount)
        };
        _onComplete?.Invoke(result);
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(50, 45, 35));
        SpriteBatch.Begin();

        // Screen shake offset (从表现系统获取)
        Vector2 shakeOffset = _presentationSystem?.GetScreenShakeOffset() ?? Vector2.Zero;
        if (_shakeTimer > 0 && shakeOffset == Vector2.Zero)
        {
            shakeOffset = new Vector2(
                (float)(Random.Shared.NextDouble() - 0.5) * _shakeIntensity * 2,
                (float)(Random.Shared.NextDouble() - 0.5) * _shakeIntensity * 2
            );
        }

        // Background gradient
        DrawBattlefield(shakeOffset);

        // Draw squads
        foreach (var squad in _allSquads.Where(s => !s.IsDead))
            DrawSquadSimple(squad, shakeOffset);

        // Floating texts
        _floatingTexts.Draw(SpriteBatch, _font);

        // 绘制战斗表现系统 (VFX, 连击等)
        _presentationSystem?.Draw(SpriteBatch);

        // ===== 使用BattleUIManager绘制UI =====
        _uiManager.Draw(SpriteBatch, shakeOffset);

        // 战况日志面板（场景特有）
        if (_phase == AutoBattlePhase.Fighting)
        {
            DrawEventLog();
        }

        // Death notifications
        _deathNotifications.Draw(SpriteBatch, Game.NotifyFont, _smallFont, _pixel);

        // Fade overlay
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
    }

    private void DrawSquadSimple(Squad squad, Vector2 offset)
    {
        Vector2 pos = squad.Position + offset;
        Color soldierColor = squad.Team == Team.Player ? new Color(60, 100, 180) : new Color(180, 60, 60);
        if (squad.State == SquadState.Fleeing) soldierColor = Color.Gray;

        SpriteEffects flip = squad.FacingDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        int visibleSoldiers = Math.Min(squad.SoldierCount, squad.SoldierOffsets.Count);

        // Draw soldiers
        if (squad.SoldierAnimator != null && squad.SoldierAnimator.HasTexture)
        {
            float scale = squad.Formation == FormationType.Cavalry ? 0.10f : 0.08f;
            for (int i = 0; i < visibleSoldiers; i++)
            {
                Vector2 sPos = pos + squad.SoldierOffsets[i];
                squad.SoldierAnimator.Draw(SpriteBatch, sPos, soldierColor, flip, scale);
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

        // Draw general
        if (squad.General != null)
        {
            Color genColor = squad.Team == Team.Player ? new Color(80, 140, 220) : new Color(220, 80, 80);
            if (squad.GeneralAnimator != null && squad.GeneralAnimator.HasTexture)
                squad.GeneralAnimator.Draw(SpriteBatch, pos, genColor, flip, 0.15f);
            else
            {
                int genSize = 16;
                SpriteBatch.Draw(_pixel, new Rectangle((int)pos.X - genSize / 2, (int)pos.Y - genSize / 2, genSize, genSize), genColor);
            }
        }
    }

    private void DrawEventLog()
    {
        int logX = GameSettings.ScreenWidth - 280;
        int logY = 150;
        int logW = 265;
        int logH = 180;
        SpriteBatch.Draw(_pixel, new Rectangle(logX, logY, logW, logH), new Color(25, 20, 14, 200));
        UIHelper.DrawBorder(SpriteBatch, _pixel, new Rectangle(logX, logY, logW, logH), new Color(80, 65, 45), 1);
        SpriteBatch.DrawString(_smallFont, "战况", new Vector2(logX + 5, logY + 3), new Color(200, 180, 140));

        var recentEvents = _eventLog.GetRecent(6);
        int eventY = logY + 22;
        foreach (var evt in recentEvents)
        {
            int evtMins = (int)(evt.Time / 60);
            int evtSecs = (int)(evt.Time % 60);
            string timeStr = $"[{evtMins:00}:{evtSecs:00}]";
            SpriteBatch.DrawString(_smallFont, $"{timeStr} {evt.Description}", new Vector2(logX + 5, eventY), evt.Color);
            eventY += 22;
        }
    }
}
