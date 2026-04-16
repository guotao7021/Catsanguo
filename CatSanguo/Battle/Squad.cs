using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Core;
using CatSanguo.Core.Animation;
using CatSanguo.Generals;
using CatSanguo.Skills;
using CatSanguo.Data.Schemas;

namespace CatSanguo.Battle;

public enum SquadState
{
    Idle,
    Moving,
    Engaging,
    UsingSkill,
    Fleeing,
    Retreating,  // 新：HP归0后尝试撤退
    Dead
}

// 保留旧名用于向后兼容，实际使用 UnitType
public enum FormationType
{
    Vanguard = 0,   // 步兵
    Archer = 6,     // 弓兵
    Cavalry = 3    // 骑兵
}

public enum Team
{
    Player,
    Enemy
}

public class Squad
{
    public General? General { get; set; }

    // 军种系统（替代旧的FormationType语义）
    public UnitType UnitType { get; set; } = UnitType.Infantry;
    public UnitConfig? UnitConfig => UnitConfigTable.GetConfig(UnitType);

    // 战斗阵型
    public BattleFormation BattleFormation { get; set; } = BattleFormation.Vanguard;
    public FormationConfig? FormationConfig => FormationConfigTable.GetConfig(BattleFormation);

    // 保持向后兼容
    public FormationType Formation
    {
        get => UnitType switch
        {
            UnitType.Archer or UnitType.Crossbowman => FormationType.Archer,
            UnitType.Cavalry or UnitType.HeavyCavalry or UnitType.LightCavalry => FormationType.Cavalry,
            _ => FormationType.Vanguard
        };
        set
        {
            // 从旧格式转换
            UnitType = value switch
            {
                FormationType.Archer => UnitType.Archer,
                FormationType.Cavalry => UnitType.Cavalry,
                _ => UnitType.Infantry
            };
        }
    }

    public Team Team { get; set; }
    public SquadState State { get; set; } = SquadState.Idle;

    public Vector2 Position { get; set; }
    public float HP { get; set; }
    public float MaxHP { get; set; }
    public float Morale { get; set; } = 100f;
    public float BaseAttack { get; set; }
    public float BaseDefense { get; set; }
    public float BaseSpeed { get; set; }
    public float AttackRange { get; set; }
    public int SoldierCount { get; set; }
    public int MaxSoldierCount { get; set; }

    public Squad? TargetSquad { get; set; }
    public Skill? ActiveSkill { get; set; }
    public Skill? PassiveSkill { get; set; }

    // 羁绊和装备加成
    public Dictionary<string, float> BondBonuses { get; set; } = new();
    public float EquipmentAttackBonus { get; set; }
    public float EquipmentDefenseBonus { get; set; }
    public float EquipmentSpeedBonus { get; set; }
    public float EquipmentHPBonus { get; set; }

    public void ApplyBondBonuses(Dictionary<string, float> bonuses)
    {
        foreach (var kvp in bonuses)
        {
            if (!BondBonuses.ContainsKey(kvp.Key))
                BondBonuses[kvp.Key] = 0f;
            BondBonuses[kvp.Key] += kvp.Value;
        }
    }

    public void ApplyEquipmentBonuses(Generals.General general, List<Data.Schemas.EquipmentData> allEquipment)
    {
        float atkBonus = 0, defBonus = 0, spdBonus = 0, hpBonus = 0;
        
        foreach (var kvp in general.EquippedEquipment)
        {
            var equip = kvp.Value;
            if (equip == null) continue;
            
            // Equipment affects effective stats which are already in general.Effective*
            // We add a portion to combat stats
            float bonusValue = equip.StatBonus * 0.5f; // Scale down for balance
            
            switch (equip.StatType)
            {
                case "strength": atkBonus += bonusValue; break;
                case "intelligence": atkBonus += bonusValue * 0.8f; break;
                case "leadership": 
                    defBonus += bonusValue * 0.3f;
                    hpBonus += bonusValue * 5f;
                    break;
                case "speed": spdBonus += bonusValue * 0.5f; break;
            }
        }
        
        EquipmentAttackBonus = atkBonus;
        EquipmentDefenseBonus = defBonus;
        EquipmentSpeedBonus = spdBonus;
        EquipmentHPBonus = hpBonus;
    }

    // ==================== 军种和阵型加成 ====================

    /// <summary>获取军种攻击加成倍率</summary>
    public float GetUnitAttackMultiplier() => UnitConfig?.AttackMultiplier ?? 1.0f;

    /// <summary>获取军种防御加成倍率</summary>
    public float GetUnitDefenseMultiplier() => UnitConfig?.DefenseMultiplier ?? 1.0f;

    /// <summary>获取军种速度加成倍率</summary>
    public float GetUnitSpeedMultiplier() => UnitConfig?.SpeedMultiplier ?? 1.0f;

    /// <summary>获取军种HP加成倍率</summary>
    public float GetUnitHPMultiplier() => UnitConfig?.HPMultiplier ?? 1.0f;

    /// <summary>获取阵型防御加成</summary>
    public float GetFormationDefenseBonus() => FormationConfig?.DefenseBonus ?? 0f;

    /// <summary>获取阵型攻击加成</summary>
    public float GetFormationAttackBonus() => FormationConfig?.AttackBonus ?? 0f;

    /// <summary>获取阵型速度加成</summary>
    public float GetFormationSpeedBonus() => FormationConfig?.SpeedBonus ?? 0f;

    /// <summary>获取阵型减伤率</summary>
    public float GetFormationDamageReduction() => FormationConfig?.DamageReduction ?? 0f;

    /// <summary>是否可以穿透攻击后排</summary>
    public bool CanPierceBackline() => FormationConfig?.HasPierce == true || (UnitConfig?.CanPierce == true);

    /// <summary>是否可以造成AOE溅射</summary>
    public bool CanSplashDamage() => UnitConfig?.CanSplash == true;

    // Combat timers
    private float _attackTimer;
    private float _attackInterval = 1.0f;
    private float _skillCastTimer;
    private bool _isCasting;
    private bool _hasCharged; // For cavalry charge bonus

    // Visual
    public float FacingDirection { get; set; } = 1f; // 1 = right, -1 = left
    public List<Vector2> SoldierOffsets { get; private set; } = new();

    // Animation
    public Animator? GeneralAnimator { get; set; }
    public Animator? SoldierAnimator { get; set; }

    // Buffs (旧字段保留用于向后兼容，BuffSystem接管后逐渐移除)
    public float AttackBuffPercent { get; set; }
    public float DefenseBuffPercent { get; set; }
    public float SpeedBuffPercent { get; set; }
    public float BuffTimer { get; set; }

    // 新Buff系统状态
    public bool IsStunned { get; set; }
    public bool IsSilenced { get; set; }

    // 属性系统 (替代内联属性计算)
    public AttributeSet Attributes { get; set; } = new();

    // 战斗上下文引用 (通过此访问EventBus/BuffSystem等)
    public BattleContext? Context { get; set; }

    // Attack tracking for morale
    public int AttackersCount { get; set; }
    public float TimeSinceLastCombat { get; set; }

    // 属性代理 (通过Attributes系统计算，向后兼容所有调用方)
    public float EffectiveAttack
    {
        get => Attributes.GetValue(AttrType.Attack);
        set => Attributes.SetBase(AttrType.Attack, value);
    }
    public float EffectiveDefense
    {
        get => Attributes.GetValue(AttrType.Defense);
        set => Attributes.SetBase(AttrType.Defense, value);
    }
    public float EffectiveSpeed
    {
        get => Attributes.GetValue(AttrType.Speed);
        set => Attributes.SetBase(AttrType.Speed, value);
    }

    public bool IsDead => State == SquadState.Dead || State == SquadState.Retreating;
    public bool IsActive => State != SquadState.Dead && State != SquadState.Fleeing && State != SquadState.Retreating;

    public void InitializeSoldierOffsets()
    {
        SoldierOffsets.Clear();
        int count = Math.Min(SoldierCount, 12);
        switch (Formation)
        {
            case FormationType.Vanguard:
                // 3x3 or 3x4 grid
                for (int i = 0; i < count; i++)
                {
                    int row = i / 3;
                    int col = i % 3;
                    SoldierOffsets.Add(new Vector2((col - 1) * 14, (row - 1) * 14));
                }
                break;
            case FormationType.Archer:
                // 2 rows spread
                for (int i = 0; i < count; i++)
                {
                    int row = i / 5;
                    int col = i % 5;
                    SoldierOffsets.Add(new Vector2((col - 2) * 16, (row) * 16 + 10));
                }
                break;
            case FormationType.Cavalry:
                // V-wedge
                for (int i = 0; i < count; i++)
                {
                    int side = i % 2 == 0 ? 1 : -1;
                    int depth = i / 2;
                    SoldierOffsets.Add(new Vector2(depth * 12, side * (depth + 1) * 10));
                }
                break;
        }
    }

    public void Update(float deltaTime, List<Squad> allSquads)
    {
        if (State == SquadState.Dead || State == SquadState.Retreating) return;

        // Update buffs
        if (BuffTimer > 0)
        {
            BuffTimer -= deltaTime;
            if (BuffTimer <= 0)
            {
                AttackBuffPercent = 0;
                DefenseBuffPercent = 0;
                SpeedBuffPercent = 0;
            }
        }

        // Update skills
        ActiveSkill?.Update(deltaTime);

        // Update soldier visual count
        float hpRatio = HP / MaxHP;
        SoldierCount = Math.Max(1, (int)(MaxSoldierCount * hpRatio));

        // Track combat time
        if (State == SquadState.Engaging || State == SquadState.UsingSkill)
            TimeSinceLastCombat = 0;
        else
            TimeSinceLastCombat += deltaTime;

        switch (State)
        {
            case SquadState.Idle:
                UpdateIdle(allSquads);
                break;
            case SquadState.Moving:
                UpdateMoving(deltaTime, allSquads);
                break;
            case SquadState.Engaging:
                UpdateEngaging(deltaTime, allSquads);
                break;
            case SquadState.UsingSkill:
                UpdateUsingSkill(deltaTime);
                break;
            case SquadState.Fleeing:
                UpdateFleeing(deltaTime);
                break;
        }
    }

    private void UpdateIdle(List<Squad> allSquads)
    {
        // Find a target
        if (TargetSquad == null || TargetSquad.IsDead)
        {
            TargetSquad = FindNearestEnemy(allSquads);
        }
        if (TargetSquad != null)
        {
            State = SquadState.Moving;
        }
    }

    private void UpdateMoving(float deltaTime, List<Squad> allSquads)
    {
        if (TargetSquad == null || TargetSquad.IsDead)
        {
            TargetSquad = FindNearestEnemy(allSquads);
            if (TargetSquad == null)
            {
                State = SquadState.Idle;
                return;
            }
        }

        float dist = Vector2.Distance(Position, TargetSquad.Position);
        if (dist <= AttackRange)
        {
            State = SquadState.Engaging;
            // Cavalry charge bonus
            if (Formation == FormationType.Cavalry && !_hasCharged)
            {
                _hasCharged = true;
            }
            return;
        }

        Vector2 direction = Vector2.Normalize(TargetSquad.Position - Position);
        FacingDirection = direction.X >= 0 ? 1f : -1f;

        // Separation from friendly squads
        Vector2 separation = Vector2.Zero;
        foreach (var other in allSquads)
        {
            if (other == this || other.IsDead || other.Team != Team) continue;
            float d = Vector2.Distance(Position, other.Position);
            if (d < GameSettings.SquadSeparationDistance && d > 0)
            {
                separation += Vector2.Normalize(Position - other.Position) * (GameSettings.SeparationForce / d);
            }
        }

        Position += (direction * EffectiveSpeed + separation) * deltaTime;
    }

    private void UpdateEngaging(float deltaTime, List<Squad> allSquads)
    {
        if (TargetSquad == null || TargetSquad.IsDead)
        {
            TargetSquad = FindNearestEnemy(allSquads);
            if (TargetSquad == null)
            {
                State = SquadState.Idle;
                return;
            }
            State = SquadState.Moving;
            return;
        }

        float dist = Vector2.Distance(Position, TargetSquad.Position);
        if (dist > AttackRange * 1.5f)
        {
            State = SquadState.Moving;
            return;
        }

        FacingDirection = (TargetSquad.Position.X - Position.X) >= 0 ? 1f : -1f;

        _attackTimer += deltaTime;
        if (_attackTimer >= _attackInterval)
        {
            _attackTimer = 0;
            PerformAttack();
        }
    }

    private void PerformAttack()
    {
        if (TargetSquad == null) return;
        float damage = DamageCalculator.Calculate(this, TargetSquad, 1.0f);

        // Cavalry first charge bonus
        if (Formation == FormationType.Cavalry && _hasCharged)
        {
            damage *= 1.5f;
            _hasCharged = false;
        }

        TargetSquad.TakeDamage(damage);
    }

    public void TakeDamage(float damage)
    {
        float reducedDamage = damage * (100f / (100f + EffectiveDefense));
        HP -= reducedDamage;
        TimeSinceLastCombat = 0;

        if (HP <= 0)
        {
            HP = 0;
            // 新：进入撤退状态，而不是直接死亡
            State = SquadState.Retreating;
        }
    }

    public void UseSkill(List<Squad> targets)
    {
        if (ActiveSkill == null || !ActiveSkill.IsReady || State == SquadState.Dead) return;

        _isCasting = true;
        _skillCastTimer = ActiveSkill.CastTime;
        State = SquadState.UsingSkill;
        ActiveSkill.Activate();

        // Apply skill effects
        ApplySkillEffect(ActiveSkill, targets);
    }

    private void ApplySkillEffect(Skill skill, List<Squad> targets)
    {
        float baseStat = skill.StatBasis == "intelligence" && General != null
            ? General.Intelligence
            : (General?.Strength ?? 30);

        switch (skill.EffectType)
        {
            case "damage":
                foreach (var target in targets)
                {
                    float damage = baseStat * skill.Coefficient * MathHelper.Lerp(0.9f, 1.1f, (float)Random.Shared.NextDouble());
                    target.TakeDamage(damage);
                }
                break;
            case "buff":
                foreach (var target in targets)
                {
                    if (skill.BuffStat == "attack") target.AttackBuffPercent = skill.BuffPercent;
                    else if (skill.BuffStat == "defense") target.DefenseBuffPercent = skill.BuffPercent;
                    else if (skill.BuffStat == "speed") target.SpeedBuffPercent = skill.BuffPercent;
                    target.BuffTimer = skill.BuffDuration;
                }
                break;
            case "morale":
                foreach (var target in targets)
                {
                    target.Morale = MathHelper.Clamp(target.Morale + skill.MoraleChange, 0, 100);
                }
                break;
        }
    }

    private void UpdateUsingSkill(float deltaTime)
    {
        _skillCastTimer -= deltaTime;
        if (_skillCastTimer <= 0)
        {
            _isCasting = false;
            State = TargetSquad != null && !TargetSquad.IsDead ? SquadState.Engaging : SquadState.Idle;
        }
    }

    private void UpdateFleeing(float deltaTime)
    {
        // Flee toward own map edge
        float fleeX = Team == Team.Player ? -50 : GameSettings.ScreenWidth + 50;
        Vector2 fleeTarget = new Vector2(fleeX, Position.Y);
        Vector2 dir = Vector2.Normalize(fleeTarget - Position);
        Position += dir * EffectiveSpeed * 1.5f * deltaTime;

        // Remove when off screen
        if (Position.X < -60 || Position.X > GameSettings.ScreenWidth + 60)
        {
            State = SquadState.Dead;
        }
    }

    private Squad? FindNearestEnemy(List<Squad> allSquads)
    {
        Squad? nearest = null;
        float minDist = float.MaxValue;
        foreach (var s in allSquads)
        {
            if (s.Team == Team || !s.IsActive) continue;
            float d = Vector2.Distance(Position, s.Position);
            if (d < minDist)
            {
                minDist = d;
                nearest = s;
            }
        }
        return nearest;
    }
}
