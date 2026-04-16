using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Core;
using CatSanguo.Core.Animation;

namespace CatSanguo.Battle.Sango;

public class Soldier
{
    public Vector2 Position { get; set; }
    public float HP { get; set; }
    public float MaxHP { get; set; }
    public SoldierState State { get; set; } = SoldierState.Idle;
    public Soldier? Target { get; set; }
    public float AttackTimer { get; set; }
    public Animator? Animator { get; set; }
    public float FacingDirection { get; set; } = 1f; // 1=右, -1=左
    public GeneralUnit Owner { get; }
    public bool HasChargeBonus { get; set; } = true; // 骑兵首次冲锋标记

    // 投射物发射回调 (由Scene层连接到ProjectileManager)
    public Action<Projectile>? OnProjectileFired;

    // 受击VFX回调 (由Scene层连接到BattleVFXSystem)
    public Action<Vector2>? OnHitVFX;

    // 死亡动画计时
    private float _dyingTimer;
    private const float DyingDuration = 0.6f;

    // 淡出透明度
    public float Alpha { get; private set; } = 1f;

    // 受击闪烁
    private float _hitFlashTimer;
    private const float HitFlashDuration = 0.15f;

    public bool IsAlive => State != SoldierState.Dead && State != SoldierState.Dying;
    public bool IsDead => State == SoldierState.Dead;

    public Soldier(GeneralUnit owner, Vector2 position, float maxHP)
    {
        Owner = owner;
        Position = position;
        MaxHP = maxHP;
        HP = maxHP;
        FacingDirection = owner.Team == Team.Player ? 1f : -1f;
    }

    public void Update(float dt)
    {
        // 受击闪烁倒计时
        if (_hitFlashTimer > 0) _hitFlashTimer -= dt;

        switch (State)
        {
            case SoldierState.Idle:
                Animator?.Play("Idle");
                break;

            case SoldierState.Charging:
                UpdateCharging(dt);
                break;

            case SoldierState.Fighting:
                UpdateFighting(dt);
                break;

            case SoldierState.Shooting:
                UpdateShooting(dt);
                break;

            case SoldierState.Dying:
                UpdateDying(dt);
                break;

            case SoldierState.Dead:
                break;
        }

        Animator?.Update(dt);
    }

    private void UpdateCharging(float dt)
    {
        Animator?.Play("Walk");
        float speed = Owner.SoldierSpeed;
        Position += new Vector2(FacingDirection * speed * dt, 0);

        // 添加轻微Y方向散开 (避免所有士兵挤在一条线上)
        float yNoise = (float)(Math.Sin(Position.X * 0.05f + GetHashCode() * 0.1f) * 0.3f);
        Position += new Vector2(0, yNoise * dt);
    }

    private void UpdateFighting(float dt)
    {
        // 目标已死亡，清空目标
        if (Target != null && !Target.IsAlive)
        {
            Target = null;
            AttackTimer = 0;
        }

        if (Target == null)
        {
            Animator?.Play("Idle");
            return;
        }

        // 向目标微调位置
        float dist = Vector2.Distance(Position, Target.Position);
        if (dist > GameSettings.SangoCollisionRadius * 1.5f)
        {
            Vector2 dir = Vector2.Normalize(Target.Position - Position);
            Position += dir * Owner.SoldierSpeed * 0.5f * dt;
            FacingDirection = dir.X >= 0 ? 1f : -1f;
            Animator?.Play("Walk");
        }
        else
        {
            Animator?.Play("Attack");
            FacingDirection = (Target.Position.X - Position.X) >= 0 ? 1f : -1f;
        }

        // 攻击计时
        AttackTimer += dt;
        if (AttackTimer >= GameSettings.SangoAttackInterval)
        {
            AttackTimer -= GameSettings.SangoAttackInterval;
            PerformAttack();
        }
    }

    private void UpdateShooting(float dt)
    {
        if (Target != null && !Target.IsAlive)
        {
            Target = null;
            AttackTimer = 0;
        }

        if (Target == null)
        {
            Animator?.Play("Idle");
            return;
        }

        FacingDirection = (Target.Position.X - Position.X) >= 0 ? 1f : -1f;
        Animator?.Play("Attack");

        AttackTimer += dt;
        if (AttackTimer >= GameSettings.SangoAttackInterval * 1.2f) // 弓兵射速稍慢
        {
            AttackTimer -= GameSettings.SangoAttackInterval * 1.2f;
            FireProjectile();
        }
    }

    private void PerformAttack()
    {
        if (Target == null || !Target.IsAlive) return;

        float damage = CalcDamage();
        Target.TakeDamage(damage);
    }

    private void FireProjectile()
    {
        if (Target == null || !Target.IsAlive) return;

        float damage = CalcDamage();
        var projectile = new Projectile(Position, Target, damage);
        OnProjectileFired?.Invoke(projectile);
    }

    private float CalcDamage()
    {
        float damage = Owner.SoldierDamage;

        // 兵种克制
        if (Target != null)
        {
            float counterMod = Data.Schemas.UnitCounterConfig.GetCounterMultiplier(
                Owner.UnitType, Target.Owner.UnitType);
            damage *= counterMod;
        }

        // 士气修正
        float moraleMod = MathHelper.Lerp(0.5f, 1.0f, Owner.Morale / 100f);
        damage *= moraleMod;

        // 随机波动
        damage *= 0.8f + (float)Random.Shared.NextDouble() * 0.4f;

        // 骑兵首次冲锋加成
        if (HasChargeBonus && (Owner.UnitType == Data.Schemas.UnitType.Cavalry
            || Owner.UnitType == Data.Schemas.UnitType.HeavyCavalry
            || Owner.UnitType == Data.Schemas.UnitType.LightCavalry))
        {
            damage *= 1.5f;
            HasChargeBonus = false;
        }

        // 暴击判定
        if (Owner.CritChance > 0 && (float)Random.Shared.NextDouble() < Owner.CritChance)
        {
            damage *= 1.5f;
        }

        return Math.Max(1f, damage);
    }

    public void TakeDamage(float damage)
    {
        if (!IsAlive) return;

        // 闪避判定
        if (Owner.DodgeChance > 0 && (float)Random.Shared.NextDouble() < Owner.DodgeChance)
            return;

        // 防御加成减伤
        if (Owner.DefenseBuffMultiplier > 1f)
            damage /= Owner.DefenseBuffMultiplier;

        HP -= damage;
        _hitFlashTimer = HitFlashDuration;

        // 受击VFX
        OnHitVFX?.Invoke(Position);

        if (HP <= 0)
        {
            HP = 0;
            State = SoldierState.Dying;
            _dyingTimer = DyingDuration;
            Animator?.Play("Death");

            // 士兵阵亡导致己方士气下降
            Owner.OnSoldierKilled();
        }
    }

    private void UpdateDying(float dt)
    {
        _dyingTimer -= dt;
        Alpha = Math.Max(0, _dyingTimer / DyingDuration);
        if (_dyingTimer <= 0 || (Animator != null && Animator.IsFinished))
        {
            State = SoldierState.Dead;
            Alpha = 0;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D? shadowTex = null)
    {
        if (State == SoldierState.Dead) return;

        var effects = FacingDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Color teamTint = _hitFlashTimer > 0 ? Color.Red : Owner.TeamTint;

        // 1. 阴影
        if (shadowTex != null)
        {
            spriteBatch.Draw(shadowTex,
                new Vector2(Position.X, Position.Y + 12),
                null, Color.White * 0.5f * Alpha, 0f,
                new Vector2(shadowTex.Width / 2f, shadowTex.Height / 2f),
                0.8f, SpriteEffects.None, 0f);
        }

        // 2. 角色精灵
        if (Animator != null && Animator.HasTexture)
        {
            Animator.Draw(spriteBatch, Position, teamTint * Alpha, effects, scale: 1.0f);
        }
        else
        {
            // 后备色块
            Color color = Owner.Team == Team.Player
                ? new Color(60, 120, 220)
                : new Color(220, 50, 50);
            if (_hitFlashTimer > 0) color = Color.Red;
            int size = 10;
            spriteBatch.Draw(pixel,
                new Rectangle((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size),
                color * Alpha);
        }

        // 3. 血条 (HP<70%时显示)
        float hpRatio = MaxHP > 0 ? HP / MaxHP : 1f;
        if (hpRatio < 0.7f && hpRatio > 0f)
        {
            int barW = 10, barH = 1;
            int barX = (int)Position.X - barW / 2;
            int barY = (int)Position.Y - 18;
            // 底条
            spriteBatch.Draw(pixel, new Rectangle(barX, barY, barW, barH), new Color(40, 35, 30) * Alpha);
            // 血量
            Color hpColor = hpRatio > 0.4f ? new Color(80, 200, 80) : new Color(220, 50, 50);
            spriteBatch.Draw(pixel, new Rectangle(barX, barY, (int)(barW * hpRatio), barH), hpColor * Alpha);
        }
    }
}
