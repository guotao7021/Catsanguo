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
using CatSanguo.Generals;
using CatSanguo.Skills;

namespace CatSanguo.Scenes;

public class GeneralRosterScene : Scene
{
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private SpriteFontBase _smallFont;

    private List<GeneralData> _allGenerals = new();
    private List<SkillData> _allSkills = new();
    private List<Button> _generalButtons = new();
    private Button? _backButton;
    private Button? _upgradeButton;

    // Tab system
    private int _selectedTabIndex = 0;
    private readonly List<Button> _tabButtons = new();
    private readonly List<string> _tabNames = new() { "属性", "装备", "技能", "技能树" };

    // Equipment Tab
    private List<Button> _equipSlotButtons = new();
    private readonly string[] _equipSlotTypes = { "weapon", "armor", "book", "mount" };
    private readonly string[] _equipSlotNames = { "武器", "铠甲", "兵书", "坐骑" };

    // Skill Tab
    private List<Button> _skillButtons = new();

    // Skill Tree Tab
    private List<Button> _skillTreeButtons = new();

    private int _selectedGeneralIndex = -1;
    private GeneralProgress? _selectedGeneral;
    private Action? _onSaveComplete;

    // Notification
    private string _notifyText = "";
    private float _notifyTimer = 0f;

    public GeneralRosterScene(Action? onSaveComplete = null)
    {
        _onSaveComplete = onSaveComplete;
    }

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.NotifyFont;
        _smallFont = Game.SmallFont;

        string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _allGenerals = DataLoader.LoadList<GeneralData>(Path.Combine(dataPath, "generals.json"));
        _allSkills = DataLoader.LoadList<SkillData>(Path.Combine(dataPath, "skills.json"));

        GameState.Instance.Initialize(_allGenerals);
        GameState.Instance.LoadGameData();

        CreateGeneralButtons();
        CreateTabButtons();

        _backButton = new Button("返 回", new Rectangle(30, GameSettings.ScreenHeight - 60, 100, 40));
        _backButton.NormalColor = new Color(60, 30, 30);
        _backButton.HoverColor = new Color(80, 40, 40);
        _backButton.OnClick = GoBack;

        _upgradeButton = new Button("升 级", new Rectangle(GameSettings.ScreenWidth - 150, GameSettings.ScreenHeight - 200, 120, 45));
        _upgradeButton.NormalColor = new Color(50, 80, 50);
        _upgradeButton.HoverColor = new Color(70, 110, 70);
        _upgradeButton.Enabled = false;
        _upgradeButton.OnClick = TryUpgradeGeneral;
    }

    private void CreateTabButtons()
    {
        _tabButtons.Clear();
        int tabWidth = 80;
        int tabHeight = 30;
        int tabGap = 5;
        int startX = 600;
        int y = 85;

        for (int i = 0; i < _tabNames.Count; i++)
        {
            int idx = i;
            var btn = new Button(_tabNames[i], new Rectangle(startX + i * (tabWidth + tabGap), y, tabWidth, tabHeight));
            btn.NormalColor = new Color(50, 45, 35);
            btn.HoverColor = new Color(70, 60, 45);
            btn.OnClick = () => { _selectedTabIndex = idx; RefreshTab(); };
            _tabButtons.Add(btn);
        }
    }

    private void RefreshTab()
    {
        _equipSlotButtons = new();
        _skillButtons = new();
        _skillTreeButtons = new();

        if (_selectedGeneral == null) return;

        switch (_selectedTabIndex)
        {
            case 1:
                CreateEquipSlotButtons();
                break;
            case 2:
                CreateSkillButtons();
                break;
            case 3:
                CreateSkillTreeButtons();
                break;
        }
    }

    private void CreateEquipSlotButtons()
    {
        var buttons = new List<Button>();
        int startX = 615;
        int startY = 160;
        int btnWidth = 170;
        int btnHeight = 40;
        int gap = 10;

        for (int i = 0; i < 4; i++)
        {
            var slotType = _equipSlotTypes[i];
            var progress = _selectedGeneral;
            string equippedName = "";
            if (progress != null && progress.EquippedItems.TryGetValue(slotType, out var equipId))
            {
                var equip = GameState.Instance.AllEquipment.FirstOrDefault(e => e.Id == equipId);
                equippedName = equip?.Name ?? "";
            }

            var btn = new Button(
                $"{_equipSlotNames[i]}: {(string.IsNullOrEmpty(equippedName) ? "(空)" : equippedName)}",
                new Rectangle(startX, startY + i * (btnHeight + gap), btnWidth, btnHeight)
            );
            btn.NormalColor = new Color(50, 45, 35);
            btn.HoverColor = new Color(70, 60, 45);
            int slotIdx = i;
            btn.OnClick = () => OpenEquipmentSelect(slotIdx);
            buttons.Add(btn);
        }
        _equipSlotButtons = buttons;
    }

    private void OpenEquipmentSelect(int slotIndex)
    {
        if (_selectedGeneral == null || _selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count) return;
        var gen = _allGenerals[_selectedGeneralIndex];
        Game.SceneManager.ChangeScene(new EquipmentSelectScene(gen.Id, _equipSlotTypes[slotIndex], () =>
        {
            Game.SceneManager.ChangeScene(new GeneralRosterScene(_onSaveComplete));
        }));
    }

    private void CreateSkillButtons()
    {
        var buttons = new List<Button>();
        if (_selectedGeneral == null || _selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count)
        {
            _skillButtons = buttons;
            return;
        }

        var gen = _allGenerals[_selectedGeneralIndex];
        int startX = 615;
        int startY = 160;
        int btnWidth = 200;
        int btnHeight = 35;
        int gap = 8;
        int row = 0;

        // Active skill display + swap
        var activeSkillId = _selectedGeneral.GetActiveSkillId();
        var activeSkill = _allSkills.FirstOrDefault(s => s.Id == activeSkillId);
        if (activeSkill != null)
        {
            int skillLevel = _selectedGeneral.SkillLevels.GetValueOrDefault(activeSkillId, 1);
            var btn = new Button($"[主] {activeSkill.Name} Lv.{skillLevel}", new Rectangle(startX, startY + row * (btnHeight + gap), btnWidth, btnHeight));
            btn.NormalColor = new Color(60, 50, 40);
            btn.HoverColor = new Color(80, 65, 50);
            buttons.Add(btn);
            row++;
        }

        // Passive skill display
        var passiveSkillId = _selectedGeneral.GetPassiveSkillId();
        var passiveSkill = _allSkills.FirstOrDefault(s => s.Id == passiveSkillId);
        if (passiveSkill != null)
        {
            int skillLevel = _selectedGeneral.SkillLevels.GetValueOrDefault(passiveSkillId, 1);
            var btn = new Button($"[被] {passiveSkill.Name} Lv.{skillLevel}", new Rectangle(startX, startY + row * (btnHeight + gap), btnWidth, btnHeight));
            btn.NormalColor = new Color(50, 50, 60);
            btn.HoverColor = new Color(65, 65, 80);
            buttons.Add(btn);
            row++;
        }

        // Learned skills that can be swapped in
        var learnedSkills = _selectedGeneral.LearnedSkillIds
            .Where(sid => sid != activeSkillId && sid != passiveSkillId)
            .ToList();

        if (learnedSkills.Count > 0)
        {
            row++; // spacing
            foreach (var sid in learnedSkills)
            {
                var skill = _allSkills.FirstOrDefault(s => s.Id == sid);
                if (skill == null) continue;

                string label = skill.Type == "active" ? "[学] 设为主动" : "[学] 设为被动";
                var btn = new Button($"{label}: {skill.Name}", new Rectangle(startX, startY + row * (btnHeight + gap), btnWidth, btnHeight));
                btn.NormalColor = new Color(40, 60, 40);
                btn.HoverColor = new Color(60, 80, 60);
                string skillId = sid;
                string skillType = skill.Type;
                btn.OnClick = () => SwapSkill(skillId, skillType);
                buttons.Add(btn);
                row++;
            }
        }

        // Skill level-up buttons
        row++;
        if (activeSkill != null)
        {
            int currentLv = _selectedGeneral.SkillLevels.GetValueOrDefault(activeSkillId, 1);
            if (currentLv < activeSkill.MaxLevel)
            {
                int cost = currentLv * 50;
                var btn = new Button($"强化 {activeSkill.Name} ({cost}功)", new Rectangle(startX, startY + row * (btnHeight + gap), btnWidth, btnHeight));
                btn.NormalColor = new Color(60, 55, 30);
                btn.HoverColor = new Color(80, 75, 40);
                btn.Enabled = GameState.Instance.BattleMerit >= cost;
                string skillIdCopy = activeSkillId;
                btn.OnClick = () => LevelUpSkill(skillIdCopy);
                buttons.Add(btn);
                row++;
            }
        }

        if (passiveSkill != null)
        {
            int currentLv = _selectedGeneral.SkillLevels.GetValueOrDefault(passiveSkillId, 1);
            if (currentLv < passiveSkill.MaxLevel)
            {
                int cost = currentLv * 50;
                var btn = new Button($"强化 {passiveSkill.Name} ({cost}功)", new Rectangle(startX, startY + row * (btnHeight + gap), btnWidth, btnHeight));
                btn.NormalColor = new Color(60, 55, 30);
                btn.HoverColor = new Color(80, 75, 40);
                btn.Enabled = GameState.Instance.BattleMerit >= cost;
                string skillIdCopy = passiveSkillId;
                btn.OnClick = () => LevelUpSkill(skillIdCopy);
                buttons.Add(btn);
                row++;
            }
        }
        _skillButtons = buttons;
    }

    private void SwapSkill(string skillId, string skillType)
    {
        if (_selectedGeneral == null || _selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count) return;
        var gen = _allGenerals[_selectedGeneralIndex];

        bool success;
        if (skillType == "active")
        {
            success = GameState.Instance.SwapActiveSkill(gen.Id, skillId);
        }
        else
        {
            success = GameState.Instance.SwapPassiveSkill(gen.Id, skillId);
        }

        if (success)
        {
            _selectedGeneral = GameState.Instance.GetGeneralProgress(gen.Id);
            var skill = _allSkills.FirstOrDefault(s => s.Id == skillId);
            ShowNotify($"已切换{(skillType == "active" ? "主动" : "被动")}技能为 {skill?.Name ?? skillId}");
            CreateSkillButtons();
        }
    }

    private void LevelUpSkill(string skillId)
    {
        if (_selectedGeneral == null || _selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count) return;
        var gen = _allGenerals[_selectedGeneralIndex];

        if (GameState.Instance.LevelUpSkill(gen.Id, skillId))
        {
            _selectedGeneral = GameState.Instance.GetGeneralProgress(gen.Id);
            var skill = _allSkills.FirstOrDefault(s => s.Id == skillId);
            int newLv = _selectedGeneral?.SkillLevels.GetValueOrDefault(skillId, 1) ?? 1;
            ShowNotify($"{skill?.Name ?? skillId} 升级到 Lv.{newLv}");
            CreateSkillButtons();
        }
        else
        {
            ShowNotify("战功不足，无法升级技能");
        }
    }

    private void CreateSkillTreeButtons()
    {
        var buttons = new List<Button>();
        if (_selectedGeneral == null || _selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count)
        {
            _skillTreeButtons = buttons;
            return;
        }

        var gen = _allGenerals[_selectedGeneralIndex];
        var tree = GameState.Instance.AllSkillTrees.FirstOrDefault(st => st.GeneralId == gen.Id);
        if (tree == null)
        {
            _skillTreeButtons = buttons;
            return;
        }

        int startX = 620;
        int startY = 160;
        int nodeSize = 40;
        int gapX = 60;
        int gapY = 60;

        foreach (var node in tree.Nodes)
        {
            var progress = _selectedGeneral!;
            bool isUnlocked = progress.UnlockedSkillTreeNodes.Contains(node.NodeId);
            bool canUnlock = !isUnlocked &&
                             progress.SkillPoints >= node.Cost &&
                             node.ParentNodeIds.All(pid => progress.UnlockedSkillTreeNodes.Contains(pid));

            var btn = new Button(
                node.NodeId.Split('_').Last(),
                new Rectangle(startX + node.PositionX * gapX, startY + node.PositionY * gapY, nodeSize, nodeSize)
            );

            if (isUnlocked)
            {
                btn.NormalColor = new Color(180, 150, 50);
                btn.HoverColor = new Color(200, 170, 70);
            }
            else if (canUnlock)
            {
                btn.NormalColor = new Color(60, 120, 60);
                btn.HoverColor = new Color(80, 150, 80);
                string nodeId = node.NodeId;
                btn.OnClick = () => UnlockSkillTreeNode(nodeId);
            }
            else
            {
                btn.NormalColor = new Color(50, 45, 40);
                btn.HoverColor = new Color(60, 55, 50);
            }

            buttons.Add(btn);
        }
        _skillTreeButtons = buttons;
    }

    private void UnlockSkillTreeNode(string nodeId)
    {
        if (_selectedGeneral == null || _selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count) return;
        var gen = _allGenerals[_selectedGeneralIndex];

        if (GameState.Instance.UnlockSkillTreeNode(gen.Id, nodeId))
        {
            _selectedGeneral = GameState.Instance.GetGeneralProgress(gen.Id);
            ShowNotify($"解锁技能树节点: {nodeId}");
            CreateSkillTreeButtons();
        }
        else
        {
            ShowNotify("技能点不足或前置未解锁");
        }
    }

    private void CreateGeneralButtons()
    {
        var buttons = new List<Button>();
        int startX = 30;
        int startY = 80;
        int cardWidth = 180;
        int cardHeight = 80;
        int gapX = 15;
        int gapY = 10;

        for (int i = 0; i < _allGenerals.Count; i++)
        {
            var gen = _allGenerals[i];
            var progress = GameState.Instance.GetGeneralProgress(gen.Id);
            int col = i % 3;
            int row = i / 3;
            int x = startX + col * (cardWidth + gapX);
            int y = startY + row * (cardHeight + gapY);

            int idx = i;
            string label;
            if (progress?.IsUnlocked == false)
                label = $"[未解锁] {gen.Name}";
            else
                label = $"{gen.Name} Lv.{progress?.Level ?? 1}";

            var btn = new Button(label, new Rectangle(x, y, cardWidth, cardHeight));
            btn.NormalColor = progress?.IsUnlocked == false ? new Color(35, 30, 25) : new Color(45, 40, 35);
            btn.HoverColor = new Color(65, 55, 45);
            btn.OnClick = () => SelectGeneral(idx);
            buttons.Add(btn);
        }
        _generalButtons = buttons;
    }

    private string GetGeneralName(string id)
    {
        var gen = _allGenerals.FirstOrDefault(g => g.Id == id);
        return gen?.Name ?? "?";
    }

    private void SelectGeneral(int index)
    {
        if (index < 0 || index >= _allGenerals.Count) return;
        _selectedGeneralIndex = index;
        var gen = _allGenerals[index];
        _selectedGeneral = GameState.Instance.GetGeneralProgress(gen.Id);

        // Update upgrade button
        bool canUpgrade = _selectedGeneral?.IsUnlocked == true && _selectedGeneral.Level < _selectedGeneral.LevelCap;
        if (_upgradeButton != null) _upgradeButton.Enabled = canUpgrade;

        RefreshTab();
    }

    private void TryUpgradeGeneral()
    {
        if (_selectedGeneral == null || _selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count) return;
        var gen = _allGenerals[_selectedGeneralIndex];
        if (!_selectedGeneral.IsUnlocked) return;
        if (_selectedGeneral.Level >= _selectedGeneral.LevelCap)
        {
            ShowNotify("已达到等级上限");
            return;
        }

        bool success = GameState.Instance.TryLevelUpGeneral(gen.Id);
        if (success)
        {
            _selectedGeneral = GameState.Instance.GetGeneralProgress(gen.Id);
            ShowNotify($"{gen.Name} 升级到 Lv.{_selectedGeneral!.Level}");
            CreateGeneralButtons();
            RefreshTab();

            // Disable button if at cap
            if (_upgradeButton != null)
                _upgradeButton.Enabled = _selectedGeneral.Level < _selectedGeneral.LevelCap;
        }
        else
        {
            ShowNotify("战功不足，无法升级");
        }
    }

    private void GoBack()
    {
        _onSaveComplete?.Invoke();
        Game.SceneManager.ChangeScene(new WorldMapScene());
    }

    private void ShowNotify(string text)
    {
        _notifyText = text;
        _notifyTimer = 2.5f;
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        foreach (var btn in _generalButtons) btn.Update(Input);
        _backButton?.Update(Input);
        _upgradeButton?.Update(Input);
        foreach (var tab in _tabButtons) tab.Update(Input);
        foreach (var btn in _equipSlotButtons) btn.Update(Input);
        foreach (var btn in _skillButtons) btn.Update(Input);
        foreach (var btn in _skillTreeButtons) btn.Update(Input);

        if (_notifyTimer > 0)
            _notifyTimer -= dt;

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
        SpriteBatch.DrawString(_titleFont, "武将管理", new Vector2(30, 20), new Color(220, 190, 130));
        SpriteBatch.Draw(_pixel, new Rectangle(0, 65, GameSettings.ScreenWidth, 1), new Color(80, 65, 45));

        // Top HUD
        SpriteBatch.DrawString(_font, $"战功: {GameState.Instance.BattleMerit}", new Vector2(GameSettings.ScreenWidth - 200, 25), new Color(255, 200, 80));

        // General cards
        foreach (var btn in _generalButtons) btn.Draw(SpriteBatch, _font, _pixel);

        // Selected general highlight
        if (_selectedGeneralIndex >= 0 && _selectedGeneralIndex < _generalButtons.Count)
        {
            var btn = _generalButtons[_selectedGeneralIndex];
            DrawBorder(btn.Bounds, new Color(255, 220, 100), 3);
        }

        // Tab navigation + Detail panel
        if (_selectedGeneralIndex >= 0)
        {
            // Draw panel background first, then tabs on top
            DrawDetailPanel();

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                var tabBtn = _tabButtons[i];
                if (i == _selectedTabIndex)
                    tabBtn.NormalColor = new Color(80, 70, 50);
                else
                    tabBtn.NormalColor = new Color(50, 45, 35);
                tabBtn.Draw(SpriteBatch, _smallFont, _pixel);
            }
        }

        // Buttons
        _backButton?.Draw(SpriteBatch, _font, _pixel);
        if (_selectedGeneral != null && _selectedGeneral.IsUnlocked)
        {
            _upgradeButton?.Draw(SpriteBatch, _font, _pixel);
        }

        // Notification
        if (_notifyTimer > 0 && !string.IsNullOrEmpty(_notifyText))
        {
            float alpha = Math.Min(1f, _notifyTimer);
            var size = _font.MeasureString(_notifyText);
            float nx = (GameSettings.ScreenWidth - size.X) / 2;
            float ny = GameSettings.ScreenHeight / 2 - 30;
            // Background
            SpriteBatch.Draw(_pixel, new Rectangle((int)nx - 15, (int)ny - 5, (int)size.X + 30, (int)size.Y + 10),
                new Color(0, 0, 0, (int)(180 * alpha)));
            SpriteBatch.DrawString(_font, _notifyText, new Vector2(nx, ny), new Color(255, 230, 100) * alpha);
        }

        SpriteBatch.End();
    }

    private void DrawDetailPanel()
    {
        if (_selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count) return;
        var gen = _allGenerals[_selectedGeneralIndex];
        var progress = GameState.Instance.GetGeneralProgress(gen.Id);
        if (progress == null) return;

        int panelX = 600;
        int panelY = 118;
        int panelWidth = 550;
        int panelHeight = 382;

        SpriteBatch.Draw(_pixel, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(40, 35, 28));
        DrawBorder(new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(100, 85, 60), 2);

        SpriteBatch.DrawString(_font, $"{gen.Name}", new Vector2(panelX + 15, panelY + 10), new Color(220, 190, 130));
        SpriteBatch.DrawString(_smallFont, $"Lv.{progress.Level}/{progress.LevelCap}",
            new Vector2(panelX + 15 + _font.MeasureString(gen.Name).X + 10, panelY + 14), new Color(200, 180, 120));
        SpriteBatch.DrawString(_smallFont, gen.Title, new Vector2(panelX + 15, panelY + 35), new Color(160, 140, 100));

        switch (_selectedTabIndex)
        {
            case 0:
                DrawStatsTab(panelX, panelY, gen, progress);
                break;
            case 1:
                DrawEquipTab(panelX, panelY, panelWidth, gen, progress);
                break;
            case 2:
                DrawSkillsTab(panelX, panelY, gen, progress);
                break;
            case 3:
                DrawSkillTreeTab(panelX, panelY, panelWidth, gen, progress);
                break;
        }

        foreach (var btn in _equipSlotButtons) btn.Draw(SpriteBatch, _smallFont, _pixel);
        foreach (var btn in _skillButtons) btn.Draw(SpriteBatch, _smallFont, _pixel);
        foreach (var btn in _skillTreeButtons) btn.Draw(SpriteBatch, _smallFont, _pixel);
    }

    private void DrawStatsTab(int panelX, int panelY, GeneralData gen, GeneralProgress progress)
    {
        int statStartY = panelY + 60;
        DrawStatBar(panelX + 15, statStartY, "武力", gen.Strength, progress.Level, new Color(200, 80, 80));
        DrawStatBar(panelX + 15, statStartY + 35, "智力", gen.Intelligence, progress.Level, new Color(80, 120, 200));
        DrawStatBar(panelX + 15, statStartY + 70, "统率", gen.Leadership, progress.Level, new Color(80, 200, 120));
        DrawStatBar(panelX + 15, statStartY + 105, "速度", gen.Speed, progress.Level, new Color(200, 160, 80));

        // XP Bar
        int xpBarY = statStartY + 150;
        int barWidth = 200;
        int barHeight = 16;
        float xpRatio = progress.XpProgressRatio;

        SpriteBatch.DrawString(_smallFont, "经验", new Vector2(panelX + 15, xpBarY + 2), Color.White);
        SpriteBatch.Draw(_pixel, new Rectangle(panelX + 60, xpBarY, barWidth, barHeight), new Color(20, 15, 10));
        int fillW = (int)(barWidth * xpRatio);
        if (fillW > 0)
            SpriteBatch.Draw(_pixel, new Rectangle(panelX + 60, xpBarY, fillW, barHeight), new Color(80, 180, 80));

        if (progress.Level >= progress.LevelCap)
        {
            SpriteBatch.DrawString(_smallFont, "MAX",
                new Vector2(panelX + 65 + barWidth, xpBarY - 1), new Color(255, 200, 80));
        }
        else
        {
            SpriteBatch.DrawString(_smallFont, $"{progress.Experience}/{progress.XpToNextLevel}",
                new Vector2(panelX + 65 + barWidth, xpBarY - 1), new Color(150, 200, 150));
        }

        // Upgrade cost info
        if (progress.Level < progress.LevelCap)
        {
            SpriteBatch.DrawString(_smallFont, $"升级消耗: {progress.LevelUpCost} 战功",
                new Vector2(panelX + 15, xpBarY + 25), new Color(180, 160, 120));
        }

        // Bond display
        var bonds = GameState.Instance.GetActiveBonds();
        if (bonds.Any())
        {
            int bondY = xpBarY + 50;
            SpriteBatch.DrawString(_smallFont, "激活羁绊:", new Vector2(panelX + 15, bondY), new Color(255, 220, 100));
            foreach (var bond in bonds.Where(b => b.RequiredGeneralIds.Contains(gen.Id)))
            {
                SpriteBatch.DrawString(_smallFont, $"* {bond.Name}: {bond.Description}",
                    new Vector2(panelX + 15, bondY + 20), new Color(255, 200, 80));
                bondY += 20;
            }
        }
    }

    private void DrawEquipTab(int panelX, int panelY, int panelWidth, GeneralData gen, GeneralProgress progress)
    {
        SpriteBatch.DrawString(_font, "装备槽位 (点击选择)", new Vector2(panelX + 15, panelY + 60), new Color(220, 190, 130));

        int infoY = panelY + 260;
        SpriteBatch.Draw(_pixel, new Rectangle(panelX + 10, infoY, panelWidth - 20, 100), new Color(30, 25, 18));

        int y = infoY + 10;
        foreach (var slotType in _equipSlotTypes)
        {
            if (progress.EquippedItems.TryGetValue(slotType, out var equipId))
            {
                var equip = GameState.Instance.AllEquipment.FirstOrDefault(e => e.Id == equipId);
                if (equip != null)
                {
                    string statName = equip.StatType switch
                    {
                        "strength" => "武力",
                        "intelligence" => "智力",
                        "leadership" => "统率",
                        "speed" => "速度",
                        _ => "?"
                    };
                    SpriteBatch.DrawString(_smallFont, $"{equip.Name} (+{equip.StatBonus} {statName})",
                        new Vector2(panelX + 20, y), GetRarityColor(equip.Rarity));
                    y += 20;
                }
            }
        }
    }

    private void DrawSkillsTab(int panelX, int panelY, GeneralData gen, GeneralProgress progress)
    {
        SpriteBatch.DrawString(_font, "技能配置", new Vector2(panelX + 15, panelY + 60), new Color(220, 190, 130));

        // Skill description area
        int descY = panelY + 300;
        SpriteBatch.Draw(_pixel, new Rectangle(panelX + 10, descY, 530, 60), new Color(30, 25, 18));

        var activeSkillId = progress.GetActiveSkillId();
        var passiveSkillId = progress.GetPassiveSkillId();
        var activeSkill = _allSkills.FirstOrDefault(s => s.Id == activeSkillId);
        var passiveSkill = _allSkills.FirstOrDefault(s => s.Id == passiveSkillId);

        int dy = descY + 5;
        if (activeSkill != null)
        {
            SpriteBatch.DrawString(_smallFont, $"主动: {activeSkill.Description}",
                new Vector2(panelX + 20, dy), new Color(180, 160, 120));
            dy += 18;
        }
        if (passiveSkill != null)
        {
            SpriteBatch.DrawString(_smallFont, $"被动: {passiveSkill.Description}",
                new Vector2(panelX + 20, dy), new Color(150, 140, 120));
        }
    }

    private void DrawSkillTreeTab(int panelX, int panelY, int panelWidth, GeneralData gen, GeneralProgress progress)
    {
        var tree = GameState.Instance.AllSkillTrees.FirstOrDefault(st => st.GeneralId == gen.Id);
        if (tree == null)
        {
            SpriteBatch.DrawString(_font, "该武将无技能树", new Vector2(panelX + 15, panelY + 100), new Color(150, 140, 120));
            return;
        }

        SpriteBatch.DrawString(_font, "技能树", new Vector2(panelX + 15, panelY + 60), new Color(220, 190, 130));
        SpriteBatch.DrawString(_smallFont, $"技能点: {progress.SkillPoints}",
            new Vector2(panelX + 100, panelY + 65), new Color(255, 220, 100));

        int descY = panelY + 280;
        SpriteBatch.Draw(_pixel, new Rectangle(panelX + 10, descY, panelWidth - 20, 80), new Color(30, 25, 18));

        foreach (var node in tree.Nodes)
        {
            bool isUnlocked = progress.UnlockedSkillTreeNodes.Contains(node.NodeId);
            if (isUnlocked)
            {
                SpriteBatch.DrawString(_smallFont, $"* {node.Description}",
                    new Vector2(panelX + 20, descY + 5), new Color(255, 220, 100));
                descY += 18;
            }
        }
    }

    private Color GetRarityColor(string rarity)
    {
        return rarity switch
        {
            "common" => new Color(150, 150, 150),
            "rare" => new Color(100, 150, 255),
            "epic" => new Color(180, 120, 255),
            "legendary" => new Color(255, 180, 50),
            _ => Color.White
        };
    }

    private void DrawStatBar(int x, int y, string label, int baseValue, int level, Color color)
    {
        int barWidth = 150;
        int barHeight = 12;
        float effectiveValue = baseValue * (1f + (level - 1) * 0.03f);
        float ratio = MathHelper.Clamp(effectiveValue / 120f, 0, 1);

        SpriteBatch.DrawString(_smallFont, label, new Vector2(x, y + 2), Color.White);
        SpriteBatch.Draw(_pixel, new Rectangle(x + 50, y, barWidth, barHeight), new Color(20, 15, 10));
        int fillW = (int)(barWidth * ratio);
        if (fillW > 0)
        {
            SpriteBatch.Draw(_pixel, new Rectangle(x + 50, y, fillW, barHeight), color);
            SpriteBatch.Draw(_pixel, new Rectangle(x + 50, y, fillW, 2), color * 0.6f);
        }
        SpriteBatch.DrawString(_smallFont, $"{(int)effectiveValue}", new Vector2(x + 55 + barWidth, y - 1), color);
    }

    private void DrawBorder(Rectangle rect, Color color, int thickness)
    {
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
