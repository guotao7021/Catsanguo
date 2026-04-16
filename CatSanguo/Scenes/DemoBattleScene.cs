using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
using CatSanguo.Systems;

namespace CatSanguo.Scenes;

public class DemoBattleScene : Scene
{
    // Input data
    private readonly List<GeneralData> _playerGenerals;
    private readonly StageData _stageData;

    // Data
    private List<SkillData> _allSkillData = new();
    private List<FormationData> _formations = new();

    // Battle state
    private AutoBattlePhase _phase = AutoBattlePhase.Fighting;
    private List<Squad> _allSquads = new();
    private List<Squad> _playerSquads = new();
    private List<Squad> _enemySquads = new();
    private MoraleSystem _moraleSystem = new();
    private BattleAI _playerAI = null!;
    private BattleAI _enemyAI = null!;
    private BattleEventLog _eventLog = new();
    private float _battleTime;
    private float _speedMultiplier = 4f;
    private bool _isVictory;
    private string _resultText = "";

    // Tracking
    private Dictionary<string, int> _generalKillCounts = new();
    private Dictionary<string, float> _generalDamageDealt = new();
    private Dictionary<string, int> _skillsUsedThisBattle = new();
    private Dictionary<Squad, float> _prevHP = new();

    // Core battle systems
    private EventBus _eventBus = null!;
    private BuffSystem _buffSystem = null!;
    private SkillTriggerSystem _skillTriggerSystem = null!;
    private BattleContext _battleContext = null!;

    // Presentation
    private CombatPresentationSystem _presentationSystem = null!;

    // UI
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _titleFont = null!;
    private SpriteFontBase _smallFont = null!;
    private BattleUIManager _uiManager = null!;

    // VFX
    private FloatingTextManager _floatingTexts = new();
    private DeathNotificationManager _deathNotifications = new();
    private float _shakeTimer;
    private float _shakeIntensity;

    // Reward result (cached after battle end)
    private BattleRewardResult? _rewardResult;

    public DemoBattleScene(List<GeneralData> playerGenerals, StageData stageData)
    {
        _playerGenerals = playerGenerals;
        _stageData = stageData;
    }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;
        _smallFont = Game.SmallFont;

        // Load data from DataManager
        _allSkillData = DataManager.Instance.AllSkills;
        _formations = DataManager.Instance.AllFormations;
        var allGenerals = DataManager.Instance.AllGenerals;

        // Initialize presentation system
        _presentationSystem = new CombatPresentationSystem();
        _presentationSystem.Initialize(_pixel, _font, _titleFont);

        // Create player squads
        int slotY = 200;
        foreach (var genData in _playerGenerals)
        {
            var squad = CreateSquad(genData, Team.Player);
            squad.Position = new Vector2(150, slotY);
            squad.FacingDirection = 1;
            _playerSquads.Add(squad);
            _allSquads.Add(squad);
            _prevHP[squad] = squad.HP;
            slotY += 120;
        }

        // Create enemy squads from StageData
        slotY = 200;
        foreach (var es in _stageData.EnemySquads)
        {
            var genData = allGenerals.FirstOrDefault(g => g.Id == es.GeneralId);
            if (genData == null) continue;

            // Override formation if specified in stage
            if (!string.IsNullOrEmpty(es.FormationType))
                genData.PreferredFormation = es.FormationType;

            var squad = CreateSquad(genData, Team.Enemy);

            // Use stage-defined position or default
            if (es.PositionX > 0 && es.PositionY > 0)
                squad.Position = new Vector2(es.PositionX, es.PositionY);
            else
                squad.Position = new Vector2(GameSettings.ScreenWidth - 150, slotY);

            squad.FacingDirection = -1;
            squad.SoldierCount = es.SoldierCount;
            squad.MaxSoldierCount = es.SoldierCount;
            squad.InitializeSoldierOffsets();

            _enemySquads.Add(squad);
            _allSquads.Add(squad);
            _prevHP[squad] = squad.HP;
            slotY += 120;
        }

        // If no enemy squads from stage data, create a default enemy
        if (_enemySquads.Count == 0)
        {
            var defaultEnemy = allGenerals.Count > 3 ? allGenerals[3] : allGenerals.First();
            var squad = CreateSquad(defaultEnemy, Team.Enemy);
            squad.Position = new Vector2(GameSettings.ScreenWidth - 150, 200);
            squad.FacingDirection = -1;
            squad.InitializeSoldierOffsets();
            _enemySquads.Add(squad);
            _allSquads.Add(squad);
            _prevHP[squad] = squad.HP;
        }

        // Create AI
        int difficulty = Math.Max(1, _stageData.Difficulty);
        _playerAI = new BattleAI(Team.Player, 2);
        _enemyAI = new BattleAI(Team.Enemy, difficulty);

        // Initialize battle UI manager
        _uiManager = new BattleUIManager(BattleUIMode.Auto);
        _uiManager.Initialize(_pixel, _font, _titleFont, _smallFont);
        _uiManager.SetSquadLists(_playerSquads, _enemySquads);
        _uiManager.HUD.StageName = $"Demo: {_stageData.Name}";
        _uiManager.HUD.OnSpeedToggled = ToggleSpeed;
        _uiManager.HUD.OnSkipClicked = SkipBattle;
        _uiManager.ResultPanel.OnContinue = OnContinue;

        _eventLog.Add(0, $"Demo 战斗: {_stageData.Name}!", BattleEventType.BattleEnd);

        // Initialize core battle systems
        _eventBus = new EventBus();
        _buffSystem = new BuffSystem(_eventBus);
        _skillTriggerSystem = new SkillTriggerSystem(_eventBus, _buffSystem, _allSquads, new Random());
        _battleContext = new BattleContext(_eventBus, _buffSystem, _skillTriggerSystem);

        // Load buff configs
        string buffsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "buffs.json");
        _buffSystem.LoadConfigs(buffsPath);

        // Set UI buff system reference
        _uiManager.SetBuffSystem(_buffSystem);

        // Initialize all Squad contexts
        foreach (var squad in _allSquads)
            squad.Context = _battleContext;

        // Register squads and start battle
        foreach (var squad in _allSquads)
            _skillTriggerSystem.RegisterSquad(squad);
        _skillTriggerSystem.Initialize();
        _eventBus.Publish(new GameEvent(GameEventType.OnBattleStart));

        _phase = AutoBattlePhase.Fighting;
    }

    private Squad CreateSquad(GeneralData genData, Team team)
    {
        var general = General.FromData(genData);
        FormationType ft = Enum.TryParse<FormationType>(genData.PreferredFormation, true, out var parsed)
            ? parsed : FormationType.Vanguard;

        var formData = _formations.FirstOrDefault(f =>
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
            Attributes = new AttributeSet()
        };

        // Set attribute base values
        squad.Attributes.SetBase(AttrType.MaxHP, baseHP + general.Leadership * 5);
        squad.Attributes.SetBase(AttrType.Attack, baseAtk);
        squad.Attributes.SetBase(AttrType.Defense, baseDef);
        squad.Attributes.SetBase(AttrType.Speed, baseSpd * 100);
        squad.Attributes.SetBase(AttrType.CritRate, 5);
        squad.Attributes.SetBase(AttrType.CritDamage, 150);
        squad.Attributes.SetBase(AttrType.AttackRange, atkRange);

        // General stat modifiers
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_str", AttrType.Attack, ModifierOp.Add, general.Strength * 2));
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_lvl", AttrType.Defense, ModifierOp.Add, general.Leadership * 1.5f));
        squad.Attributes.AddModifier(new Modifier($"gen_{general.Id}_spd", AttrType.Speed, ModifierOp.Add, general.Speed * 1.2f));

        // Skills
        var activeSkillData = _allSkillData.FirstOrDefault(s => s.Id == genData.ActiveSkillId);
        if (activeSkillData != null)
            squad.ActiveSkill = Skill.FromData(activeSkillData);

        var passiveSkillData = _allSkillData.FirstOrDefault(s => s.Id == genData.PassiveSkillId);
        if (passiveSkillData != null)
        {
            squad.PassiveSkill = Skill.FromData(passiveSkillData);
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
        for (int i = 0; i < 2000; i++)
        {
            if (_phase != AutoBattlePhase.Fighting) break;
            UpdateFighting(0.05f);
        }
    }

    public override void Update(GameTime gameTime)
    {
        float rawDt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float timeScale = _presentationSystem?.GetTimeScale() ?? 1.0f;
        float scaledDt = rawDt * _speedMultiplier * timeScale;

        switch (_phase)
        {
            case AutoBattlePhase.Fighting:
                UpdateFighting(scaledDt);
                break;
            case AutoBattlePhase.Result:
                break;
        }

        bool paused = false;
        _uiManager.Update(rawDt, Input, _battleTime, _speedMultiplier, paused);

        _floatingTexts.Update(rawDt);
        _deathNotifications.Update(rawDt);
        _presentationSystem?.Update(rawDt);

        if (_shakeTimer > 0)
            _shakeTimer -= rawDt;
    }

    private void UpdateFighting(float dt)
    {
        _battleTime += dt;
        _buffSystem.Update(dt);

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

            // Track damage
            if (squad.HP < prevHP)
            {
                float dmg = prevHP - squad.HP;
                string attackerId = squad.TargetSquad?.General?.Id ?? "unknown";

                if (!_generalDamageDealt.ContainsKey(attackerId))
                    _generalDamageDealt[attackerId] = 0f;
                _generalDamageDealt[attackerId] += dmg;

                if (squad.TargetSquad != null)
                {
                    Color hitColor = squad.Team == Team.Player ? Color.LightCoral : Color.LightBlue;
                    _presentationSystem?.AddFloatingText($"{(int)dmg}", squad.Position, hitColor, 1.0f, false);
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
                if (squad.Morale < 20 && _prevHP[squad] == prevHP)
                    _eventLog.Add(_battleTime, $"{squad.General.Name} 溃败!", BattleEventType.MoraleBreak);
            }

            _prevHP[squad] = squad.HP;
        }

        _moraleSystem.Update(dt, _allSquads);
        _playerAI.Update(dt, _allSquads);
        _enemyAI.Update(dt, _allSquads);

        // Track skill usage
        foreach (var squad in _allSquads.Where(s => s.IsActive && s.State == SquadState.UsingSkill && s.ActiveSkill != null && s.General != null))
        {
            string key = $"{squad.General!.Id}:{squad.ActiveSkill!.Id}";
            if (!_skillsUsedThisBattle.ContainsKey(key))
            {
                _skillsUsedThisBattle[key] = 0;
                string skillMsg = $"{squad.General.Name} 释放 {squad.ActiveSkill.Name}!";
                _eventLog.Add(_battleTime, skillMsg, BattleEventType.SkillUsed);
                _floatingTexts.AddText(squad.ActiveSkill.Name, squad.Position - new Vector2(0, 45), new Color(255, 230, 100));

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
            EndBattle(true);
        else if (!playerAlive)
            EndBattle(false);
    }

    private void EndBattle(bool victory)
    {
        _phase = AutoBattlePhase.Result;
        _isVictory = victory;
        _resultText = victory ? "胜 利 !" : "败 北 ...";
        _eventLog.Add(_battleTime, _resultText, BattleEventType.BattleEnd);

        // Use RewardSystem to calculate rewards
        int playerSurvivors = _playerSquads.Count(s => !s.IsDead);
        int playerTotal = _playerSquads.Count;
        int enemyKills = _enemySquads.Count(s => s.IsDead);
        int enemyTotal = _enemySquads.Count;

        _rewardResult = GameRoot.Instance.Systems.Rewards.Calculate(
            _isVictory, playerSurvivors, playerTotal,
            enemyKills, enemyTotal, _battleTime,
            "medium", _stageData.Difficulty);

        // Build result data for UI
        var keyEvents = _eventLog.Events
            .Where(e => e.Type == BattleEventType.GeneralDeath || e.Type == BattleEventType.SkillUsed)
            .Select(e => e.Description).Take(3).ToList();

        var resultData = new BattleResultData
        {
            IsVictory = _isVictory,
            PerformanceRating = _rewardResult.Rating,
            BattleTime = _battleTime,
            PlayerLost = _playerSquads.Count(s => s.IsDead),
            EnemyLost = enemyKills,
            TotalXp = _rewardResult.XpPerGeneral * playerTotal,
            GoldReward = _rewardResult.Gold,
            FoodReward = _rewardResult.Food,
            WoodReward = _rewardResult.Wood,
            IronReward = _rewardResult.Iron,
            MeritReward = _rewardResult.Merit,
            KeyEvents = keyEvents
        };
        _uiManager.ShowResult(resultData);
    }

    private void OnContinue()
    {
        // Apply rewards via RewardSystem
        if (_rewardResult != null)
        {
            string cityId = GameRoot.Instance.GetDemoCityId();
            var generalIds = _playerGenerals.Select(g => g.Id).ToList();
            GameRoot.Instance.Systems.Rewards.Apply(cityId, _rewardResult, generalIds);
        }

        // Return to city
        GameRoot.Instance.TransitionTo(GamePhase.City);
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(50, 45, 35));
        SpriteBatch.Begin();

        // Screen shake offset
        Vector2 shakeOffset = _presentationSystem?.GetScreenShakeOffset() ?? Vector2.Zero;
        if (_shakeTimer > 0 && shakeOffset == Vector2.Zero)
        {
            shakeOffset = new Vector2(
                (float)(Random.Shared.NextDouble() - 0.5) * _shakeIntensity * 2,
                (float)(Random.Shared.NextDouble() - 0.5) * _shakeIntensity * 2
            );
        }

        // Background
        DrawBattlefield(shakeOffset);

        // Draw squads
        foreach (var squad in _allSquads.Where(s => !s.IsDead))
            DrawSquadSimple(squad, shakeOffset);

        // Floating texts
        _floatingTexts.Draw(SpriteBatch, _font);

        // Presentation VFX
        _presentationSystem?.Draw(SpriteBatch);

        // Battle UI
        _uiManager.Draw(SpriteBatch, shakeOffset);

        // Event log during fighting
        if (_phase == AutoBattlePhase.Fighting)
            DrawEventLog();

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
