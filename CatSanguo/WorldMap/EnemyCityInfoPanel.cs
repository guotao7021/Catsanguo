using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.UI;
using CatSanguo.UI.Battle;

namespace CatSanguo.WorldMap;

/// <summary>
/// 敌方/中立城池信息面板（只读查看）
/// 显示城池名、势力、君主、武将列表、驻军信息
/// </summary>
public class EnemyCityInfoPanel
{
    private const int PanelW = 280;
    private const int Padding = 10;
    private const int RowH = 24;

    public bool IsActive { get; private set; }
    public Vector2 CityScreenPos { get; set; }
    public Vector2 CityWorldPos { get; set; }

    private CityData? _city;
    private string _factionName = "";
    private string _leaderName = "";
    private string _cityScale = "";
    private int _defenseLevel;
    private List<GeneralDisplayInfo> _generals = new();
    private int _garrisonCount;

    public void Open(CityData city, Vector2 screenPos, Vector2 worldPos,
                     List<ScenarioFaction> factions, List<GeneralData> allGenerals)
    {
        _city = city;
        CityScreenPos = screenPos;
        CityWorldPos = worldPos;
        IsActive = true;

        // 查找拥有此城池的势力
        var faction = factions.FirstOrDefault(f =>
            f.FactionId.Equals(city.Owner, StringComparison.OrdinalIgnoreCase));

        _factionName = faction?.FactionName ?? (city.Owner == "neutral" ? "无主" : city.Owner);

        // 查找君主
        _leaderName = "";
        if (faction != null && !string.IsNullOrEmpty(faction.LeaderId))
        {
            var leader = allGenerals.FirstOrDefault(g => g.Id == faction.LeaderId);
            _leaderName = leader?.Name ?? "";
        }

        _cityScale = city.CityScale switch
        {
            "small" => "小城",
            "medium" => "中城",
            "large" => "大城",
            "huge" => "巨城",
            _ => "城池"
        };
        _defenseLevel = city.DefenseLevel;

        // 查找驻扎在此城的武将
        _generals.Clear();
        if (faction != null)
        {
            foreach (var alloc in faction.InitialGenerals)
            {
                if (alloc.AssignedCityId == city.Id)
                {
                    var gen = allGenerals.FirstOrDefault(g => g.Id == alloc.GeneralId);
                    if (gen != null)
                    {
                        _generals.Add(new GeneralDisplayInfo
                        {
                            Name = gen.Name,
                            Strength = gen.Strength,
                            Intelligence = gen.Intelligence,
                            Command = gen.Command
                        });
                    }
                }
            }
        }

        _garrisonCount = city.Garrison.Count;
    }

    public void Close()
    {
        IsActive = false;
        _city = null;
    }

    public void Update(InputManager input)
    {
        if (!IsActive) return;

        // 点击面板外关闭
        var panelRect = ComputePanelRect();
        if (input.IsMouseClicked() && !panelRect.Contains(input.MousePosition.ToPoint()))
        {
            Close();
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        if (!IsActive || _city == null) return;

        var rect = ComputePanelRect();

        // 背景
        sb.Draw(pixel, rect, new Color(30, 25, 18, 235));
        UIHelper.DrawBorder(sb, pixel, rect, new Color(120, 90, 50), 2);

        // 内边框装饰
        var innerRect = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
        UIHelper.DrawBorder(sb, pixel, innerRect, new Color(70, 55, 35), 1);

        int y = rect.Y + Padding;
        int contentX = rect.X + Padding;
        int contentW = rect.Width - Padding * 2;

        // 城池名称（大字）
        string title = _city.Name;
        var titleSize = font.MeasureString(title);
        sb.DrawString(font, title,
            new Vector2(rect.X + (rect.Width - titleSize.X) / 2, y),
            new Color(255, 220, 140));
        y += (int)titleSize.Y + 4;

        // 分隔线
        sb.Draw(pixel, new Rectangle(contentX, y, contentW, 1), new Color(80, 65, 45));
        y += 6;

        // 势力信息
        sb.DrawString(smallFont, $"势力: {_factionName}",
            new Vector2(contentX, y), new Color(200, 180, 140));
        y += RowH;

        if (!string.IsNullOrEmpty(_leaderName))
        {
            sb.DrawString(smallFont, $"君主: {_leaderName}",
                new Vector2(contentX, y), new Color(200, 180, 140));
            y += RowH;
        }

        // 城池属性
        sb.DrawString(smallFont, $"规模: {_cityScale}  城防: Lv.{_defenseLevel}",
            new Vector2(contentX, y), new Color(170, 155, 120));
        y += RowH;

        // 分隔线
        sb.Draw(pixel, new Rectangle(contentX, y, contentW, 1), new Color(70, 55, 40));
        y += 6;

        // 武将列表
        if (_generals.Count > 0)
        {
            sb.DrawString(smallFont, $"武将 ({_generals.Count}人):",
                new Vector2(contentX, y), new Color(180, 160, 110));
            y += RowH;

            foreach (var gen in _generals)
            {
                sb.DrawString(smallFont, $" {gen.Name}",
                    new Vector2(contentX + 4, y), new Color(210, 195, 155));
                // 右侧显示关键属性
                string stats = $"武{gen.Strength} 智{gen.Intelligence} 统{gen.Command}";
                var statsSize = smallFont.MeasureString(stats);
                sb.DrawString(smallFont, stats,
                    new Vector2(rect.X + rect.Width - Padding - statsSize.X, y),
                    new Color(150, 140, 110));
                y += RowH;
            }
        }
        else
        {
            sb.DrawString(smallFont, "无驻守武将",
                new Vector2(contentX, y), new Color(140, 125, 95));
            y += RowH;
        }

        // 驻军信息
        if (_garrisonCount > 0)
        {
            y += 4;
            sb.DrawString(smallFont, $"守备军: {_garrisonCount}队",
                new Vector2(contentX, y), new Color(200, 120, 100));
        }

        // 底部提示
        y = rect.Y + rect.Height - Padding - 18;
        string hint = "[ 点击空白处关闭 ]";
        var hintSize = smallFont.MeasureString(hint);
        sb.DrawString(smallFont, hint,
            new Vector2(rect.X + (rect.Width - hintSize.X) / 2, y),
            new Color(120, 110, 85));
    }

    private Rectangle ComputePanelRect()
    {
        // 计算面板高度
        int h = Padding; // top padding
        h += 28; // city name
        h += 6; // separator
        h += RowH; // faction
        if (!string.IsNullOrEmpty(_leaderName)) h += RowH; // leader
        h += RowH; // scale + defense
        h += 6; // separator
        h += RowH; // generals header or "no generals"
        h += _generals.Count * RowH; // general rows
        if (_garrisonCount > 0) h += RowH + 4; // garrison
        h += 26; // hint
        h += Padding; // bottom padding

        // 定位：城池屏幕坐标右侧
        int sx = (int)CityScreenPos.X;
        int sy = (int)CityScreenPos.Y;
        int offsetX = 45;

        int px = sx + offsetX;
        if (px + PanelW > GameSettings.ScreenWidth - 10)
            px = sx - offsetX - PanelW;

        int py = sy - h / 2;
        py = Math.Clamp(py, 60, GameSettings.ScreenHeight - 40 - h);
        px = Math.Clamp(px, 10, GameSettings.ScreenWidth - 10 - PanelW);

        return new Rectangle(px, py, PanelW, h);
    }

    private struct GeneralDisplayInfo
    {
        public string Name;
        public int Strength;
        public int Intelligence;
        public int Command;
    }
}
