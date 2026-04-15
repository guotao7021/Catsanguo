using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Battle;
using CatSanguo.Data.Schemas;

namespace CatSanguo.UI.Battle;

public enum BattleInfoType
{
    CriticalHit,
    Dodge,
    SkillReady,
    SkillOnCooldown,
    FormationChanged,
    TacticalTip,
    MoraleWarning,
    BuffApplied,
    General
}

public class BattleInfoDisplay
{
    private struct InfoEntry
    {
        public string Text;
        public BattleInfoType Type;
        public float Life;
        public float MaxLife;
        public float YOffset;
    }

    private readonly List<InfoEntry> _entries = new();
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _bigFont = null!;
    private bool _tacticalTipShown;

    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase bigFont)
    {
        _pixel = pixel;
        _font = font;
        _bigFont = bigFont;
    }

    public void AddInfo(string text, BattleInfoType type, float duration = 1.5f)
    {
        _entries.Add(new InfoEntry
        {
            Text = text,
            Type = type,
            Life = duration,
            MaxLife = duration,
            YOffset = 0
        });
    }

    public void GenerateTacticalTip(List<Squad> enemySquads)
    {
        if (_tacticalTipShown || enemySquads.Count == 0) return;
        _tacticalTipShown = true;

        // 分析敌方军种组成
        int ranged = enemySquads.Count(s => s.UnitType == UnitType.Archer || s.UnitType == UnitType.Crossbowman);
        int cavalry = enemySquads.Count(s => s.UnitType == UnitType.Cavalry || s.UnitType == UnitType.HeavyCavalry || s.UnitType == UnitType.LightCavalry);
        int infantry = enemySquads.Count(s => s.UnitType == UnitType.Infantry || s.UnitType == UnitType.Spearman || s.UnitType == UnitType.ShieldInfantry);
        int total = enemySquads.Count;

        if (ranged * 2 > total)
            AddInfo("敌方远程较多, 推荐盾兵/骑兵冲锋", BattleInfoType.TacticalTip, 4f);
        else if (cavalry * 2 > total)
            AddInfo("敌方骑兵较多, 推荐枪兵反制", BattleInfoType.TacticalTip, 4f);
        else if (infantry * 2 > total)
            AddInfo("敌方步兵较多, 推荐弓兵远程输出", BattleInfoType.TacticalTip, 4f);
    }

    public void Update(float deltaTime)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            entry.Life -= deltaTime;
            entry.YOffset += deltaTime * 15f;
            _entries[i] = entry;

            if (entry.Life <= 0)
                _entries.RemoveAt(i);
        }
    }

    public void Draw(SpriteBatch sb)
    {
        int sw = GameSettings.ScreenWidth;
        int baseY = 80;

        for (int i = 0; i < Math.Min(_entries.Count, 5); i++)
        {
            var entry = _entries[_entries.Count - 1 - i];
            float alpha = MathHelper.Clamp(entry.Life / entry.MaxLife, 0, 1);
            Color color = GetInfoColor(entry.Type) * alpha;

            var useFont = entry.Type == BattleInfoType.CriticalHit ? _bigFont : _font;
            var size = useFont.MeasureString(entry.Text);
            float y = baseY + i * 30 - entry.YOffset;

            // 半透明背景
            sb.Draw(_pixel, new Rectangle((int)(sw / 2 - size.X / 2 - 8), (int)(y - 2),
                (int)(size.X + 16), (int)(size.Y + 4)), new Color(0, 0, 0, (int)(80 * alpha)));

            sb.DrawString(useFont, entry.Text, new Vector2(sw / 2 - size.X / 2, y), color);
        }
    }

    private static Color GetInfoColor(BattleInfoType type) => type switch
    {
        BattleInfoType.CriticalHit => new Color(255, 80, 80),
        BattleInfoType.Dodge => new Color(200, 200, 255),
        BattleInfoType.SkillReady => new Color(255, 230, 100),
        BattleInfoType.SkillOnCooldown => new Color(150, 150, 150),
        BattleInfoType.FormationChanged => new Color(255, 220, 100),
        BattleInfoType.TacticalTip => new Color(100, 220, 200),
        BattleInfoType.MoraleWarning => new Color(255, 100, 100),
        BattleInfoType.BuffApplied => new Color(100, 180, 255),
        _ => UIHelper.BodyText
    };
}
