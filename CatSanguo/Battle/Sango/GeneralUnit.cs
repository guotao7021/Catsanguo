using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Core;
using CatSanguo.Core.Animation;
using CatSanguo.Data.Schemas;
using CatSanguo.Generals;

namespace CatSanguo.Battle.Sango;

public class GeneralUnit
{
    // 武将数据
    public General General { get; }
    public Team Team { get; }
    public UnitType UnitType { get; set; } = UnitType.Infantry;

    // 士兵
    public List<Soldier> Soldiers { get; } = new();
    public int InitialSoldierCount { get; private set; }

    // 武将精灵
    public Vector2 GeneralPosition { get; set; }
    public Animator? GeneralAnimator { get; set; }
    public float GeneralFacing { get; set; } = 1f;

    // 战斗属性
    public float Morale { get; set; } = 100f;
    public GeneralUnitState State { get; set; } = GeneralUnitState.Deploying;

    // 已解析技能 (回合制技能系统)
    public List<ResolvedSkill> ResolvedSkills { get; set; } = new();

    // 被动技能属性
    public float AttackBuffMultiplier { get; set; } = 1.0f;
    public float DefenseBuffMultiplier { get; set; } = 1.0f;
    public float MoraleFloor { get; set; } = 0f;
    public float CritChance { get; set; } = 0f;
    public float DodgeChance { get; set; } = 0f;
    public float RegenPercent { get; set; } = 0f;
    public float SkillDamageBonus { get; set; } = 0f;

    // 武将自身HP (单挑用)
    public float GeneralHP { get; set; }
    public float GeneralMaxHP { get; set; }

    // 派生属性缓存
    private float _baseSoldierDamage;
    private float _baseSoldierSpeed;
    private float _baseSoldierMaxHP;

    // 击杀统计
    public int KillCount { get; set; }

    public GeneralUnit(General general, Team team, UnitType unitType)
    {
        General = general;
        Team = team;
        UnitType = unitType;
        GeneralFacing = team == Team.Player ? 1f : -1f;

        // 武将自身HP
        GeneralMaxHP = general.EffectiveStrength * 15 + general.EffectiveCommand * 8 + 1000;
        GeneralHP = GeneralMaxHP;

        // 计算基础派生属性
        CalcDerivedStats();
    }

    private void CalcDerivedStats()
    {
        float attack = General.EffectiveStrength * 2 + General.EffectiveCommand;
        float speed = 80f + General.EffectiveSpeed * 0.8f;
        float maxHP = General.EffectiveCommand * 10 + General.EffectiveStrength * 5 + 1500;

        // 军种修正
        float atkMul = 1f, spdMul = 1f, hpMul = 1f;
        switch (UnitType)
        {
            case UnitType.Infantry:
            case UnitType.ShieldInfantry:
                atkMul = 1.0f; spdMul = 1.0f; hpMul = 1.2f; break;
            case UnitType.Spearman:
                atkMul = 1.1f; spdMul = 0.95f; hpMul = 1.1f; break;
            case UnitType.Cavalry:
            case UnitType.HeavyCavalry:
            case UnitType.LightCavalry:
                atkMul = 1.2f; spdMul = 1.6f; hpMul = 0.9f; break;
            case UnitType.Archer:
            case UnitType.Crossbowman:
                atkMul = 1.0f; spdMul = 0.8f; hpMul = 0.8f; break;
        }

        _baseSoldierDamage = attack * atkMul;
        _baseSoldierSpeed = speed * spdMul;
        _baseSoldierMaxHP = maxHP * hpMul;
    }

    /// <summary>每个士兵的攻击伤害</summary>
    public float SoldierDamage
    {
        get
        {
            int alive = AliveSoldierCount;
            if (alive <= 0) return 0;
            return _baseSoldierDamage / InitialSoldierCount * 0.4f * AttackBuffMultiplier;
        }
    }

    /// <summary>士兵移动速度</summary>
    public float SoldierSpeed => _baseSoldierSpeed;

    /// <summary>存活士兵数</summary>
    public int AliveSoldierCount => Soldiers.Count(s => s.IsAlive);

    /// <summary>是否弓兵类</summary>
    public bool IsRanged => UnitType == UnitType.Archer || UnitType == UnitType.Crossbowman;

    /// <summary>是否已败</summary>
    public bool IsDefeated => State == GeneralUnitState.Defeated || AliveSoldierCount == 0;

    /// <summary>阵营色调 (蓝方/红方)</summary>
    public Color TeamTint => Team == Team.Player
        ? new Color(180, 200, 255)
        : new Color(255, 190, 180);

    /// <summary>士兵阵亡回调 - 降低己方士气</summary>
    public void OnSoldierKilled()
    {
        float moraleLoss = 100f / Math.Max(1, InitialSoldierCount) * 0.8f;
        Morale = Math.Max(MoraleFloor, Morale - moraleLoss);
    }

    /// <summary>创建士兵并排列阵型</summary>
    public void SpawnSoldiers(int count, SpriteSheetManager? spriteSheets)
    {
        InitialSoldierCount = count;
        float soldierMaxHP = _baseSoldierMaxHP / count;

        string spriteKey = UnitType switch
        {
            UnitType.Infantry => "soldier_infantry",
            UnitType.Spearman => "soldier_spearman",
            UnitType.ShieldInfantry => "soldier_shield",
            UnitType.Cavalry => "soldier_cavalry",
            UnitType.HeavyCavalry => "soldier_heavy_cavalry",
            UnitType.LightCavalry => "soldier_light_cavalry",
            UnitType.Archer => "soldier_archer",
            UnitType.Crossbowman => "soldier_crossbow",
            UnitType.Siege => "soldier_siege",
            UnitType.Mage => "soldier_mage",
            _ => "soldier_infantry"
        };

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = GetFormationOffset(i, count);
            var soldier = new Soldier(this, GeneralPosition + offset, soldierMaxHP);
            soldier.Animator = spriteSheets?.CreateAnimator(spriteKey);
            Soldiers.Add(soldier);
        }
    }

    private Vector2 GetFormationOffset(int index, int total)
    {
        float dir = GeneralFacing;
        switch (UnitType)
        {
            case UnitType.Archer:
            case UnitType.Crossbowman:
            {
                // 弓兵：2行横排
                int cols = (total + 1) / 2;
                int col = index % cols;
                int row = index / cols;
                float x = dir * (col * 20 + 45);
                float y = (row - 0.5f) * 25;
                return new Vector2(x, y);
            }
            case UnitType.Cavalry:
            case UnitType.HeavyCavalry:
            case UnitType.LightCavalry:
            {
                // 骑兵：V形楔形
                int half = (total + 1) / 2;
                int side = index < half ? 0 : 1;
                int pos = side == 0 ? index : index - half;
                float x = dir * (pos * 24 + 25);
                float y = (side == 0 ? -1 : 1) * (pos * 16 + 14);
                return new Vector2(x, y);
            }
            default:
            {
                // 步兵：3列×N行网格
                int cols = 3;
                int col = index % cols;
                int row = index / cols;
                float x = dir * (row * 22 + 35);
                float y = (col - 1) * 24;
                return new Vector2(x, y);
            }
        }
    }

    public void Update(float dt)
    {
        // 更新所有士兵
        foreach (var soldier in Soldiers)
        {
            soldier.Update(dt);
        }

        // 更新武将位置 (跟随士兵质心)
        UpdateGeneralPosition(dt);

        // 更新武将动画
        GeneralAnimator?.Update(dt);

        // 检查是否全军覆没
        if (AliveSoldierCount == 0 && State != GeneralUnitState.Defeated)
        {
            State = GeneralUnitState.Defeated;
        }

        // 士气过低时溃逃
        if (Morale < GameSettings.MoraleCritical && State == GeneralUnitState.InCombat)
        {
            State = GeneralUnitState.Retreating;
            foreach (var s in Soldiers.Where(s => s.IsAlive))
            {
                s.State = SoldierState.Charging;
                s.FacingDirection = Team == Team.Player ? -1f : 1f; // 反向逃跑
                s.Target = null;
            }
        }
    }

    private void UpdateGeneralPosition(float dt)
    {
        var aliveSoldiers = Soldiers.Where(s => s.IsAlive).ToList();
        if (aliveSoldiers.Count == 0) return;

        // 计算士兵质心
        Vector2 centroid = Vector2.Zero;
        foreach (var s in aliveSoldiers)
            centroid += s.Position;
        centroid /= aliveSoldiers.Count;

        // 武将在质心后方
        float behindOffset = Team == Team.Player ? -40f : 40f;
        Vector2 targetPos = centroid + new Vector2(behindOffset, 0);

        // 平滑插值
        GeneralPosition = Vector2.Lerp(GeneralPosition, targetPos, 5f * dt);

        // 更新动画
        string clip = State switch
        {
            GeneralUnitState.Charging => "Walk",
            GeneralUnitState.InCombat => "Idle",
            GeneralUnitState.CastingSkill => "Attack",
            GeneralUnitState.InDuel => "Attack",
            GeneralUnitState.Defeated => "Death",
            _ => "Idle"
        };
        GeneralAnimator?.Play(clip);
    }

    /// <summary>设置所有士兵进入冲锋状态</summary>
    public void StartCharge()
    {
        State = GeneralUnitState.Charging;
        foreach (var soldier in Soldiers.Where(s => s.IsAlive))
        {
            if (IsRanged)
            {
                // 弓兵不冲锋，原地等待
                soldier.State = SoldierState.Idle;
            }
            else
            {
                soldier.State = SoldierState.Charging;
            }
        }
    }

    /// <summary>切换到混战状态</summary>
    public void EnterMelee()
    {
        State = GeneralUnitState.InCombat;
    }

    /// <summary>强制撤退 (单挑败北/士气崩溃)</summary>
    public void ForceRetreat()
    {
        if (State == GeneralUnitState.Defeated) return;
        State = GeneralUnitState.Retreating;
        foreach (var s in Soldiers.Where(s => s.IsAlive))
        {
            s.State = SoldierState.Charging;
            s.FacingDirection = Team == Team.Player ? -1f : 1f; // 反向逃跑
            s.Target = null;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // 绘制所有士兵
        foreach (var soldier in Soldiers)
        {
            soldier.Draw(spriteBatch, pixel);
        }

        // 绘制武将 (更大的精灵)
        if (State != GeneralUnitState.Defeated)
        {
            DrawGeneral(spriteBatch, pixel);
        }
    }

    private void DrawGeneral(SpriteBatch spriteBatch, Texture2D pixel)
    {
        var effects = GeneralFacing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        if (GeneralAnimator != null && GeneralAnimator.HasTexture)
        {
            GeneralAnimator.Draw(spriteBatch, GeneralPosition, TeamTint, effects, scale: 1.0f);
        }
        else
        {
            // 后备色块
            Color color = Team == Team.Player
                ? new Color(40, 100, 200)
                : new Color(200, 40, 40);
            int size = 20;
            spriteBatch.Draw(pixel,
                new Rectangle((int)GeneralPosition.X - size / 2, (int)GeneralPosition.Y - size / 2, size, size),
                color);
        }
    }
}
