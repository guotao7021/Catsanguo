using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.UI.Battle;

namespace CatSanguo.UI;

public enum SaveLoadMode
{
    Save,
    Load
}

public class SaveLoadPanel
{
    private const int PanelW = 500;
    private const int PanelH = 440;
    private const int SlotH = 54;
    private const int SlotGap = 6;
    private const int Padding = 16;
    private const int TitleH = 40;
    private const int BottomBarH = 46;

    public bool IsActive { get; private set; }
    public SaveLoadMode Mode { get; private set; }

    /// <summary>操作完成回调: (slotIndex, isLoad). isLoad=true表示读取了存档需要重新加载场景</summary>
    public Action<int, bool>? OnOperationComplete { get; set; }

    private List<SaveSlotInfo> _slots = new();
    private int _selectedSlot = -1;
    private int _confirmDeleteSlot = -1; // 需要确认删除的槽位
    private string _statusMessage = "";
    private float _statusTimer = 0f;

    public void Open(SaveLoadMode mode)
    {
        Mode = mode;
        IsActive = true;
        _selectedSlot = -1;
        _confirmDeleteSlot = -1;
        _statusMessage = "";
        _statusTimer = 0f;
        RefreshSlots();
    }

    public void Close()
    {
        IsActive = false;
        _confirmDeleteSlot = -1;
    }

    private void RefreshSlots()
    {
        _slots = GameState.Instance.GetSaveSlotInfos();
    }

    public void Update(InputManager input, float dt)
    {
        if (!IsActive) return;

        _statusTimer -= dt;

        var panelRect = GetPanelRect();
        var mp = input.MousePosition.ToPoint();

        if (!input.IsMouseClicked()) return;

        // 点击面板外关闭
        if (!panelRect.Contains(mp))
        {
            Close();
            return;
        }

        // 关闭按钮
        var closeRect = new Rectangle(panelRect.Right - 36, panelRect.Y + 6, 28, 28);
        if (closeRect.Contains(mp))
        {
            Close();
            return;
        }

        // 确认删除对话框的按钮
        if (_confirmDeleteSlot >= 0)
        {
            var dialogRect = GetConfirmDialogRect(panelRect);
            var yesRect = new Rectangle(dialogRect.X + 30, dialogRect.Bottom - 42, 80, 30);
            var noRect = new Rectangle(dialogRect.Right - 110, dialogRect.Bottom - 42, 80, 30);

            if (yesRect.Contains(mp))
            {
                GameState.Instance.DeleteSlot(_confirmDeleteSlot);
                _statusMessage = $"档位 {_confirmDeleteSlot} 已删除";
                _statusTimer = 2f;
                _confirmDeleteSlot = -1;
                if (_selectedSlot == _confirmDeleteSlot) _selectedSlot = -1;
                RefreshSlots();
                return;
            }
            if (noRect.Contains(mp))
            {
                _confirmDeleteSlot = -1;
                return;
            }
            // 确认对话框激活时，不处理其他点击
            return;
        }

        // 槽位区域
        int slotsStartY = panelRect.Y + TitleH + 4;
        for (int i = 0; i < _slots.Count; i++)
        {
            var slotRect = new Rectangle(panelRect.X + Padding, slotsStartY + i * (SlotH + SlotGap), PanelW - Padding * 2, SlotH);
            if (!slotRect.Contains(mp)) continue;

            var slot = _slots[i];

            // 删除按钮（仅非空槽位）
            if (!slot.IsEmpty)
            {
                var delRect = new Rectangle(slotRect.Right - 50, slotRect.Y + (SlotH - 24) / 2, 40, 24);
                if (delRect.Contains(mp))
                {
                    _confirmDeleteSlot = slot.SlotIndex;
                    return;
                }
            }

            // 选中槽位
            _selectedSlot = slot.SlotIndex;
            break;
        }

        // 底部操作按钮
        int btnY = panelRect.Bottom - BottomBarH + 8;
        var actionRect = new Rectangle(panelRect.X + Padding, btnY, PanelW - Padding * 2, 30);
        if (actionRect.Contains(mp) && _selectedSlot > 0)
        {
            ExecuteAction();
        }
    }

    private void ExecuteAction()
    {
        if (_selectedSlot < 1) return;
        var slot = _slots.Find(s => s.SlotIndex == _selectedSlot);

        if (Mode == SaveLoadMode.Save)
        {
            bool ok = GameState.Instance.SaveToSlot(_selectedSlot);
            _statusMessage = ok ? $"已保存到档位 {_selectedSlot}" : "保存失败";
            _statusTimer = 2f;
            RefreshSlots();
            if (ok) OnOperationComplete?.Invoke(_selectedSlot, false);
        }
        else // Load
        {
            if (slot == null || slot.IsEmpty)
            {
                _statusMessage = "该档位为空";
                _statusTimer = 2f;
                return;
            }
            bool ok = GameState.Instance.LoadFromSlot(_selectedSlot);
            if (ok)
            {
                _statusMessage = $"已加载档位 {_selectedSlot}";
                _statusTimer = 2f;
                IsActive = false;
                OnOperationComplete?.Invoke(_selectedSlot, true);
            }
            else
            {
                _statusMessage = "加载失败";
                _statusTimer = 2f;
            }
        }
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont)
    {
        if (!IsActive) return;

        // 全屏半透明遮罩
        sb.Draw(pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), new Color(0, 0, 0, 150));

        var panelRect = GetPanelRect();
        var mp = Microsoft.Xna.Framework.Input.Mouse.GetState().Position;

        // 面板背景
        sb.Draw(pixel, panelRect, new Color(30, 25, 18, 245));
        UIHelper.DrawBorder(sb, pixel, panelRect, new Color(120, 95, 55), 2);

        // 标题
        string title = Mode == SaveLoadMode.Save ? "保 存 档 案" : "加 载 档 案";
        var titleSize = font.MeasureString(title);
        sb.DrawString(font, title,
            new Vector2(panelRect.X + (PanelW - titleSize.X) / 2, panelRect.Y + 10),
            new Color(240, 210, 140));

        // 分隔线
        sb.Draw(pixel, new Rectangle(panelRect.X + Padding, panelRect.Y + TitleH, PanelW - Padding * 2, 1),
            new Color(90, 75, 50));

        // 关闭按钮
        var closeRect = new Rectangle(panelRect.Right - 36, panelRect.Y + 6, 28, 28);
        bool closeHover = closeRect.Contains(mp);
        sb.Draw(pixel, closeRect, closeHover ? new Color(120, 50, 50) : new Color(70, 40, 40));
        var xSize = font.MeasureString("X");
        sb.DrawString(font, "X",
            new Vector2(closeRect.X + (28 - xSize.X) / 2, closeRect.Y + (28 - xSize.Y) / 2),
            new Color(220, 180, 180));

        // 槽位列表
        int slotsStartY = panelRect.Y + TitleH + 4;
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            var slotRect = new Rectangle(panelRect.X + Padding, slotsStartY + i * (SlotH + SlotGap), PanelW - Padding * 2, SlotH);
            bool isSelected = _selectedSlot == slot.SlotIndex;
            bool isHovered = slotRect.Contains(mp);

            // 槽位背景
            Color slotBg = isSelected ? new Color(55, 45, 30, 230)
                         : isHovered ? new Color(45, 38, 28, 200)
                         : new Color(35, 30, 22, 180);
            sb.Draw(pixel, slotRect, slotBg);
            UIHelper.DrawBorder(sb, pixel, slotRect,
                isSelected ? new Color(180, 150, 80) : new Color(80, 65, 45), 1);

            // 槽位号
            sb.DrawString(font, $"#{slot.SlotIndex}",
                new Vector2(slotRect.X + 8, slotRect.Y + 6),
                new Color(160, 140, 100));

            if (slot.IsEmpty)
            {
                sb.DrawString(smallFont, "-- 空档位 --",
                    new Vector2(slotRect.X + 60, slotRect.Y + 18),
                    new Color(100, 90, 70));
            }
            else
            {
                // 第一行: 势力 + 回合
                string line1 = $"第{slot.TurnNumber}回合  {slot.CityCount}城";
                if (slot.GameDateYear > 0)
                    line1 = $"{slot.GameDateYear}年{slot.GameDateMonth}月  " + line1;
                sb.DrawString(smallFont, line1,
                    new Vector2(slotRect.X + 60, slotRect.Y + 6),
                    new Color(210, 190, 150));

                // 第二行: 保存时间
                string line2 = $"保存: {slot.SaveTime:yyyy-MM-dd HH:mm}";
                sb.DrawString(smallFont, line2,
                    new Vector2(slotRect.X + 60, slotRect.Y + 28),
                    new Color(150, 135, 110));

                // 删除按钮
                var delRect = new Rectangle(slotRect.Right - 50, slotRect.Y + (SlotH - 24) / 2, 40, 24);
                bool delHover = delRect.Contains(mp);
                sb.Draw(pixel, delRect, delHover ? new Color(140, 45, 45) : new Color(80, 40, 40));
                UIHelper.DrawBorder(sb, pixel, delRect, new Color(120, 60, 60), 1);
                var delSize = smallFont.MeasureString("删除");
                sb.DrawString(smallFont, "删除",
                    new Vector2(delRect.X + (40 - delSize.X) / 2, delRect.Y + (24 - delSize.Y) / 2),
                    new Color(220, 160, 160));
            }
        }

        // 底部操作按钮
        int btnY = panelRect.Bottom - BottomBarH + 8;
        sb.Draw(pixel, new Rectangle(panelRect.X + Padding, btnY - 6, PanelW - Padding * 2, 1),
            new Color(90, 75, 50));

        bool canAct = _selectedSlot > 0;
        if (Mode == SaveLoadMode.Load)
        {
            var selSlot = _slots.Find(s => s.SlotIndex == _selectedSlot);
            canAct = canAct && selSlot != null && !selSlot.IsEmpty;
        }

        var actionRect = new Rectangle(panelRect.X + Padding, btnY, PanelW - Padding * 2, 30);
        bool actHover = actionRect.Contains(mp) && canAct;
        sb.Draw(pixel, actionRect, actHover ? new Color(100, 75, 35) : (canAct ? new Color(70, 55, 30) : new Color(40, 35, 28)));
        UIHelper.DrawBorder(sb, pixel, actionRect, canAct ? new Color(160, 130, 70) : new Color(60, 50, 35), 1);

        string btnText = Mode == SaveLoadMode.Save ? "保存到选中档位" : "加载选中档位";
        if (_selectedSlot > 0)
            btnText = Mode == SaveLoadMode.Save ? $"保存到档位 #{_selectedSlot}" : $"加载档位 #{_selectedSlot}";
        var btnSize = font.MeasureString(btnText);
        sb.DrawString(font, btnText,
            new Vector2(actionRect.X + (actionRect.Width - btnSize.X) / 2, actionRect.Y + (30 - btnSize.Y) / 2),
            canAct ? new Color(240, 210, 140) : new Color(100, 90, 70));

        // 状态消息
        if (_statusTimer > 0 && !string.IsNullOrEmpty(_statusMessage))
        {
            float alpha = Math.Min(1f, _statusTimer);
            var msgSize = smallFont.MeasureString(_statusMessage);
            sb.DrawString(smallFont, _statusMessage,
                new Vector2(panelRect.X + (PanelW - msgSize.X) / 2, panelRect.Bottom - BottomBarH - 18),
                new Color(255, 220, 100) * alpha);
        }

        // 确认删除对话框
        if (_confirmDeleteSlot >= 0)
        {
            DrawConfirmDialog(sb, pixel, font, smallFont, panelRect);
        }
    }

    private void DrawConfirmDialog(SpriteBatch sb, Texture2D pixel, SpriteFontBase font, SpriteFontBase smallFont, Rectangle panelRect)
    {
        var dialogRect = GetConfirmDialogRect(panelRect);
        var mp = Microsoft.Xna.Framework.Input.Mouse.GetState().Position;

        sb.Draw(pixel, dialogRect, new Color(40, 32, 22, 250));
        UIHelper.DrawBorder(sb, pixel, dialogRect, new Color(180, 80, 60), 2);

        string msg = $"确定删除档位 #{_confirmDeleteSlot} ?";
        var msgSize = font.MeasureString(msg);
        sb.DrawString(font, msg,
            new Vector2(dialogRect.X + (dialogRect.Width - msgSize.X) / 2, dialogRect.Y + 18),
            new Color(240, 200, 140));

        // 确认按钮
        var yesRect = new Rectangle(dialogRect.X + 30, dialogRect.Bottom - 42, 80, 30);
        bool yesHover = yesRect.Contains(mp);
        sb.Draw(pixel, yesRect, yesHover ? new Color(140, 55, 45) : new Color(100, 45, 35));
        UIHelper.DrawBorder(sb, pixel, yesRect, new Color(160, 80, 60), 1);
        var yesSize = font.MeasureString("确认");
        sb.DrawString(font, "确认",
            new Vector2(yesRect.X + (80 - yesSize.X) / 2, yesRect.Y + (30 - yesSize.Y) / 2),
            new Color(240, 180, 160));

        // 取消按钮
        var noRect = new Rectangle(dialogRect.Right - 110, dialogRect.Bottom - 42, 80, 30);
        bool noHover = noRect.Contains(mp);
        sb.Draw(pixel, noRect, noHover ? new Color(70, 60, 45) : new Color(50, 42, 32));
        UIHelper.DrawBorder(sb, pixel, noRect, new Color(120, 100, 70), 1);
        var noSize = font.MeasureString("取消");
        sb.DrawString(font, "取消",
            new Vector2(noRect.X + (80 - noSize.X) / 2, noRect.Y + (30 - noSize.Y) / 2),
            new Color(200, 180, 140));
    }

    private Rectangle GetPanelRect()
    {
        int x = (GameSettings.ScreenWidth - PanelW) / 2;
        int y = (GameSettings.ScreenHeight - PanelH) / 2;
        return new Rectangle(x, y, PanelW, PanelH);
    }

    private Rectangle GetConfirmDialogRect(Rectangle panelRect)
    {
        int dw = 260, dh = 100;
        return new Rectangle(
            panelRect.X + (PanelW - dw) / 2,
            panelRect.Y + (PanelH - dh) / 2,
            dw, dh);
    }
}
