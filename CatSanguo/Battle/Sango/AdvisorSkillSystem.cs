using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;

namespace CatSanguo.Battle.Sango;

/// <summary>
/// 军师技系统 - 三国群英传2风格全屏AOE技能
/// </summary>
public class AdvisorSkillSystem
{
    public enum SkillPhase { Idle, Casting, Animating, Done }

    public SkillPhase Phase { get; private set; } = SkillPhase.Idle;
    public bool IsActive => Phase != SkillPhase.Idle;

    // 施法
    private string _skillName = "";
    private GeneralUnit? _caster;
    private ArmyGroup? _targetArmy;
    private AdvisorSkillType _skillType;
    private float _castTimer;
    private float _animTimer;
    private BattleVFXSystem? _vfx;

    // 时间常量
    private const float CastDuration = 1.5f;  // 施法前摇
    private const float AnimDuration = 2.0f;   // 效果动画时长

    // 回合冷却
    public int CooldownRoundsLeft { get; set; }
    private const int CooldownRoundsTotal = 5;

    // 回调
    public Action? OnComplete;

    public void Cast(GeneralUnit caster, ArmyGroup targetArmy, AdvisorSkillType skillType,
                     BattleVFXSystem vfx)
    {
        _caster = caster;
        _targetArmy = targetArmy;
        _skillType = skillType;
        _skillName = GetSkillName(skillType);
        _vfx = vfx;
        _castTimer = CastDuration;
        _animTimer = AnimDuration;
        Phase = SkillPhase.Casting;
        CooldownRoundsLeft = CooldownRoundsTotal;
    }

    /// <summary>回合结束时递减冷却</summary>
    public void TickCooldown()
    {
        if (CooldownRoundsLeft > 0)
            CooldownRoundsLeft--;
    }

    public void Update(float dt)
    {
        if (!IsActive) return;

        switch (Phase)
        {
            case SkillPhase.Casting:
                _castTimer -= dt;
                if (_castTimer <= 0)
                {
                    ApplyEffect();
                    Phase = SkillPhase.Animating;
                }
                break;

            case SkillPhase.Animating:
                _animTimer -= dt;
                if (_animTimer <= 0)
                {
                    Phase = SkillPhase.Idle;
                    OnComplete?.Invoke();
                }
                break;
        }
    }

    private void ApplyEffect()
    {
        if (_targetArmy == null || _vfx == null) return;

        var targets = _targetArmy.GetAllAliveSoldiers();
        if (targets.Count == 0) return;

        float baseDamage = _caster != null ? _caster.General.EffectiveIntelligence * 0.3f + 15 : 20;

        switch (_skillType)
        {
            case AdvisorSkillType.FireRain:
                // 火计 - 大范围火焰伤害
                foreach (var s in targets)
                {
                    float dmg = baseDamage * (0.8f + (float)Random.Shared.NextDouble() * 0.4f);
                    s.TakeDamage(dmg);
                    _vfx.SpawnFireEffect(s.Position, 8);
                }
                _vfx.ScreenFlash(new Color(255, 120, 30), 0.3f);
                break;

            case AdvisorSkillType.Lightning:
                // 落雷 - 随机打击多个目标
                int strikes = Math.Min(8, targets.Count);
                var shuffled = targets.OrderBy(_ => Random.Shared.Next()).Take(strikes).ToList();
                foreach (var s in shuffled)
                {
                    float dmg = baseDamage * 1.5f * (0.8f + (float)Random.Shared.NextDouble() * 0.4f);
                    s.TakeDamage(dmg);
                    _vfx.SpawnLightningEffect(s.Position);
                    _vfx.AddDamageText(s.Position, (int)dmg, true);
                }
                break;

            case AdvisorSkillType.IceStorm:
                // 冰冻 - 伤害并降低士气
                foreach (var s in targets)
                {
                    float dmg = baseDamage * 0.6f * (0.8f + (float)Random.Shared.NextDouble() * 0.4f);
                    s.TakeDamage(dmg);
                    _vfx.SpawnIceEffect(s.Position, 6);
                }
                // 降低目标军团士气
                foreach (var unit in _targetArmy.Units.Where(u => !u.IsDefeated))
                {
                    unit.Morale = Math.Max(0, unit.Morale - 15);
                }
                _vfx.ScreenFlash(new Color(100, 150, 255), 0.25f);
                break;

            case AdvisorSkillType.HealingWind:
                // 治疗 - 恢复己方士兵HP
                if (_caster != null)
                {
                    var friendlies = _caster.Team == Team.Player
                        ? _targetArmy.GetAllAliveSoldiers() // 此时targetArmy应该是己方
                        : _targetArmy.GetAllAliveSoldiers();
                    foreach (var s in friendlies)
                    {
                        float heal = baseDamage * 0.5f;
                        s.HP = Math.Min(s.MaxHP, s.HP + heal);
                        _vfx.AddHealText(s.Position, (int)heal);
                    }
                }
                _vfx.ScreenFlash(new Color(100, 255, 150), 0.2f);
                break;
        }
    }

    // ==================== Draw ====================

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont)
    {
        if (!IsActive) return;

        if (Phase == SkillPhase.Casting)
        {
            // 施法提示
            float alpha = Math.Min(1f, (CastDuration - _castTimer) / 0.5f);

            // 半透明遮罩
            sb.Draw(pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
                new Color(0, 0, 0) * 0.3f * alpha);

            // 技能名称
            Vector2 nameSize = titleFont.MeasureString(_skillName);
            sb.DrawString(titleFont, _skillName,
                new Vector2(GameSettings.ScreenWidth / 2 - nameSize.X / 2,
                            GameSettings.ScreenHeight / 2 - nameSize.Y / 2 - 20),
                GetSkillColor(_skillType) * alpha);

            // 施法者名字
            if (_caster != null)
            {
                string casterText = $"{_caster.General.Name} 施法中...";
                Vector2 cSize = font.MeasureString(casterText);
                sb.DrawString(font, casterText,
                    new Vector2(GameSettings.ScreenWidth / 2 - cSize.X / 2,
                                GameSettings.ScreenHeight / 2 + 20),
                    new Color(200, 190, 170) * alpha);
            }
        }
    }

    private string GetSkillName(AdvisorSkillType type)
    {
        return type switch
        {
            AdvisorSkillType.FireRain => "火 计",
            AdvisorSkillType.Lightning => "落 雷",
            AdvisorSkillType.IceStorm => "冰 冻",
            AdvisorSkillType.HealingWind => "回 春",
            _ => "军师技"
        };
    }

    private Color GetSkillColor(AdvisorSkillType type)
    {
        return type switch
        {
            AdvisorSkillType.FireRain => new Color(255, 150, 50),
            AdvisorSkillType.Lightning => new Color(200, 220, 255),
            AdvisorSkillType.IceStorm => new Color(120, 180, 255),
            AdvisorSkillType.HealingWind => new Color(100, 255, 150),
            _ => Color.White
        };
    }
}

public enum AdvisorSkillType
{
    FireRain,
    Lightning,
    IceStorm,
    HealingWind
}
