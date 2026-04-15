using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;

namespace CatSanguo.Battle;

/// <summary>
/// 战斗表现事件类型
/// </summary>
public enum CombatEventType
{
    None,
    Attack,          // 攻击
    Hit,             // 命中
    Crit,            // 暴击
    SkillCast,       // 技能释放
    SkillHit,        // 技能命中
    BuffAdd,         // Buff添加
    BuffRemove,      // Buff移除
    UnitDeath,       // 单位死亡
    Heal,            // 治疗
    Dodge,           // 闪避
    Block,           // 格挡
    UnitSpawn,       // 单位生成
}

/// <summary>
/// 战斗表现事件
/// </summary>
public struct CombatPresentationEvent
{
    public CombatEventType Type;
    public float Timestamp;
    public int SourceId;
    public int TargetId;
    public float Value;           // 伤害值或治疗值
    public bool IsCritical;
    public string SkillId;
    public string BuffId;
    public Vector2 Position;
    public Vector2 TargetPosition;
    public string Message;        // 自定义消息

    public static CombatPresentationEvent Create(CombatEventType type, int sourceId, int targetId, float value = 0)
    {
        return new CombatPresentationEvent
        {
            Type = type,
            Timestamp = 0,
            SourceId = sourceId,
            TargetId = targetId,
            Value = value,
            IsCritical = false
        };
    }
}

/// <summary>
/// 战斗表现系统 - 负责所有视觉和音效表现
/// </summary>
public class CombatPresentationSystem
{
    // 事件队列
    private Queue<CombatPresentationEvent> _eventQueue = new();
    private List<CombatPresentationEvent> _activeEvents = new();

    // 屏幕效果
    private float _screenShakeTime;
    private float _screenShakeIntensity;
    private float _screenFlashAlpha;
    private Color _screenFlashColor = Color.White;

    // 慢动作
    private float _timeScale = 1.0f;
    private float _targetTimeScale = 1.0f;
    private float _timeScaleRecoveryRate = 2.0f;

    // Hit Stop (命中停顿)
    private float _hitStopTime;
    private const float HitStopDuration = 0.05f;

    // 连击系统
    private int _comboCount;
    private float _comboTimer;
    private const float ComboTimeout = 2.0f;

    // 飘字管理
    private List<PresentationFloatingText> _floatingTexts = new();

    // VFX特效
    private List<VFXParticle> _particles = new();
    private int _particlePoolSize = 200;

    // UI引用
    private SpriteFontBase? _font;
    private SpriteFontBase? _bigFont;
    private Texture2D? _pixel;

    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase bigFont)
    {
        _pixel = pixel;
        _font = font;
        _bigFont = bigFont;

        // 预创建粒子池
        for (int i = 0; i < _particlePoolSize; i++)
        {
            _particles.Add(new VFXParticle());
        }
    }

    /// <summary>入队战斗事件</summary>
    public void EnqueueEvent(CombatPresentationEvent evt)
    {
        evt.Timestamp = _activeEvents.Count > 0 ? _activeEvents.Max(e => e.Timestamp) + 0.1f : 0f;
        _eventQueue.Enqueue(evt);
    }

    /// <summary>入队快捷方法</summary>
    public void OnAttack(int attackerId, int targetId) =>
        EnqueueEvent(CombatPresentationEvent.Create(CombatEventType.Attack, attackerId, targetId));

    public void OnHit(int attackerId, int targetId, float damage, bool isCrit = false)
    {
        var evt = CombatPresentationEvent.Create(CombatEventType.Hit, attackerId, targetId, damage);
        evt.IsCritical = isCrit;
        EnqueueEvent(evt);
    }

    public void OnSkillCast(int casterId, string skillId) =>
        EnqueueEvent(new CombatPresentationEvent { Type = CombatEventType.SkillCast, SourceId = casterId, SkillId = skillId });

    public void OnDeath(int unitId) =>
        EnqueueEvent(CombatPresentationEvent.Create(CombatEventType.UnitDeath, unitId, -1));

    public void OnBuff(int unitId, string buffId, bool added) =>
        EnqueueEvent(new CombatPresentationEvent { Type = added ? CombatEventType.BuffAdd : CombatEventType.BuffRemove, SourceId = unitId, BuffId = buffId });

    public void OnHeal(int unitId, float amount) =>
        EnqueueEvent(new CombatPresentationEvent { Type = CombatEventType.Heal, SourceId = unitId, Value = amount });

    public void OnDodge(int targetId) =>
        EnqueueEvent(CombatPresentationEvent.Create(CombatEventType.Dodge, -1, targetId));

    public void OnBlock(int targetId) =>
        EnqueueEvent(CombatPresentationEvent.Create(CombatEventType.Block, -1, targetId));

    /// <summary>添加飘字（公共方法供外部调用）</summary>
    public void AddFloatingText(string text, Vector2 position, Color color, float scale = 1.0f, bool isCrit = false)
    {
        _floatingTexts.Add(new PresentationFloatingText(text, position, color, scale, isCrit));
    }

    /// <summary>触发死亡特效</summary>
    public void TriggerDeathEffect(Vector2 position)
    {
        TriggerScreenShake(20f, 0.5f);
        TriggerSlowMotion(0.2f, 0.6f);
        // 死亡粒子效果
        SpawnDeathVFX(position);
    }

    /// <summary>触发技能特效</summary>
    public void TriggerSkillEffect(Vector2 position)
    {
        TriggerSlowMotion(0.3f, 0.8f);
        TriggerScreenShake(8f, 0.3f);
        // 技能粒子效果
        SpawnSkillVFXAtPosition(position);
    }

    /// <summary>触发慢动作</summary>
    public void TriggerSlowMotion(float scale, float duration)
    {
        _targetTimeScale = scale;
        _timeScaleRecoveryRate = (1.0f - scale) / duration;
    }

    /// <summary>获取实际时间缩放</summary>
    public float GetTimeScale() => _timeScale * (_hitStopTime > 0 ? 0f : 1f);

    /// <summary>处理屏幕震动</summary>
    public void TriggerScreenShake(float intensity, float duration)
    {
        _screenShakeIntensity = Math.Max(_screenShakeIntensity, intensity);
        _screenShakeTime = Math.Max(_screenShakeTime, duration);
    }

    /// <summary>触发屏幕闪白</summary>
    public void TriggerScreenFlash(Color color, float alpha)
    {
        _screenFlashColor = color;
        _screenFlashAlpha = alpha;
    }

    /// <summary>获取屏幕震动偏移</summary>
    public Vector2 GetScreenShakeOffset()
    {
        if (_screenShakeTime <= 0) return Vector2.Zero;
        return new Vector2(
            (float)(Random.Shared.NextDouble() - 0.5) * _screenShakeIntensity * 2,
            (float)(Random.Shared.NextDouble() - 0.5) * _screenShakeIntensity * 2
        );
    }

    /// <summary>更新表现系统</summary>
    public void Update(float deltaTime)
    {
        // Hit Stop
        if (_hitStopTime > 0)
        {
            _hitStopTime -= deltaTime;
            return;
        }

        // 时间缩放恢复
        if (_timeScale < _targetTimeScale)
            _timeScale = Math.Min(_timeScale + _timeScaleRecoveryRate * deltaTime, _targetTimeScale);
        else if (_timeScale > _targetTimeScale)
            _timeScale = Math.Max(_timeScale - _timeScaleRecoveryRate * deltaTime, _targetTimeScale);

        // 处理事件队列
        while (_eventQueue.Count > 0)
        {
            var evt = _eventQueue.Dequeue();
            ProcessEvent(evt);
        }

        // 更新屏幕震动
        if (_screenShakeTime > 0)
            _screenShakeTime -= deltaTime;

        // 更新屏幕闪白
        if (_screenFlashAlpha > 0)
            _screenFlashAlpha -= deltaTime * 3;

        // 更新连击
        if (_comboTimer > 0)
        {
            _comboTimer -= deltaTime;
            if (_comboTimer <= 0)
                _comboCount = 0;
        }

        // 更新飘字
        for (int i = _floatingTexts.Count - 1; i >= 0; i--)
        {
            _floatingTexts[i].Update(deltaTime);
            if (_floatingTexts[i].IsExpired)
                _floatingTexts.RemoveAt(i);
        }

        // 更新粒子
        foreach (var p in _particles.Where(p => p.IsActive))
        {
            p.Update(deltaTime);
        }
    }

    private void ProcessEvent(CombatPresentationEvent evt)
    {
        switch (evt.Type)
        {
            case CombatEventType.Attack:
                // 播放攻击特效
                SpawnAttackVFX(evt.SourceId, evt.TargetId);
                break;

            case CombatEventType.Hit:
                // Hit Stop
                _hitStopTime = evt.IsCritical ? HitStopDuration * 1.5f : HitStopDuration;

                // 屏幕震动
                float shakeIntensity = evt.IsCritical ? 12f : 5f;
                TriggerScreenShake(shakeIntensity, evt.IsCritical ? 0.3f : 0.15f);

                // 闪白
                if (evt.IsCritical)
                    TriggerScreenFlash(Color.White, 0.4f);

                // 伤害飘字
                ShowDamageText(evt.TargetPosition, evt.Value, evt.IsCritical);

                // 击杀提示
                if (evt.Value > 200)
                    TriggerScreenShake(15f, 0.4f);

                _comboCount++;
                _comboTimer = ComboTimeout;
                break;

            case CombatEventType.SkillCast:
                // 技能释放慢动作
                TriggerSlowMotion(0.3f, 0.8f);
                TriggerScreenShake(8f, 0.3f);
                ShowSkillText(evt.SourceId, evt.SkillId);
                SpawnSkillVFX(evt.SourceId, evt.SkillId);
                break;

            case CombatEventType.UnitDeath:
                // 死亡震动
                TriggerScreenShake(20f, 0.5f);
                TriggerSlowMotion(0.2f, 0.6f);
                ShowDeathText(evt.SourceId);
                break;

            case CombatEventType.Heal:
                ShowHealText(evt.SourceId, evt.Value);
                SpawnHealVFX(evt.SourceId);
                break;

            case CombatEventType.Dodge:
                ShowDodgeText(evt.TargetId);
                SpawnDodgeVFX(evt.TargetId);
                break;

            case CombatEventType.Block:
                ShowBlockText(evt.TargetId);
                TriggerScreenShake(3f, 0.1f);
                break;
        }
    }

    private void ShowDamageText(Vector2 position, float value, bool isCrit)
    {
        string text = $"-{(int)value}";
        Color color = isCrit ? new Color(255, 80, 80) : Color.White;
        float scale = isCrit ? 1.5f : 1.0f;

        _floatingTexts.Add(new PresentationFloatingText(text, position, color, scale, isCrit));
    }

    private void ShowHealText(int unitId, float value)
    {
        string text = $"+{(int)value}";
        _floatingTexts.Add(new PresentationFloatingText(text, Vector2.Zero, new Color(100, 255, 100), 1.2f));
    }

    private void ShowSkillText(int unitId, string skillId)
    {
        _floatingTexts.Add(new PresentationFloatingText($"【{skillId}】", Vector2.Zero, new Color(255, 200, 100), 1.5f));
    }

    private void ShowDeathText(int unitId)
    {
        _floatingTexts.Add(new PresentationFloatingText("击杀!", Vector2.Zero, Color.Yellow, 2.0f));
    }

    private void ShowDodgeText(int unitId)
    {
        _floatingTexts.Add(new PresentationFloatingText("闪避", Vector2.Zero, new Color(200, 200, 255), 1.0f));
    }

    private void ShowBlockText(int unitId)
    {
        _floatingTexts.Add(new PresentationFloatingText("格挡", Vector2.Zero, new Color(180, 180, 180), 1.0f));
    }

    private void SpawnAttackVFX(int sourceId, int targetId)
    {
        // 获取粒子并激活
        var particle = _particles.FirstOrDefault(p => !p.IsActive);
        if (particle != null)
        {
            particle.Activate(VFXType.Slash, Vector2.Zero, Vector2.Zero, 0.3f);
        }
    }

    private void SpawnSkillVFX(int sourceId, string skillId)
    {
        var particle = _particles.FirstOrDefault(p => !p.IsActive);
        if (particle != null)
        {
            particle.Activate(VFXType.Explosion, Vector2.Zero, Vector2.Zero, 0.8f);
        }
    }

    private void SpawnHealVFX(int unitId)
    {
        var particle = _particles.FirstOrDefault(p => !p.IsActive);
        if (particle != null)
        {
            particle.Activate(VFXType.Heal, Vector2.Zero, Vector2.Zero, 0.6f);
        }
    }

    private void SpawnDodgeVFX(int unitId)
    {
        // 闪避特效
    }

    private void SpawnDeathVFX(Vector2 position)
    {
        // 死亡爆炸粒子效果
        for (int i = 0; i < 20; i++)
        {
            var particle = _particles.FirstOrDefault(p => !p.IsActive);
            if (particle != null)
            {
                float angle = (float)(i / 20.0 * Math.PI * 2);
                float speed = 80f + Random.Shared.NextSingle() * 60f;
                var velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
                particle.Activate(VFXType.Explosion, position, velocity, 0.8f);
            }
        }
    }

    private void SpawnSkillVFXAtPosition(Vector2 position)
    {
        // 技能光环粒子效果
        for (int i = 0; i < 15; i++)
        {
            var particle = _particles.FirstOrDefault(p => !p.IsActive);
            if (particle != null)
            {
                float angle = (float)(i / 15.0 * Math.PI * 2);
                float speed = 50f + Random.Shared.NextSingle() * 40f;
                var velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
                particle.Activate(VFXType.Skill, position, velocity, 1.0f);
            }
        }
    }

    public void Draw(SpriteBatch sb)
    {
        if (_font == null || _bigFont == null || _pixel == null) return;

        // 绘制飘字
        foreach (var text in _floatingTexts)
        {
            text.Draw(sb, _bigFont);
        }

        // 绘制粒子
        foreach (var p in _particles.Where(p => p.IsActive))
        {
            p.Draw(sb, _pixel);
        }

        // 绘制屏幕闪白
        if (_screenFlashAlpha > 0)
        {
            sb.Draw(_pixel,
                new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
                _screenFlashColor * _screenFlashAlpha);
        }

        // 绘制连击
        if (_comboCount > 1)
        {
            DrawComboUI(sb);
        }
    }

    private void DrawComboUI(SpriteBatch sb)
    {
        if (_font == null || _pixel == null) return;

        // 连击框
        Rectangle comboPanel = new Rectangle(GameSettings.ScreenWidth - 150, 10, 140, 70);
        sb.Draw(_pixel, comboPanel, new Color((byte)40, (byte)30, (byte)20, (byte)200));

        // 连击数字
        string comboText = $"{_comboCount}";
        var size = _bigFont!.MeasureString(comboText);
        sb.DrawString(_bigFont, comboText,
            new Vector2(comboPanel.X + comboPanel.Width / 2 - size.X / 2, comboPanel.Y + 5),
            _comboCount >= 5 ? Color.Yellow : Color.White);

        // 连击标签
        string label = _comboCount >= 10 ? "大爆发!" : "连击";
        sb.DrawString(_font, label, new Vector2(comboPanel.X + 35, comboPanel.Y + 45), new Color((byte)200, (byte)180, (byte)140));
    }
}

/// <summary>
/// VFX粒子类型
/// </summary>
public enum VFXType
{
    Slash,      // 刀光
    Impact,     // 冲击
    Explosion,  // 爆炸
    Heal,       // 治疗
    Buff,       // Buff光环
    Death,      // 死亡特效
    Skill       // 技能特效
}

/// <summary>
/// VFX粒子
/// </summary>
public class VFXParticle
{
    public bool IsActive { get; private set; }
    public VFXType Type { get; private set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Life { get; private set; }
    public float MaxLife { get; private set; }
    public float Scale { get; set; } = 1f;
    public Color Color { get; set; } = Color.White;

    public void Activate(VFXType type, Vector2 pos, Vector2 vel, float duration)
    {
        Type = type;
        Position = pos;
        Velocity = vel;
        MaxLife = duration;
        Life = duration;
        IsActive = true;
        Scale = 1f;
        Color = type switch
        {
            VFXType.Slash => new Color(255, 200, 100),
            VFXType.Impact => new Color(255, 150, 50),
            VFXType.Explosion => new Color(255, 100, 50),
            VFXType.Heal => new Color(100, 255, 100),
            VFXType.Buff => new Color(100, 150, 255),
            VFXType.Death => new Color(150, 50, 50),
            _ => Color.White
        };
    }

    public void Update(float deltaTime)
    {
        if (!IsActive) return;
        Life -= deltaTime;
        if (Life <= 0)
        {
            IsActive = false;
            return;
        }
        Position += Velocity * deltaTime;
        Scale = 1f + (1f - Life / MaxLife) * 0.5f;
    }

    public float Alpha => Life / MaxLife;

    public void Draw(SpriteBatch sb, Texture2D pixel)
    {
        if (!IsActive) return;
        int size = (int)(30 * Scale);
        var rect = new Rectangle((int)Position.X - size / 2, (int)Position.Y - size / 2, size, size);
        sb.Draw(pixel, rect, Color * Alpha);
    }
}

/// <summary>
/// 增强的飘字系统
/// </summary>
public class PresentationFloatingText
{
    public string Text { get; }
    public Vector2 BasePosition { get; }
    public Color BaseColor { get; }
    public float BaseScale { get; }
    public bool IsCrit { get; }

    private float _life;
    private const float Duration = 1.2f;
    private const float CritDuration = 1.8f;

    public PresentationFloatingText(string text, Vector2 position, Color color, float scale, bool isCrit = false)
    {
        Text = text;
        BasePosition = position + new Vector2(
            (float)(Random.Shared.NextDouble() - 0.5) * 20,
            (float)(Random.Shared.NextDouble() - 0.5) * 10
        );
        BaseColor = color;
        BaseScale = scale;
        IsCrit = isCrit;
        _life = isCrit ? CritDuration : Duration;
    }

    public bool IsExpired => _life <= 0;

    public void Update(float deltaTime)
    {
        _life -= deltaTime;
    }

    public float Alpha => Math.Clamp(_life / (IsCrit ? CritDuration : Duration), 0, 1);
    public float Scale => BaseScale * (IsCrit ? (1f + (1f - Alpha) * 0.3f) : 1f);

    // 飘动路径
    private const float FloatSpeed = 60f;

    public Vector2 GetPosition(float elapsed)
    {
        float t = 1f - (elapsed / (IsCrit ? CritDuration : Duration));
        return BasePosition - new Vector2(0, FloatSpeed * elapsed);
    }

    public void Draw(SpriteBatch sb, SpriteFontBase font)
    {
        float t = 1f - (_life / (IsCrit ? CritDuration : Duration));
        float alpha = Alpha;
        float scale = Scale;

        Vector2 pos = BasePosition - new Vector2(0, FloatSpeed * (IsCrit ? CritDuration : Duration) * t);

        // 黑色描边
        Color outlineColor = new Color((byte)0, (byte)0, (byte)0, (byte)(alpha * 200));
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
                sb.DrawString(font, Text, pos + new Vector2(dx, dy), outlineColor * alpha, 0f, Vector2.Zero, new Vector2(scale), 0f);

        // 主文字
        sb.DrawString(font, Text, pos, BaseColor * alpha, 0f, Vector2.Zero, new Vector2(scale), 0f);
    }
}
