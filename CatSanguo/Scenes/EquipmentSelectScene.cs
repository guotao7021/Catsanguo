using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.UI;

namespace CatSanguo.Scenes;

public class EquipmentSelectScene : Scene
{
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private SpriteFontBase _smallFont;

    private string _generalId;
    private string _slotType;
    private Action? _backAction;

    private List<EquipmentData> _filteredEquipment = new();
    private List<Button> _equipmentButtons = new();
    private Button? _backButton;
    private Button? _unequipButton;

    private static readonly Dictionary<string, Color> RarityColors = new()
    {
        { "common", new Color(150, 150, 150) },
        { "rare", new Color(100, 150, 255) },
        { "epic", new Color(180, 120, 255) },
        { "legendary", new Color(255, 180, 50) },
    };

    private static readonly Dictionary<string, string> StatTypeNames = new()
    {
        { "strength", "武力" },
        { "intelligence", "智力" },
        { "leadership", "统率" },
        { "speed", "速度" },
    };

    private static readonly Dictionary<string, string> SlotTypeNames = new()
    {
        { "weapon", "武器" },
        { "armor", "铠甲" },
        { "book", "兵书" },
        { "mount", "坐骑" },
    };

    public EquipmentSelectScene(string generalId, string slotType, Action? backAction = null)
    {
        _generalId = generalId;
        _slotType = slotType;
        _backAction = backAction;
    }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.NotifyFont;
        _smallFont = Game.SmallFont;

        var allEquipment = GameState.Instance.AllEquipment;
        _filteredEquipment = allEquipment
            .Where(e => e.Type == _slotType)
            .OrderByDescending(e => e.StatBonus)
            .ToList();

        CreateEquipmentButtons();

        _backButton = new Button("返 回", new Rectangle(30, GameSettings.ScreenHeight - 60, 100, 40));
        _backButton.NormalColor = new Color(60, 30, 30);
        _backButton.HoverColor = new Color(80, 40, 40);
        _backButton.OnClick = GoBack;

        _unequipButton = new Button("卸 下", new Rectangle(GameSettings.ScreenWidth - 160, GameSettings.ScreenHeight - 60, 120, 40));
        _unequipButton.NormalColor = new Color(60, 30, 30);
        _unequipButton.HoverColor = new Color(80, 40, 40);
        _unequipButton.OnClick = UnequipCurrentItem;

        var progress = GameState.Instance.GetGeneralProgress(_generalId);
        if (_unequipButton != null)
        {
            bool hasEquipped = progress != null && progress.EquippedItems.ContainsKey(_slotType);
            _unequipButton.Enabled = hasEquipped;
        }
    }

    private void CreateEquipmentButtons()
    {
        var buttons = new List<Button>();

        int startX = 30;
        int startY = 80;
        int cardWidth = 290;
        int cardHeight = 60;
        int gapX = 15;
        int gapY = 12;
        int cols = 3;

        var progress = GameState.Instance.GetGeneralProgress(_generalId);
        string equippedId = progress?.EquippedItems.GetValueOrDefault(_slotType) ?? "";

        for (int i = 0; i < _filteredEquipment.Count; i++)
        {
            var equip = _filteredEquipment[i];
            int col = i % cols;
            int row = i / cols;
            int x = startX + col * (cardWidth + gapX);
            int y = startY + row * (cardHeight + gapY);

            bool isEquipped = equip.Id == equippedId;
            int idx = i;
            var btn = new Button("", new Rectangle(x, y, cardWidth, cardHeight));
            btn.NormalColor = isEquipped ? new Color(55, 50, 35) : new Color(45, 40, 35);
            btn.HoverColor = isEquipped ? new Color(75, 65, 45) : new Color(65, 55, 45);
            btn.OnClick = () => ToggleEquip(idx);
            buttons.Add(btn);
        }

        _equipmentButtons = buttons;
    }

    private void ToggleEquip(int index)
    {
        if (index < 0 || index >= _filteredEquipment.Count) return;

        var equip = _filteredEquipment[index];
        var progress = GameState.Instance.GetGeneralProgress(_generalId);
        if (progress == null) return;

        string equippedId = progress.EquippedItems.GetValueOrDefault(_slotType, "");

        if (equippedId == equip.Id)
        {
            GameState.Instance.UnequipItem(_generalId, _slotType);
        }
        else
        {
            GameState.Instance.EquipItem(_generalId, _slotType, equip.Id);
        }

        CreateEquipmentButtons();
        if (_unequipButton != null)
        {
            var updatedProgress = GameState.Instance.GetGeneralProgress(_generalId);
            _unequipButton.Enabled = updatedProgress != null && updatedProgress.EquippedItems.ContainsKey(_slotType);
        }
    }

    private void UnequipCurrentItem()
    {
        GameState.Instance.UnequipItem(_generalId, _slotType);
        CreateEquipmentButtons();
        if (_unequipButton != null)
        {
            var progress = GameState.Instance.GetGeneralProgress(_generalId);
            _unequipButton.Enabled = progress != null && progress.EquippedItems.ContainsKey(_slotType);
        }
    }

    private void GoBack()
    {
        if (_backAction != null)
        {
            _backAction.Invoke();
        }
        else
        {
            Game.SceneManager.ChangeScene(new GeneralRosterScene());
        }
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var btn in _equipmentButtons) btn.Update(Input);
        _backButton?.Update(Input);
        _unequipButton?.Update(Input);

        if (Input.IsKeyPressed(Keys.Escape))
        {
            GoBack();
        }
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(30, 25, 18));
        SpriteBatch.Begin();

        // Title
        string slotName = SlotTypeNames.GetValueOrDefault(_slotType, _slotType);
        SpriteBatch.DrawString(_titleFont, $"装备选择 - {slotName}", new Vector2(30, 20), new Color(220, 190, 130));
        SpriteBatch.Draw(_pixel, new Rectangle(0, 65, GameSettings.ScreenWidth, 1), new Color(80, 65, 45));

        // Equipment cards
        foreach (var btn in _equipmentButtons)
        {
            btn.Draw(SpriteBatch, _font, _pixel);
        }

        // Draw equipment details on top of buttons
        DrawEquipmentDetails();

        // Highlight equipped item border
        var progress = GameState.Instance.GetGeneralProgress(_generalId);
        string equippedId = progress?.EquippedItems.GetValueOrDefault(_slotType) ?? "";
        for (int i = 0; i < _equipmentButtons.Count && i < _filteredEquipment.Count; i++)
        {
            if (_filteredEquipment[i].Id == equippedId)
            {
                var btn = _equipmentButtons[i];
                DrawBorder(btn.Bounds, new Color(255, 220, 100), 3);
            }
        }

        // Currently equipped info panel
        DrawEquippedInfo();

        // Buttons
        _backButton?.Draw(SpriteBatch, _font, _pixel);
        _unequipButton?.Draw(SpriteBatch, _font, _pixel);

        SpriteBatch.End();
    }

    private void DrawEquipmentDetails()
    {
        for (int i = 0; i < _equipmentButtons.Count && i < _filteredEquipment.Count; i++)
        {
            var equip = _filteredEquipment[i];
            var btn = _equipmentButtons[i];

            Color rarityColor = RarityColors.GetValueOrDefault(equip.Rarity, Color.White);
            string statName = StatTypeNames.GetValueOrDefault(equip.StatType, equip.StatType);

            // Equipment name with rarity color
            SpriteBatch.DrawString(_font, equip.Name, new Vector2(btn.Bounds.X + 10, btn.Bounds.Y + 8), rarityColor);

            // Stat bonus
            string statText = $"+{equip.StatBonus} {statName}";
            SpriteBatch.DrawString(_smallFont, statText, new Vector2(btn.Bounds.X + 10, btn.Bounds.Y + 30), new Color(200, 200, 180));

            // Rarity label
            string rarityLabel = GetRarityLabel(equip.Rarity);
            SpriteBatch.DrawString(_smallFont, rarityLabel, new Vector2(btn.Bounds.Right - 60, btn.Bounds.Y + 8), rarityColor * 0.8f);
        }
    }

    private void DrawEquippedInfo()
    {
        var progress = GameState.Instance.GetGeneralProgress(_generalId);
        if (progress == null) return;

        string equippedId = progress.EquippedItems.GetValueOrDefault(_slotType, "");
        var equippedItem = _filteredEquipment.FirstOrDefault(e => e.Id == equippedId);

        int panelX = GameSettings.ScreenWidth - 305;
        int panelY = 80;
        int panelWidth = 290;
        int panelHeight = 120;

        // Panel background
        SpriteBatch.Draw(_pixel, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(40, 35, 28));
        DrawBorder(new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(100, 85, 60), 2);

        SpriteBatch.DrawString(_font, "当前装备", new Vector2(panelX + 15, panelY + 10), new Color(220, 190, 130));

        if (equippedItem != null)
        {
            Color rarityColor = RarityColors.GetValueOrDefault(equippedItem.Rarity, Color.White);
            string statName = StatTypeNames.GetValueOrDefault(equippedItem.StatType, equippedItem.StatType);

            SpriteBatch.DrawString(_font, equippedItem.Name, new Vector2(panelX + 15, panelY + 40), rarityColor);
            SpriteBatch.DrawString(_smallFont, $"+{equippedItem.StatBonus} {statName}", new Vector2(panelX + 15, panelY + 65), new Color(200, 200, 180));

            string rarityLabel = GetRarityLabel(equippedItem.Rarity);
            SpriteBatch.DrawString(_smallFont, rarityLabel, new Vector2(panelX + 15, panelY + 88), rarityColor * 0.8f);
        }
        else
        {
            SpriteBatch.DrawString(_smallFont, "未装备", new Vector2(panelX + 15, panelY + 45), new Color(120, 110, 90));
        }
    }

    private string GetRarityLabel(string rarity)
    {
        return rarity switch
        {
            "common" => "普通",
            "rare" => "稀有",
            "epic" => "史诗",
            "legendary" => "传说",
            _ => rarity,
        };
    }

    private void DrawBorder(Rectangle rect, Color color, int thickness)
    {
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
