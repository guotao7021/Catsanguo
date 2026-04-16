using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Core.Animation;
using CatSanguo.Data.Schemas;
using CatSanguo.Battle;
using CatSanguo.Battle.Sango;
using CatSanguo.Generals;
using CatSanguo.UI;

namespace CatSanguo.Scenes;

public class SangoFieldBattleScene : Scene
{
    // 渲染资源
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _titleFont = null!;
    private SpriteFontBase _smallFont = null!;

    // 相机
    private Camera2D _camera = null!;
    private BattleCameraController _cameraController = null!;

    // 战场
    private ArmyGroup _playerArmy = null!;
    private ArmyGroup _enemyArmy = null!;
    private ProjectileManager _projectiles = new();

    // UI组件
    private SangoBattleHUD _hud = null!;
    private SangoCommandBar _commandBar = null!;

    // 战斗状态
    private SangoBattlePhase _phase = SangoBattlePhase.Deploy;
    private float _countdownTimer = 3f;
    private float _battleTimer;
    private float _speedMultiplier = 1f;

    // 数据
    private List<GeneralData> _allGenerals = new();
    private List<SkillData> _allSkills = new();

    // 输入的参数 (从WorldMapScene传入)
    private List<string>? _playerGeneralIds;
    private List<StageSquadData>? _enemySquads;
    private Action? _onComplete;

    // UI
    private Button? _startButton;
    private Button? _speedButton;
    private Button? _backButton;

    // 屏幕震动
    private float _shakeTimer;
    private float _shakeIntensity;

    // 单挑系统
    private DuelSystem _duel = new();

    // VFX + 军师技
    private BattleVFXSystem _vfx = new();
    private AdvisorSkillSystem _advisorSkill = new();

    // 回合管理 + 技能系统
    private RoundManager _roundManager = new();
    private GeneralSkillSystem _skillSystem = new();
    private Button? _endTurnButton;

    // 精灵渲染资源
    private Texture2D? _shadowTexture;
    private Texture2D? _arrowTexture;

    /// <summary>测试入口 (无参数，使用默认武将)</summary>
    public SangoFieldBattleScene() { }

    /// <summary>正式入口 (从世界地图传入参数)</summary>
    public SangoFieldBattleScene(List<string> playerGeneralIds, List<StageSquadData>? enemySquads, Action? onComplete = null)
    {
        _playerGeneralIds = playerGeneralIds;
        _enemySquads = enemySquads;
        _onComplete = onComplete;
    }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.NotifyFont;
        _smallFont = Game.SmallFont;

        // 使用DataManager缓存数据
        _allGenerals = DataManager.Instance.AllGenerals;
        _allSkills = DataManager.Instance.AllSkills;

        // 初始化相机
        _camera = new Camera2D(GraphicsDevice);
        _camera.WorldBounds = new Rectangle(0, 0, GameSettings.SangoBattlefieldWidth, GameSettings.SangoBattlefieldHeight);
        _camera.Position = new Vector2(GameSettings.SangoBattlefieldWidth / 2f, GameSettings.SangoBattlefieldHeight / 2f);
        _camera.MinZoom = 0.7f;
        _camera.MaxZoom = 1.3f;
        _cameraController = new BattleCameraController(_camera);

        // 创建军团
        CreateArmies();

        // 获取精灵渲染资源
        _shadowTexture = Game.SpriteSheets.ShadowTexture;
        _arrowTexture = Game.SpriteSheets.ArrowTexture;

        // 初始化HUD和指令栏
        _hud = new SangoBattleHUD(_pixel, _font, _smallFont);
        _hud.OnGeneralSelected = idx =>
        {
            if (idx >= 0 && idx < _playerArmy.Units.Count)
                _commandBar.UpdateForGeneral(_playerArmy.Units[idx]);
        };
        _commandBar = new SangoCommandBar(_pixel, _font, _smallFont);
        _commandBar.OnDuelChallenge = TryInitiateDuel;

        // 单挑完成回调
        _duel.OnDuelComplete = (winner, loser) =>
        {
            _phase = SangoBattlePhase.RoundCommand;
            _shakeTimer = 0.5f;
            _shakeIntensity = 8f;
        };

        // 军师技回调
        _advisorSkill.OnComplete = () => { /* 返回战斗 */ };
        _commandBar.OnAdvisorSkill = TryCastAdvisorSkill;

        // 技能回调 - 回合制技能释放
        _commandBar.OnSkillUsed = TryCastGeneralSkill;

        // 创建UI按钮
        _startButton = new Button("开 战", new Rectangle(
            GameSettings.ScreenWidth / 2 - 60, GameSettings.ScreenHeight - 60, 120, 40));
        _startButton.NormalColor = new Color(120, 50, 30);
        _startButton.HoverColor = new Color(160, 70, 40);
        _startButton.OnClick = StartBattle;

        _speedButton = new Button("1x", new Rectangle(GameSettings.ScreenWidth - 80, GameSettings.ScreenHeight - 50, 60, 35));
        _speedButton.NormalColor = new Color(50, 50, 60);
        _speedButton.HoverColor = new Color(70, 70, 80);
        _speedButton.OnClick = ToggleSpeed;

        _backButton = new Button("返 回", new Rectangle(20, GameSettings.ScreenHeight - 50, 80, 35));
        _backButton.NormalColor = new Color(60, 30, 30);
        _backButton.HoverColor = new Color(80, 40, 40);
        _backButton.OnClick = GoBack;

        _endTurnButton = new Button("结束回合", new Rectangle(
            GameSettings.ScreenWidth / 2 + 180, GameSettings.ScreenHeight - (int)GameSettings.SangoBottomBarHeight + 25, 100, 40));
        _endTurnButton.NormalColor = new Color(80, 70, 40);
        _endTurnButton.HoverColor = new Color(110, 95, 55);
        _endTurnButton.OnClick = EndTurn;
    }

    private void CreateArmies()
    {
        _playerArmy = new ArmyGroup(Team.Player);
        _enemyArmy = new ArmyGroup(Team.Enemy);

        // 获取玩家武将
        var playerGens = GetPlayerGenerals();
        float playerStartX = 200f;
        float slotY = 250f;
        float slotGap = 150f;

        for (int i = 0; i < playerGens.Count && i < 3; i++)
        {
            var gen = playerGens[i];
            var unitType = MapFormationToUnitType(gen.PreferredFormation);
            var generalObj = General.FromData(gen);
            var unit = new GeneralUnit(generalObj, Team.Player, unitType);
            unit.GeneralPosition = new Vector2(playerStartX, slotY + i * slotGap);
            unit.GeneralAnimator = Game.SpriteSheets.CreateAnimator("general_default");
            unit.SpawnSoldiers(GameSettings.SangoSoldiersPerGeneral, Game.SpriteSheets);
            _playerArmy.Units.Add(unit);
        }

        // 获取敌方武将
        var enemyGens = GetEnemyGenerals();
        float enemyStartX = GameSettings.SangoBattlefieldWidth - 200f;

        for (int i = 0; i < enemyGens.Count && i < 3; i++)
        {
            var gen = enemyGens[i];
            var unitType = MapFormationToUnitType(gen.PreferredFormation);
            var generalObj = General.FromData(gen);
            var unit = new GeneralUnit(generalObj, Team.Enemy, unitType);
            unit.GeneralPosition = new Vector2(enemyStartX, slotY + i * slotGap);
            unit.GeneralAnimator = Game.SpriteSheets.CreateAnimator("general_default");
            unit.SpawnSoldiers(GameSettings.SangoSoldiersPerGeneral, Game.SpriteSheets);
            _enemyArmy.Units.Add(unit);
        }

        // 连接所有士兵的投射物回调 + 受击VFX回调
        foreach (var unit in _playerArmy.Units.Concat(_enemyArmy.Units))
        {
            foreach (var soldier in unit.Soldiers)
            {
                soldier.OnProjectileFired = p => _projectiles.Add(p);
                soldier.OnHitVFX = pos => _vfx.SpawnHitSparks(pos);
            }
        }

        // 解析所有武将技能
        foreach (var unit in _playerArmy.Units.Concat(_enemyArmy.Units))
        {
            _skillSystem.ResolveSkills(unit, _allSkills);
            _skillSystem.ResolvePassiveSkill(unit, _allSkills);
        }
    }

    private List<GeneralData> GetPlayerGenerals()
    {
        if (_playerGeneralIds != null && _playerGeneralIds.Count > 0)
        {
            return _playerGeneralIds
                .Select(id => _allGenerals.FirstOrDefault(g => g.Id == id))
                .Where(g => g != null)
                .Cast<GeneralData>()
                .ToList();
        }
        // 默认：取前2个武将
        return _allGenerals.Take(2).ToList();
    }

    private List<GeneralData> GetEnemyGenerals()
    {
        if (_enemySquads != null && _enemySquads.Count > 0)
        {
            return _enemySquads
                .Select(es => _allGenerals.FirstOrDefault(g => g.Id == es.GeneralId))
                .Where(g => g != null)
                .Cast<GeneralData>()
                .ToList();
        }
        // 默认：取第3、4个武将作为敌方
        return _allGenerals.Skip(2).Take(2).ToList();
    }

    private UnitType MapFormationToUnitType(string? formation)
    {
        return formation?.ToLower() switch
        {
            "archer" => UnitType.Archer,
            "cavalry" => UnitType.Cavalry,
            _ => UnitType.Infantry
        };
    }

    // ==================== Update ====================

    public override void Update(GameTime gameTime)
    {
        float rawDt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float dt = rawDt * _speedMultiplier;

        switch (_phase)
        {
            case SangoBattlePhase.Deploy:
                UpdateDeploy(rawDt);
                break;
            case SangoBattlePhase.Countdown:
                UpdateCountdown(dt);
                break;
            case SangoBattlePhase.Charge:
                UpdateCharge(dt);
                break;
            case SangoBattlePhase.Melee:
                // 过渡状态: 立即进入第一回合指令阶段
                foreach (var unit in _playerArmy.Units) unit.EnterMelee();
                foreach (var unit in _enemyArmy.Units) unit.EnterMelee();
                _roundManager.BeginFirstRound();
                _phase = SangoBattlePhase.RoundCommand;
                break;
            case SangoBattlePhase.RoundCommand:
                UpdateRoundCommand(rawDt);
                break;
            case SangoBattlePhase.RoundExecution:
                UpdateRoundExecution(dt);
                break;
            case SangoBattlePhase.Result:
                UpdateResult(rawDt);
                break;
            case SangoBattlePhase.Duel:
                _duel.Update(dt);
                if (!_duel.IsActive)
                    _phase = SangoBattlePhase.RoundCommand;
                break;
        }

        // 更新相机 (使用控制器)
        _cameraController.Update(Input, rawDt, _phase, _playerArmy, _enemyArmy);

        // 更新VFX + 军师技
        _vfx.Update(dt);
        _advisorSkill.Update(dt);

        // 更新指令栏hover
        _commandBar.UpdateHover(Input);

        // 屏幕震动
        if (_shakeTimer > 0)
            _shakeTimer -= rawDt;

        // ESC 返回
        if (Input.IsKeyPressed(Keys.Escape) && _phase == SangoBattlePhase.Deploy)
            GoBack();
    }

    private void UpdateDeploy(float dt)
    {
        _startButton?.Update(Input);
        _backButton?.Update(Input);
    }

    private void UpdateCountdown(float dt)
    {
        _countdownTimer -= dt;
        if (_countdownTimer <= 0)
        {
            _phase = SangoBattlePhase.Charge;
            // 所有军团开始冲锋
            foreach (var unit in _playerArmy.Units) unit.StartCharge();
            foreach (var unit in _enemyArmy.Units) unit.StartCharge();
        }
    }

    private void UpdateCharge(float dt)
    {
        _battleTimer += dt;
        _playerArmy.Update(dt);
        _enemyArmy.Update(dt);
        _projectiles.Update(dt);

        // 检查是否有士兵接触 → 切换到Melee
        var playerSoldiers = _playerArmy.GetAllAliveSoldiers();
        var enemySoldiers = _enemyArmy.GetAllAliveSoldiers();

        bool anyContact = false;
        foreach (var ps in playerSoldiers)
        {
            if (ps.State != SoldierState.Charging) continue;
            foreach (var es in enemySoldiers)
            {
                if (es.State != SoldierState.Charging) continue;
                float dist = Vector2.Distance(ps.Position, es.Position);
                if (dist < GameSettings.SangoCollisionRadius * 2)
                {
                    // 这对士兵进入战斗
                    ps.State = SoldierState.Fighting;
                    ps.Target = es;
                    es.State = SoldierState.Fighting;
                    es.Target = ps;
                    anyContact = true;
                }
            }
        }

        // 弓兵检测射程内目标
        HandleRangedSoldiers(playerSoldiers, enemySoldiers);
        HandleRangedSoldiers(enemySoldiers, playerSoldiers);

        if (anyContact)
        {
            _phase = SangoBattlePhase.Melee;
            foreach (var unit in _playerArmy.Units) unit.EnterMelee();
            foreach (var unit in _enemyArmy.Units) unit.EnterMelee();
            _shakeTimer = 0.3f;
            _shakeIntensity = 6f;
        }

        _speedButton?.Update(Input);
        _commandBar.Update(Input);
    }

    private void UpdateMelee(float dt)
    {
        _battleTimer += dt;
        _playerArmy.Update(dt);
        _enemyArmy.Update(dt);
        _projectiles.Update(dt);

        var playerSoldiers = _playerArmy.GetAllAliveSoldiers();
        var enemySoldiers = _enemyArmy.GetAllAliveSoldiers();

        // 为没有目标的Fighting士兵寻找新目标
        AssignTargets(playerSoldiers, enemySoldiers);
        AssignTargets(enemySoldiers, playerSoldiers);

        // 冲锋中的士兵碰到敌人也要进入战斗
        foreach (var ps in playerSoldiers.Where(s => s.State == SoldierState.Charging))
        {
            var nearest = FindNearest(ps, enemySoldiers);
            if (nearest != null && Vector2.Distance(ps.Position, nearest.Position) < GameSettings.SangoCollisionRadius * 2)
            {
                ps.State = SoldierState.Fighting;
                ps.Target = nearest;
                if (nearest.State == SoldierState.Charging || nearest.Target == null)
                {
                    nearest.State = SoldierState.Fighting;
                    nearest.Target = ps;
                }
            }
        }
        foreach (var es in enemySoldiers.Where(s => s.State == SoldierState.Charging))
        {
            var nearest = FindNearest(es, playerSoldiers);
            if (nearest != null && Vector2.Distance(es.Position, nearest.Position) < GameSettings.SangoCollisionRadius * 2)
            {
                es.State = SoldierState.Fighting;
                es.Target = nearest;
                if (nearest.State == SoldierState.Charging || nearest.Target == null)
                {
                    nearest.State = SoldierState.Fighting;
                    nearest.Target = es;
                }
            }
        }

        // 弓兵射击
        HandleRangedSoldiers(playerSoldiers, enemySoldiers);
        HandleRangedSoldiers(enemySoldiers, playerSoldiers);

        // 胜负判定
        if (_playerArmy.IsDefeated() || _enemyArmy.IsDefeated())
        {
            _phase = SangoBattlePhase.Result;
            _shakeTimer = 0.5f;
            _shakeIntensity = 10f;
        }

        _speedButton?.Update(Input);
        _commandBar.Update(Input);
    }

    private void UpdateRoundCommand(float rawDt)
    {
        // 指令阶段: 时间冻结, 只处理UI输入
        _commandBar.IsCommandPhase = true;
        _hud.DrawRoundInfo = (_roundManager.CurrentRound, true, 0f, 0f);

        // 每帧刷新选中武将的技能状态
        int selIdx = _hud.SelectedGeneralIndex;
        if (selIdx >= 0 && selIdx < _playerArmy.Units.Count)
            _commandBar.UpdateForGeneral(_playerArmy.Units[selIdx]);
        else
            _commandBar.UpdateForGeneral(null);

        // 更新UI组件 (不更新战斗)
        _commandBar.UpdateHover(Input);
        _commandBar.Update(Input);
        _speedButton?.Update(Input);
        _endTurnButton?.Update(Input);

        // Space/Enter 快捷键结束回合
        if (Input.IsKeyPressed(Keys.Space) || Input.IsKeyPressed(Keys.Enter))
            EndTurn();

        // 更新VFX (飘字等继续播放)
        _vfx.Update(rawDt);
        _advisorSkill.Update(rawDt);
    }

    private void UpdateRoundExecution(float dt)
    {
        _commandBar.IsCommandPhase = false;
        _battleTimer += dt;

        // 执行阶段首帧: 处理AI技能
        ProcessAISkills();

        // 运行现有混战逻辑
        _playerArmy.Update(dt);
        _enemyArmy.Update(dt);
        _projectiles.Update(dt);

        var playerSoldiers = _playerArmy.GetAllAliveSoldiers();
        var enemySoldiers = _enemyArmy.GetAllAliveSoldiers();

        AssignTargets(playerSoldiers, enemySoldiers);
        AssignTargets(enemySoldiers, playerSoldiers);

        // 冲锋中的士兵碰到敌人进入战斗
        foreach (var ps in playerSoldiers.Where(s => s.State == SoldierState.Charging))
        {
            var nearest = FindNearest(ps, enemySoldiers);
            if (nearest != null && Vector2.Distance(ps.Position, nearest.Position) < GameSettings.SangoCollisionRadius * 2)
            {
                ps.State = SoldierState.Fighting;
                ps.Target = nearest;
                if (nearest.State == SoldierState.Charging || nearest.Target == null)
                {
                    nearest.State = SoldierState.Fighting;
                    nearest.Target = ps;
                }
            }
        }
        foreach (var es in enemySoldiers.Where(s => s.State == SoldierState.Charging))
        {
            var nearest = FindNearest(es, playerSoldiers);
            if (nearest != null && Vector2.Distance(es.Position, nearest.Position) < GameSettings.SangoCollisionRadius * 2)
            {
                es.State = SoldierState.Fighting;
                es.Target = nearest;
                if (nearest.State == SoldierState.Charging || nearest.Target == null)
                {
                    nearest.State = SoldierState.Fighting;
                    nearest.Target = es;
                }
            }
        }

        HandleRangedSoldiers(playerSoldiers, enemySoldiers);
        HandleRangedSoldiers(enemySoldiers, playerSoldiers);

        // 胜负判定
        if (_playerArmy.IsDefeated() || _enemyArmy.IsDefeated())
        {
            _phase = SangoBattlePhase.Result;
            _shakeTimer = 0.5f;
            _shakeIntensity = 10f;
            return;
        }

        // 更新执行计时器
        _hud.DrawRoundInfo = (_roundManager.CurrentRound, false, _roundManager.ExecutionTimer, _roundManager.ExecutionDuration);
        if (_roundManager.UpdateExecution(dt))
        {
            // 执行阶段结束, 递减冷却, 回血, 进入下一回合指令阶段
            var allUnits = _playerArmy.Units.Concat(_enemyArmy.Units);
            _roundManager.TickCooldowns(allUnits);
            _advisorSkill.TickCooldown();
            _skillSystem.ApplyRoundEndRegen(allUnits);
            _roundManager.BeginCommandPhase();
            _phase = SangoBattlePhase.RoundCommand;
        }

        _speedButton?.Update(Input);
    }

    private void EndTurn()
    {
        if (_phase != SangoBattlePhase.RoundCommand) return;
        // AI决策
        QueueAIActions();
        // 进入执行阶段
        _roundManager.BeginExecutionPhase();
        _phase = SangoBattlePhase.RoundExecution;
    }

    private void QueueAIActions()
    {
        foreach (var unit in _enemyArmy.Units)
        {
            if (unit.IsDefeated) continue;
            // 简单AI: 选择第一个不在冷却中的技能
            for (int i = 0; i < unit.ResolvedSkills.Count; i++)
            {
                var skill = unit.ResolvedSkills[i];
                if (skill.CooldownRoundsLeft == 0)
                {
                    _roundManager.EnqueueAISkill(unit, i);
                    break;
                }
            }
        }
    }

    private void ProcessAISkills()
    {
        foreach (var (caster, skillIndex) in _roundManager.AISkillQueue)
        {
            if (caster.IsDefeated) continue;
            _skillSystem.CastSkill(caster, skillIndex, _playerArmy, _enemyArmy, _vfx);
        }
        // 清空队列 (仅首帧执行)
        if (_roundManager.AISkillQueue.Count > 0)
            _roundManager.ClearAIQueue();
    }

    private void TryCastGeneralSkill(int slotIndex)
    {
        if (_phase != SangoBattlePhase.RoundCommand || _roundManager.HasPlayerActed) return;
        int idx = _hud.SelectedGeneralIndex;
        if (idx < 0 || idx >= _playerArmy.Units.Count) return;

        var caster = _playerArmy.Units[idx];
        if (caster.IsDefeated) return;

        if (_skillSystem.CastSkill(caster, slotIndex, _enemyArmy, _playerArmy, _vfx))
        {
            _roundManager.HasPlayerActed = true;
            _shakeTimer = 0.2f;
            _shakeIntensity = 4f;
            // 自动进入执行阶段
            EndTurn();
        }
    }

    private void HandleRangedSoldiers(List<Soldier> friendlySoldiers, List<Soldier> enemySoldiers)
    {
        foreach (var s in friendlySoldiers)
        {
            if (!s.Owner.IsRanged || !s.IsAlive) continue;
            if (s.State == SoldierState.Fighting) continue; // 已近战则不切换

            if (s.Target == null || !s.Target.IsAlive)
            {
                s.Target = FindNearest(s, enemySoldiers);
            }

            if (s.Target != null)
            {
                float dist = Vector2.Distance(s.Position, s.Target.Position);
                if (dist <= GameSettings.SangoRangedRange)
                {
                    if (s.State != SoldierState.Shooting)
                        s.State = SoldierState.Shooting;
                }
            }
        }
    }

    private void AssignTargets(List<Soldier> friendlySoldiers, List<Soldier> enemySoldiers)
    {
        foreach (var s in friendlySoldiers)
        {
            if (s.State != SoldierState.Fighting) continue;
            if (s.Target != null && s.Target.IsAlive) continue;

            s.Target = FindNearest(s, enemySoldiers);
            if (s.Target == null)
            {
                // 没有敌人了，回到idle
                s.State = SoldierState.Idle;
            }
        }
    }

    private Soldier? FindNearest(Soldier source, List<Soldier> candidates)
    {
        Soldier? nearest = null;
        float minDist = float.MaxValue;
        foreach (var c in candidates)
        {
            if (!c.IsAlive) continue;
            float dist = Vector2.DistanceSquared(source.Position, c.Position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = c;
            }
        }
        return nearest;
    }

    private void UpdateResult(float dt)
    {
        _backButton?.Update(Input);
    }

    private void StartBattle()
    {
        _phase = SangoBattlePhase.Countdown;
        _countdownTimer = 3f;
        _commandBar.Show();
        _cameraController.ResetManualOverride();
    }

    private void ToggleSpeed()
    {
        _speedMultiplier = _speedMultiplier switch
        {
            1f => 2f,
            2f => 3f,
            _ => 1f
        };
        if (_speedButton != null)
            _speedButton.Text = $"{(int)_speedMultiplier}x";
    }

    private void GoBack()
    {
        if (_onComplete != null)
            _onComplete();
        else if (_playerGeneralIds != null)
            Game.SceneManager.ChangeScene(new WorldMapScene());
        else
            Game.SceneManager.ChangeScene(new MainMenuScene()); // 测试模式返回主菜单
    }

    private void TryInitiateDuel()
    {
        if (_phase != SangoBattlePhase.RoundCommand) return;
        int idx = _hud.SelectedGeneralIndex;
        if (idx < 0 || idx >= _playerArmy.Units.Count) return;

        var challenger = _playerArmy.Units[idx];
        if (challenger.IsDefeated) return;

        // 找离该武将最近的敌方武将
        GeneralUnit? closestEnemy = null;
        float minDist = float.MaxValue;
        foreach (var eu in _enemyArmy.Units)
        {
            if (eu.IsDefeated) continue;
            float d = Vector2.DistanceSquared(challenger.GeneralPosition, eu.GeneralPosition);
            if (d < minDist) { minDist = d; closestEnemy = eu; }
        }
        if (closestEnemy == null) return;

        _phase = SangoBattlePhase.Duel;
        _duel.StartDuel(challenger, closestEnemy);
    }

    private void TryCastAdvisorSkill()
    {
        if (_phase != SangoBattlePhase.RoundCommand || _advisorSkill.IsActive) return;
        if (_advisorSkill.CooldownRoundsLeft > 0) return;
        int idx = _hud.SelectedGeneralIndex;
        if (idx < 0 || idx >= _playerArmy.Units.Count) return;

        var caster = _playerArmy.Units[idx];
        if (caster.IsDefeated) return;

        // 根据智力选择技能类型
        var skillType = caster.General.EffectiveIntelligence >= 80 ? AdvisorSkillType.Lightning
                      : caster.General.EffectiveIntelligence >= 60 ? AdvisorSkillType.FireRain
                      : AdvisorSkillType.IceStorm;

        _advisorSkill.Cast(caster, _enemyArmy, skillType, _vfx);
        _roundManager.HasPlayerActed = true;
        EndTurn();
    }

    // ==================== Draw ====================

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(40, 55, 30)); // 战场绿色基底

        // 屏幕震动偏移
        Vector2 shakeOffset = Vector2.Zero;
        if (_shakeTimer > 0)
        {
            shakeOffset = new Vector2(
                (float)(Random.Shared.NextDouble() - 0.5) * _shakeIntensity * 2,
                (float)(Random.Shared.NextDouble() - 0.5) * _shakeIntensity * 2);
        }

        // ===== 世界空间层 =====
        var shakeMatrix = Matrix.CreateTranslation(new Vector3(shakeOffset, 0));
        SpriteBatch.Begin(transformMatrix: _camera.GetTransformMatrix() * shakeMatrix);

        DrawBattlefield();
        DrawArmies();
        _projectiles.Draw(SpriteBatch, _pixel, _arrowTexture);
        _vfx.DrawWorld(SpriteBatch, _pixel);

        SpriteBatch.End();

        // ===== 屏幕空间层 =====
        SpriteBatch.Begin();

        _hud.DrawTopHUD(SpriteBatch, _playerArmy, _enemyArmy, _battleTimer, _phase);
        _hud.DrawBottomBar(SpriteBatch, _playerArmy, _enemyArmy, _phase, Input);
        _commandBar.Draw(SpriteBatch);
        _speedButton?.Draw(SpriteBatch, _font, _pixel);

        // VFX飘字 + 全屏闪光
        _vfx.DrawScreen(SpriteBatch, _pixel, _smallFont, pos => _camera.WorldToScreen(pos));

        // 军师技UI
        _advisorSkill.Draw(SpriteBatch, _pixel, _font, _titleFont);

        // 阶段特殊UI
        switch (_phase)
        {
            case SangoBattlePhase.Deploy:
                DrawDeployUI();
                break;
            case SangoBattlePhase.Countdown:
                DrawCountdown();
                break;
            case SangoBattlePhase.RoundCommand:
                DrawRoundCommandUI();
                break;
            case SangoBattlePhase.Result:
                DrawResult();
                break;
            case SangoBattlePhase.Duel:
                _duel.Draw(SpriteBatch, _pixel, _font, _titleFont, _smallFont);
                break;
        }

        // 淡入淡出
        if (Game.SceneManager.IsFading)
        {
            float alpha = Game.SceneManager.FadeAlpha;
            SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
                Color.Black * alpha);
        }

        SpriteBatch.End();
    }

    private void DrawBattlefield()
    {
        int bw = GameSettings.SangoBattlefieldWidth;
        int bh = GameSettings.SangoBattlefieldHeight;

        // 天空渐变 (顶部)
        for (int y = 0; y < 120; y += 4)
        {
            float t = y / 120f;
            byte r = (byte)MathHelper.Lerp(70, 50, t);
            byte g = (byte)MathHelper.Lerp(100, 75, t);
            byte b = (byte)MathHelper.Lerp(130, 55, t);
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, bw, 4), new Color(r, g, b));
        }

        // 远山 (装饰线条)
        DrawMountainSilhouette(bw, 80, new Color(55, 70, 42), 30);
        DrawMountainSilhouette(bw, 130, new Color(50, 65, 38), 20);

        // 草地主体
        for (int y = 150; y < bh; y += 4)
        {
            float t = (y - 150f) / (bh - 150f);
            byte r = (byte)MathHelper.Lerp(48, 38, t);
            byte g = (byte)MathHelper.Lerp(65, 50, t);
            byte b = (byte)MathHelper.Lerp(32, 25, t);
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, bw, 4), new Color(r, g, b));
        }

        // 草地纹理 (横向细线模拟)
        for (int y = 200; y < bh - 50; y += 40)
        {
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, bw, 1),
                new Color(60, 80, 45) * (0.15f + (float)Math.Sin(y * 0.1f) * 0.05f));
        }

        // 战场中线标记 (旗帜位置暗示)
        int midX = bw / 2;
        SpriteBatch.Draw(_pixel, new Rectangle(midX - 1, 150, 2, bh - 250), new Color(100, 100, 80) * 0.1f);

        // 左右阵营旗帜标记
        SpriteBatch.Draw(_pixel, new Rectangle(300, 200, 3, 40), new Color(60, 100, 180) * 0.3f);
        SpriteBatch.Draw(_pixel, new Rectangle(bw - 300, 200, 3, 40), new Color(180, 60, 60) * 0.3f);
    }

    private void DrawMountainSilhouette(int width, int baseY, Color color, int height)
    {
        // 简单山脉轮廓 (用三角形近似)
        int peaks = width / 200;
        for (int i = 0; i < peaks; i++)
        {
            int peakX = i * 200 + 100;
            int peakH = height + (int)(Math.Sin(i * 1.7f) * height * 0.4f);
            // 用矩形近似三角形
            for (int dy = 0; dy < peakH; dy++)
            {
                float ratio = 1f - (float)dy / peakH;
                int w = (int)(100 * ratio);
                SpriteBatch.Draw(_pixel,
                    new Rectangle(peakX - w / 2, baseY - dy, w, 1),
                    color * (0.7f + ratio * 0.3f));
            }
        }
        // 山脚填充
        SpriteBatch.Draw(_pixel, new Rectangle(0, baseY, width, 4), color);
    }

    private void DrawArmies()
    {
        // 收集所有需要绘制的实体，按Y坐标排序 (伪深度)
        var drawList = new List<(float y, Action draw)>();

        foreach (var unit in _playerArmy.Units.Concat(_enemyArmy.Units))
        {
            foreach (var soldier in unit.Soldiers.Where(s => s.State != SoldierState.Dead))
            {
                var s = soldier;
                drawList.Add((s.Position.Y, () => s.Draw(SpriteBatch, _pixel, _shadowTexture)));
            }

            if (unit.State != GeneralUnitState.Defeated)
            {
                var u = unit;
                drawList.Add((u.GeneralPosition.Y, () => DrawGeneralUnit(u)));
            }
        }

        // 按Y排序绘制
        foreach (var item in drawList.OrderBy(d => d.y))
        {
            item.draw();
        }
    }

    private void DrawGeneralUnit(GeneralUnit unit)
    {
        var effects = unit.GeneralFacing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Vector2 pos = unit.GeneralPosition;

        // 1. 阴影 (比士兵大)
        if (_shadowTexture != null)
        {
            SpriteBatch.Draw(_shadowTexture,
                new Vector2(pos.X, pos.Y + 28),
                null, Color.White * 0.6f, 0f,
                new Vector2(_shadowTexture.Width / 2f, _shadowTexture.Height / 2f),
                2.0f, SpriteEffects.None, 0f);
        }

        // 2. 阵营旗帜
        Color bannerColor = unit.Team == Team.Player
            ? new Color(60, 100, 200) : new Color(200, 60, 50);
        Color trimColor = unit.Team == Team.Player
            ? new Color(220, 200, 80) : new Color(220, 190, 80);
        float bannerSway = (float)Math.Sin(_battleTimer * 2.5f) * 1.5f;
        int flagX = (int)(pos.X - 20 + bannerSway);
        int flagY = (int)(pos.Y - 56);
        // 旗杆
        SpriteBatch.Draw(_pixel, new Rectangle(flagX, flagY, 2, 45), new Color(120, 100, 70));
        // 旗面
        SpriteBatch.Draw(_pixel, new Rectangle(flagX + 2, flagY, 10, 8), bannerColor);
        SpriteBatch.Draw(_pixel, new Rectangle(flagX + 2, flagY, 10, 1), trimColor);
        SpriteBatch.Draw(_pixel, new Rectangle(flagX + 2, flagY + 7, 10, 1), trimColor);

        // 3. 武将精灵
        if (unit.GeneralAnimator != null && unit.GeneralAnimator.HasTexture)
        {
            unit.GeneralAnimator.Draw(SpriteBatch, pos, unit.TeamTint, effects, scale: 1.0f);
        }
        else
        {
            Color color = unit.Team == Team.Player
                ? new Color(40, 100, 200)
                : new Color(200, 40, 40);
            int size = 40;
            SpriteBatch.Draw(_pixel,
                new Rectangle((int)pos.X - size / 2, (int)pos.Y - size / 2, size, size),
                color);
        }

        // 4. 武将名字 (世界空间)
        string name = unit.General.Name;
        Vector2 nameSize = _smallFont.MeasureString(name);
        Vector2 namePos = pos + new Vector2(-nameSize.X / 2, -62);
        Color nameColor = unit.Team == Team.Player ? new Color(120, 180, 255) : new Color(255, 130, 130);
        // 名字背景
        SpriteBatch.Draw(_pixel, new Rectangle((int)namePos.X - 2, (int)namePos.Y - 1,
            (int)nameSize.X + 4, (int)nameSize.Y + 2), new Color(0, 0, 0) * 0.7f);
        // 名字边框
        SpriteBatch.Draw(_pixel, new Rectangle((int)namePos.X - 3, (int)namePos.Y - 2,
            (int)nameSize.X + 6, 1), nameColor * 0.4f);
        SpriteBatch.DrawString(_smallFont, name, namePos, nameColor);

        // 5. 武将HP条
        float hpRatio = unit.GeneralMaxHP > 0 ? unit.GeneralHP / unit.GeneralMaxHP : 1f;
        if (hpRatio < 1f)
        {
            int barW = 32, barH = 3;
            int barX = (int)pos.X - barW / 2;
            int barY = (int)namePos.Y + (int)nameSize.Y + 3;
            SpriteBatch.Draw(_pixel, new Rectangle(barX, barY, barW, barH), new Color(30, 25, 20));
            Color hpColor = hpRatio > 0.5f ? new Color(80, 200, 80)
                          : hpRatio > 0.25f ? new Color(220, 180, 50)
                          : new Color(220, 50, 50);
            SpriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barW * hpRatio), barH), hpColor);
        }
    }

    // ==================== 屏幕空间UI ====================

    private void DrawDeployUI()
    {
        // 半透明面板背景
        int panelW = 700, panelH = 320;
        int panelX = GameSettings.ScreenWidth / 2 - panelW / 2;
        int panelY = 80;
        SpriteBatch.Draw(_pixel, new Rectangle(panelX, panelY, panelW, panelH), new Color(20, 15, 10) * 0.85f);

        // 标题
        string title = "— 战 斗 部 署 —";
        Vector2 titleSize = _titleFont.MeasureString(title);
        SpriteBatch.DrawString(_titleFont, title,
            new Vector2(GameSettings.ScreenWidth / 2 - titleSize.X / 2, panelY + 10),
            new Color(255, 220, 130));

        // 左侧：我方武将列表
        int leftX = panelX + 30;
        int infoY = panelY + 60;
        SpriteBatch.DrawString(_font, "我方", new Vector2(leftX, infoY), new Color(120, 180, 255));
        infoY += 30;

        foreach (var unit in _playerArmy.Units)
        {
            string info = $"{unit.General.Name}  武:{unit.General.Strength} 智:{unit.General.Intelligence} 统:{unit.General.Command}  兵:{unit.InitialSoldierCount}";
            SpriteBatch.DrawString(_smallFont, info, new Vector2(leftX, infoY), new Color(200, 200, 200));
            infoY += 22;
        }

        // 右侧：敌方武将列表
        int rightX = panelX + panelW / 2 + 20;
        infoY = panelY + 60;
        SpriteBatch.DrawString(_font, "敌方", new Vector2(rightX, infoY), new Color(255, 130, 130));
        infoY += 30;

        foreach (var unit in _enemyArmy.Units)
        {
            string info = $"{unit.General.Name}  武:{unit.General.Strength} 智:{unit.General.Intelligence} 统:{unit.General.Command}  兵:{unit.InitialSoldierCount}";
            SpriteBatch.DrawString(_smallFont, info, new Vector2(rightX, infoY), new Color(200, 200, 200));
            infoY += 22;
        }

        // VS分隔线
        int midX = panelX + panelW / 2;
        SpriteBatch.Draw(_pixel, new Rectangle(midX, panelY + 55, 1, panelH - 70), new Color(100, 80, 55) * 0.5f);
        string vs = "VS";
        Vector2 vsSize = _font.MeasureString(vs);
        SpriteBatch.DrawString(_font, vs,
            new Vector2(midX - vsSize.X / 2, panelY + panelH / 2 - vsSize.Y / 2),
            new Color(200, 180, 120));

        // 总兵力对比
        int totalP = _playerArmy.GetTotalMax();
        int totalE = _enemyArmy.GetTotalMax();
        string compText = $"总兵力  {totalP} : {totalE}";
        Vector2 compSize = _font.MeasureString(compText);
        SpriteBatch.DrawString(_font, compText,
            new Vector2(GameSettings.ScreenWidth / 2 - compSize.X / 2, panelY + panelH - 40),
            new Color(220, 200, 160));

        // 提示
        string hint = "点击「开战」开始战斗";
        Vector2 hintSize = _smallFont.MeasureString(hint);
        SpriteBatch.DrawString(_smallFont, hint,
            new Vector2(GameSettings.ScreenWidth / 2 - hintSize.X / 2, panelY + panelH + 10),
            new Color(150, 140, 120));

        _startButton?.Draw(SpriteBatch, _font, _pixel);
        _backButton?.Draw(SpriteBatch, _font, _pixel);
    }

    private void DrawCountdown()
    {
        int num = Math.Max(1, (int)Math.Ceiling(_countdownTimer));
        string text = num.ToString();
        Vector2 textSize = _titleFont.MeasureString(text);
        Vector2 pos = new Vector2(
            GameSettings.ScreenWidth / 2 - textSize.X / 2,
            GameSettings.ScreenHeight / 2 - textSize.Y / 2 - 30);

        // 描边
        for (int dx = -3; dx <= 3; dx++)
            for (int dy = -3; dy <= 3; dy++)
                SpriteBatch.DrawString(_titleFont, text, pos + new Vector2(dx, dy), new Color(0, 0, 0) * 0.5f);

        SpriteBatch.DrawString(_titleFont, text, pos, new Color(255, 230, 100));
    }

    private void DrawResult()
    {
        bool playerWon = !_playerArmy.IsDefeated();
        string resultText = playerWon ? "胜 利" : "败 北";
        Color resultColor = playerWon ? new Color(255, 220, 80) : new Color(220, 80, 80);

        // 半透明遮罩
        SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
            new Color(0, 0, 0) * 0.5f);

        // 结果文字
        Vector2 textSize = _titleFont.MeasureString(resultText);
        Vector2 pos = new Vector2(
            GameSettings.ScreenWidth / 2 - textSize.X / 2,
            GameSettings.ScreenHeight / 2 - textSize.Y / 2 - 40);
        SpriteBatch.DrawString(_titleFont, resultText, pos, resultColor);

        // 统计
        string stats = $"战斗时间: {(int)_battleTimer / 60:D2}:{(int)_battleTimer % 60:D2}  " +
                       $"我方剩余: {_playerArmy.GetTotalAlive()}/{_playerArmy.GetTotalMax()}  " +
                       $"敌方剩余: {_enemyArmy.GetTotalAlive()}/{_enemyArmy.GetTotalMax()}";
        Vector2 statsSize = _font.MeasureString(stats);
        SpriteBatch.DrawString(_font, stats,
            new Vector2(GameSettings.ScreenWidth / 2 - statsSize.X / 2, pos.Y + 60),
            new Color(200, 190, 170));

        _backButton?.Draw(SpriteBatch, _font, _pixel);
    }

    private void DrawRoundCommandUI()
    {
        // 半透明遮罩 (轻微暗化战场)
        SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
            new Color(0, 0, 0) * 0.15f);

        // 回合标题
        string roundText = $"第 {_roundManager.CurrentRound} 回合";
        Vector2 roundSize = _titleFont.MeasureString(roundText);
        SpriteBatch.DrawString(_titleFont, roundText,
            new Vector2(GameSettings.ScreenWidth / 2 - roundSize.X / 2, 60),
            new Color(255, 220, 130));

        // 指令提示 (脉冲动画)
        float pulse = 0.7f + 0.3f * (float)Math.Sin(_battleTimer * 3.0);
        string hintText = "选择武将释放技能 或 结束回合";
        Vector2 hintSize = _font.MeasureString(hintText);
        SpriteBatch.DrawString(_font, hintText,
            new Vector2(GameSettings.ScreenWidth / 2 - hintSize.X / 2, 95),
            new Color(200, 190, 160) * pulse);

        // 结束回合按钮
        _endTurnButton?.Draw(SpriteBatch, _font, _pixel);
    }
}
