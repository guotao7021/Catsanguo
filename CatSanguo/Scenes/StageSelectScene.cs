using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.UI;

namespace CatSanguo.Scenes;

public class StageSelectScene : Scene
{
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private List<StageData> _stages;
    private List<Button> _stageButtons = new();
    private Button _backButton;
    private int _selectedStage = -1;
    private StageData? _selectedStageData;
    private List<GeneralData> _allGenerals;
    private List<string> _selectedGenerals = new();
    private Button? _deployButton;
    private List<Button> _generalToggleButtons = new();
    private HashSet<int> _unlockedStages = new() { 0 }; // First stage always unlocked

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;

        // Load data
        string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _stages = DataLoader.LoadList<StageData>(Path.Combine(dataPath, "stages.json"));
        _allGenerals = DataLoader.LoadList<GeneralData>(Path.Combine(dataPath, "generals.json"));

        // Unlock all stages for demo
        for (int i = 0; i < _stages.Count; i++) _unlockedStages.Add(i);

        CreateStageButtons();

        _backButton = new Button("返 回", new Rectangle(30, GameSettings.ScreenHeight - 60, 100, 40));
        _backButton.OnClick = () => Game.SceneManager.ChangeScene(new MainMenuScene());
    }

    private void CreateStageButtons()
    {
        _stageButtons.Clear();
        int startY = 120;
        int spacing = 80;

        for (int i = 0; i < _stages.Count; i++)
        {
            var stage = _stages[i];
            bool unlocked = _unlockedStages.Contains(i);
            string text = unlocked ? $"{stage.Name}" : $"[锁定] {stage.Name}";
            int idx = i;

            var btn = new Button(text, new Rectangle(60, startY + i * spacing, 280, 60));
            if (unlocked)
            {
                btn.OnClick = () => SelectStage(idx);
            }
            else
            {
                btn.NormalColor = new Color(40, 35, 30);
                btn.TextColor = new Color(100, 90, 70);
            }
            _stageButtons.Add(btn);
        }
    }

    private void SelectStage(int index)
    {
        _selectedStage = index;
        _selectedStageData = _stages[index];
        _selectedGenerals.Clear();
        _generalToggleButtons.Clear();

        // Create general selection buttons
        int startY = 200;
        for (int i = 0; i < _selectedStageData.PlayerGeneralPool.Count; i++)
        {
            string genId = _selectedStageData.PlayerGeneralPool[i];
            var gen = _allGenerals.FirstOrDefault(g => g.Id == genId);
            if (gen == null) continue;

            int idx = i;
            string gId = genId;
            var btn = new Button(gen.Name, new Rectangle(500, startY + i * 55, 200, 45));
            btn.OnClick = () => ToggleGeneral(gId);
            _generalToggleButtons.Add(btn);
        }

        _deployButton = new Button("出 战 !", new Rectangle(550, GameSettings.ScreenHeight - 80, 180, 55));
        _deployButton.NormalColor = new Color(120, 50, 30);
        _deployButton.HoverColor = new Color(160, 70, 40);
        _deployButton.OnClick = () =>
        {
            if (_selectedGenerals.Count > 0 && _selectedStageData != null)
            {
                Game.SceneManager.ChangeScene(new BattleScene(_selectedStageData, _selectedGenerals, _allGenerals));
            }
        };
    }

    private void ToggleGeneral(string generalId)
    {
        if (_selectedStageData == null) return;

        if (_selectedGenerals.Contains(generalId))
        {
            _selectedGenerals.Remove(generalId);
        }
        else if (_selectedGenerals.Count < _selectedStageData.PlayerSlots)
        {
            _selectedGenerals.Add(generalId);
        }
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var btn in _stageButtons) btn.Update(Input);
        _backButton.Update(Input);

        if (_selectedStageData != null)
        {
            foreach (var btn in _generalToggleButtons) btn.Update(Input);
            _deployButton?.Update(Input);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(35, 28, 22));
        SpriteBatch.Begin();

        // Background
        DrawScrollBackground();

        // Title
        string title = "选 关";
        Vector2 ts = _titleFont.MeasureString(title);
        SpriteBatch.DrawString(_titleFont, title, new Vector2(60, 30), new Color(220, 190, 130));

        // Stage buttons
        foreach (var btn in _stageButtons) btn.Draw(SpriteBatch, _font, _pixel);

        // Stage detail panel
        if (_selectedStageData != null)
        {
            DrawStageDetail();
        }

        _backButton.Draw(SpriteBatch, _font, _pixel);
        SpriteBatch.End();
    }

    private void DrawStageDetail()
    {
        // Panel background
        Rectangle panel = new Rectangle(400, 80, 820, GameSettings.ScreenHeight - 160);
        SpriteBatch.Draw(_pixel, panel, new Color(45, 38, 30));
        DrawBorder(panel, new Color(120, 100, 70), 2);

        // Stage name & description
        SpriteBatch.DrawString(_titleFont, _selectedStageData!.Name,
            new Vector2(420, 100), new Color(220, 190, 130));

        SpriteBatch.DrawString(_font, _selectedStageData.Description,
            new Vector2(420, 155), new Color(180, 160, 120));

        // Difficulty stars
        string diff = "难度: " + new string('★', _selectedStageData.Difficulty) + new string('☆', 3 - _selectedStageData.Difficulty);
        SpriteBatch.DrawString(_font, diff, new Vector2(420, 180), new Color(200, 170, 100));

        // General selection
        SpriteBatch.DrawString(_font, $"选择武将 ({_selectedGenerals.Count}/{_selectedStageData.PlayerSlots}):",
            new Vector2(500, 215), new Color(180, 160, 120));

        // Draw general toggle buttons with selection state
        for (int i = 0; i < _generalToggleButtons.Count; i++)
        {
            var btn = _generalToggleButtons[i];
            string genId = _selectedStageData.PlayerGeneralPool[i];
            bool selected = _selectedGenerals.Contains(genId);
            btn.NormalColor = selected ? new Color(80, 50, 30) : new Color(50, 40, 30);
            btn.BorderColor = selected ? new Color(220, 180, 80) : new Color(100, 80, 60);
            btn.Draw(SpriteBatch, _font, _pixel);

            // Show general stats
            var gen = _allGenerals.FirstOrDefault(g => g.Id == genId);
            if (gen != null)
            {
                string stats = $"武:{gen.Strength} 智:{gen.Intelligence} 统:{gen.Leadership} 速:{gen.Speed}";
                SpriteBatch.DrawString(_font, stats, new Vector2(710, 210 + i * 55), new Color(140, 120, 90));
            }
        }

        // Enemy info
        SpriteBatch.DrawString(_font, "敌方部队:", new Vector2(500, 440), new Color(200, 100, 80));
        for (int i = 0; i < _selectedStageData.EnemySquads.Count; i++)
        {
            var es = _selectedStageData.EnemySquads[i];
            var gen = _allGenerals.FirstOrDefault(g => g.Id == es.GeneralId);
            string enemyText = gen != null
                ? $"  {gen.Name} ({es.FormationType}) - {es.SoldierCount}兵"
                : $"  {es.GeneralId} ({es.FormationType})";
            SpriteBatch.DrawString(_font, enemyText, new Vector2(500, 470 + i * 28), new Color(180, 120, 90));
        }

        _deployButton?.Draw(SpriteBatch, _font, _pixel);
    }

    private void DrawScrollBackground()
    {
        // Aged parchment feel
        for (int y = 0; y < GameSettings.ScreenHeight; y += 3)
        {
            float t = (float)y / GameSettings.ScreenHeight;
            byte r = (byte)MathHelper.Lerp(40, 32, t);
            byte g = (byte)MathHelper.Lerp(34, 26, t);
            byte b = (byte)MathHelper.Lerp(28, 20, t);
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, GameSettings.ScreenWidth, 3), new Color(r, g, b));
        }
    }

    private void DrawBorder(Rectangle rect, Color color, int thickness)
    {
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
