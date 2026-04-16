using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.Battle.Sango;

public class Projectile
{
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Damage { get; set; }
    public Soldier? Target { get; set; }
    public float Lifetime { get; set; }
    public bool IsExpired { get; set; }

    // 拖尾 (存储前2帧位置)
    private Vector2 _trail0;
    private Vector2 _trail1;

    private const float Speed = 400f;
    private const float HitRadius = 10f;

    public Projectile(Vector2 startPos, Soldier target, float damage)
    {
        Position = startPos;
        Target = target;
        Damage = damage;
        Lifetime = 3f;
        _trail0 = startPos;
        _trail1 = startPos;

        // 初始朝目标方向发射
        Vector2 dir = Vector2.Normalize(target.Position - startPos);
        Velocity = dir * Speed;
    }

    public void Update(float dt)
    {
        if (IsExpired) return;

        Lifetime -= dt;
        if (Lifetime <= 0)
        {
            IsExpired = true;
            return;
        }

        // 更新拖尾
        _trail1 = _trail0;
        _trail0 = Position;

        // 追踪目标
        if (Target != null && Target.IsAlive)
        {
            Vector2 dir = Vector2.Normalize(Target.Position - Position);
            Velocity = dir * Speed;
        }

        Position += Velocity * dt;

        // 命中检测
        if (Target != null && Target.IsAlive)
        {
            float dist = Vector2.Distance(Position, Target.Position);
            if (dist < HitRadius)
            {
                Target.TakeDamage(Damage);
                IsExpired = true;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D? arrowTex = null)
    {
        if (IsExpired) return;

        // 计算旋转角度
        float rotation = MathF.Atan2(Velocity.Y, Velocity.X);

        if (arrowTex != null)
        {
            // 拖尾 (前2帧半透明)
            var origin = new Vector2(arrowTex.Width / 2f, arrowTex.Height / 2f);
            spriteBatch.Draw(arrowTex, _trail1, null, Color.White * 0.2f,
                rotation, origin, 0.8f, SpriteEffects.None, 0f);
            spriteBatch.Draw(arrowTex, _trail0, null, Color.White * 0.5f,
                rotation, origin, 0.9f, SpriteEffects.None, 0f);
            // 箭矢主体
            spriteBatch.Draw(arrowTex, Position, null, Color.White,
                rotation, origin, 1.0f, SpriteEffects.None, 0f);
        }
        else
        {
            // 后备: 旋转矩形
            var color = new Color(180, 150, 100);
            var origin = new Vector2(4, 1);
            spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1), color,
                rotation, Vector2.Zero, new Vector2(8, 2), SpriteEffects.None, 0f);
        }
    }
}

public class ProjectileManager
{
    private readonly List<Projectile> _projectiles = new();

    public void Add(Projectile p)
    {
        _projectiles.Add(p);
    }

    public void Update(float dt)
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            _projectiles[i].Update(dt);
            if (_projectiles[i].IsExpired)
                _projectiles.RemoveAt(i);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D? arrowTex = null)
    {
        foreach (var p in _projectiles)
        {
            p.Draw(spriteBatch, pixel, arrowTex);
        }
    }

    public void Clear()
    {
        _projectiles.Clear();
    }
}
