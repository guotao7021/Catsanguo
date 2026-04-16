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
    private List<int> _filteredGeneralIndices = new(); // 当前显示的武将索引（原_allGenerals中的位置）
    private GeneralProgress? _selectedGeneral;
    private Action? _onSaveComplete;
    private float _rosterScrollOffset = 0f; // 武将列表滚动偏移

    // Notification
    private string _notifyText = "";
    private float _notifyTimer = 0f;
    
    // 升级加点选择
    private bool _showStatSelect = false;
    private List<Button> _statSelectButtons = new();
    private static readonly (string key, string label)[] StatOptions = {
        ("strength", "武力+1"),
        ("intelligence", "智力+1"),
        ("command", "统帅+1"),
        ("politics", "政治+1"),
        ("charisma", "魅力+1")
    };
    
    // 赏赐系统
    private bool _showRewardSelect = false;
    private List<Button> _rewardButtons = new();
    private static readonly (int gold, string label)[] RewardOptions = {
        (50, "赏赐50金"),
        (100, "赏赐100金"),
        (200, "赏赐200金")
    };

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
        int startY = 200;
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
        int startY = 200;
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
        int startY = 200;
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
        _filteredGeneralIndices.Clear();
        _rosterScrollOffset = 0f;

        int startX = 30;
        int startY = 80;
        int cardWidth = 530;
        int cardHeight = 50;
        int gapY = 6;

        string playerFactionId = GameState.Instance.PlayerFactionId;

        for (int i = 0; i < _allGenerals.Count; i++)
        {
            var gen = _allGenerals[i];
            var progress = GameState.Instance.GetGeneralProgress(gen.Id);

            // 只显示本势力已解锁的武将
            if (!string.IsNullOrEmpty(gen.ForceId) && gen.ForceId != playerFactionId)
                continue;
            if (progress?.IsUnlocked != true)
                continue;

            _filteredGeneralIndices.Add(i);

            int row = _filteredGeneralIndices.Count - 1;
            int x = startX;
            int y = startY + row * (cardHeight + gapY);

            int idx = i;
            string label = $"{gen.Name} Lv.{progress?.Level ?? 1}  武{gen.Strength} 智{gen.Intelligence} 统{gen.Command} 魅{gen.Charisma} 忠{gen.Loyalty}";

            var btn = new Button(label, new Rectangle(x, y, cardWidth, cardHeight));
            btn.NormalColor = new Color(45, 40, 35);
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
        if (!_selectedGeneral.IsUnlocked) return;
        if (_selectedGeneral.Level >= _selectedGeneral.LevelCap)
        {
            ShowNotify("已达到等级上限");
            return;
        }
        if (GameState.Instance.BattleMerit < _selectedGeneral.LevelUpCost)
        {
            ShowNotify("战功不足，无法升级");
            return;
        }

        // 显示属性选择面板
        _showStatSelect = true;
        _statSelectButtons.Clear();
        int dialogX = GameSettings.ScreenWidth / 2 - 100;
        int dialogY = GameSettings.ScreenHeight / 2 - 80;
        for (int i = 0; i < StatOptions.Length; i++)
        {
            var (key, label) = StatOptions[i];
            var btn = new Button(label, new Rectangle(dialogX, dialogY + i * 36, 200, 30));
            btn.NormalColor = new Color(40, 50, 60);
            btn.HoverColor = new Color(60, 80, 100);
            string statKey = key;
            btn.OnClick = () => ConfirmStatUpgrade(statKey);
            _statSelectButtons.Add(btn);
        }
    }

    private void ConfirmStatUpgrade(string statType)
    {
        if (_selectedGeneral == null || _selectedGeneralIndex < 0 || _selectedGeneralIndex >= _allGenerals.Count) return;
        var gen = _allGenerals[_selectedGeneralIndex];

        bool success = GameState.Instance.TryLevelUpGeneral(gen.Id, statType);
        if (success)
        {
            _selectedGeneral = GameState.Instance.GetGeneralProgress(gen.Id);
            string statLabel = StatOptions.FirstOrDefault(s => s.key == statType).label ?? statType;
            ShowNotify($"{gen.Name} 升级到 Lv.{_selectedGeneral!.Level} ({statLabel})");
            CreateGeneralButtons();
            RefreshTab();

            if (_upgradeButton != null)
                _upgradeButton.Enabled = _selectedGeneral.Level < _selectedGeneral.LevelCap;
        }
        else
        {
            ShowNotify("升级失败");
        }
        _showStatSelect = false;
    }

    private void OpenRewardDialog()
    {
        if (_selectedGeneral == null) return;
        _showRewardSelect = true;
        _rewardButtons.Clear();
        int dialogX = GameSettings.ScreenWidth / 2 - 100;
        int dialogY = GameSettings.ScreenHeight / 2 - 60;
        for (int i = 0; i < RewardOptions.Length; i++)
        {
            var (gold, label) = RewardOptions[i];
            var btn = new Button(label, new Rectangle(dialogX, dialogY + i * 36, 200, 30));
            btn.NormalColor = new Color(50, 45, 30);
            btn.HoverColor = new Color(80, 70, 40);
            int amount = gold;
            btn.OnClick = () => ConfirmReward(amount);
            _rewardButtons.Add(btn);
        }
    }

    private void ConfirmReward(int goldAmount)
    {
        if (_selectedGeneral == null) return;
        string cityId = _selectedGeneral.CurrentCityId;
        if (string.IsNullOrEmpty(cityId))
        {
            ShowNotify("武将未驻扎在城池");
            _showRewardSelect = false;
            return;
        }
        bool success = GameState.Instance.GrantReward(_selectedGeneral.Data.Id, cityId, goldAmount, out string msg);
        ShowNotify(msg);
        if (success)
        {
            _selectedGeneral = GameState.Instance.GetGeneralProgress(_selectedGeneral.Data.Id);
        }
        _showRewardSelect = false;
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

        // --- 武将列表滚动 ---
        int listTop = 80;
        int listBottom = GameSettings.ScreenHeight - 70;
        int cardHeight = 50;
        int gapY = 6;
        int stride = cardHeight + gapY;

        int scrollDelta = Input.ScrollWheelDelta;
        if (scrollDelta != 0)
        {
            var mousePos = Input.MousePosition;
            if (mousePos.X >= 30 && mousePos.X <= 560 && mousePos.Y >= listTop && mousePos.Y <= listBottom)
            {
                _rosterScrollOffset -= scrollDelta * 0.3f;
                int totalHeight = _generalButtons.Count * stride;
                int visibleHeight = listBottom - listTop;
                float maxScroll = Math.Max(0, totalHeight - visibleHeight);
                _rosterScrollOffset = MathHelper.Clamp(_rosterScrollOffset, 0, maxScroll);
            }
        }

        // 更新按钮位置并只更新可见区域的按钮
        for (int i = 0; i < _generalButtons.Count; i++)
        {
            var btn = _generalButtons[i];
            int baseY = listTop + i * stride;
            int drawY = baseY - (int)_rosterScrollOffset;
            btn.Bounds = new Rectangle(btn.Bounds.X, drawY, btn.Bounds.Width, btn.Bounds.Height);

            // 只更新可见区域内的按钮（避免点击不可见的按钮）
            if (drawY + cardHeight >= listTop && drawY <= listBottom)
                btn.Update(Input);
        }

        _backButton?.Update(Input);
        _upgradeButton?.Update(Input);
        foreach (var tab in _tabButtons) tab.Update(Input);
        foreach (var btn in _equipSlotButtons) btn.Update(Input);
        foreach (var btn in _skillButtons) btn.Update(Input);
        foreach (var btn in _skillTreeButtons) btn.Update(Input);
        
        // 升级加点选择面板
        if (_showStatSelect)
        {
            foreach (var btn in _statSelectButtons) btn.Update(Input);
            if (Input.IsKeyPressed(Keys.Escape))
            {
                _showStatSelect = false;
                return;
            }
        }
        
        // 赏赐金额选择面板
        if (_showRewardSelect)
        {
            foreach (var btn in _rewardButtons) btn.Update(Input);
            if (Input.IsKeyPressed(Keys.Escape))
            {
                _showRewardSelect = false;
                return;
            }
        }

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

        // General cards (只绘制可见区域内的按钮)
        int listTop = 80;
        int listBottom = GameSettings.ScreenHeight - 70;
        int cardHeight = 50;
        foreach (var btn in _generalButtons)
        {
            if (btn.Bounds.Y + cardHeight >= listTop && btn.Bounds.Y <= listBottom)
                btn.Draw(SpriteBatch, _font, _pixel);
        }

        // Selected general highlight (通过 _filteredGeneralIndices 映射到正确的按钮索引)
        int selectedBtnIdx = _filteredGeneralIndices.IndexOf(_selectedGeneralIndex);
        if (selectedBtnIdx >= 0 && selectedBtnIdx < _generalButtons.Count)
        {
            var btn = _generalButtons[selectedBtnIdx];
            if (btn.Bounds.Y + cardHeight >= listTop && btn.Bounds.Y <= listBottom)
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

        // 升级加点选择面板
        if (_showStatSelect)
        {
            // 半透明遮罩
            SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), new Color(0, 0, 0, 150));
            
            int dialogW = 240;
            int dialogH = 36 * StatOptions.Length + 50;
            int dialogX = GameSettings.ScreenWidth / 2 - dialogW / 2;
            int dialogY = GameSettings.ScreenHeight / 2 - dialogH / 2;
            
            SpriteBatch.Draw(_pixel, new Rectangle(dialogX - 10, dialogY - 40, dialogW + 20, dialogH + 50), new Color(50, 45, 35));
            DrawBorder(new Rectangle(dialogX - 10, dialogY - 40, dialogW + 20, dialogH + 50), new Color(120, 100, 70), 2);
            SpriteBatch.DrawString(_font, "选择加点属性", new Vector2(dialogX + 10, dialogY - 32), new Color(255, 220, 100));
            
            foreach (var btn in _statSelectButtons) btn.Draw(SpriteBatch, _font, _pixel);
        }

        // 赏赐金额选择面板
        if (_showRewardSelect)
        {
            SpriteBatch.Draw(_pixel, new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight), new Color(0, 0, 0, 150));
            
            int dialogW = 240;
            int dialogH = 36 * RewardOptions.Length + 50;
            int dialogX = GameSettings.ScreenWidth / 2 - dialogW / 2;
            int dialogY = GameSettings.ScreenHeight / 2 - dialogH / 2;
            
            SpriteBatch.Draw(_pixel, new Rectangle(dialogX - 10, dialogY - 40, dialogW + 20, dialogH + 50), new Color(50, 45, 35));
            DrawBorder(new Rectangle(dialogX - 10, dialogY - 40, dialogW + 20, dialogH + 50), new Color(120, 100, 70), 2);
            SpriteBatch.DrawString(_font, "赏赐武将", new Vector2(dialogX + 10, dialogY - 32), new Color(255, 220, 100));
            
            foreach (var btn in _rewardButtons) btn.Draw(SpriteBatch, _font, _pixel);
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
        int bonusStr = progress.BonusStats.GetValueOrDefault("strength");
        int bonusInt = progress.BonusStats.GetValueOrDefault("intelligence");
        int bonusCmd = progress.BonusStats.GetValueOrDefault("command");
        int bonusSpd = progress.BonusStats.GetValueOrDefault("speed", 0);
        int bonusPol = progress.BonusStats.GetValueOrDefault("politics");
        int bonusCha = progress.BonusStats.GetValueOrDefault("charisma");
        
        DrawStatBar(panelX + 15, statStartY, "武力", gen.Strength, progress.Level, new Color(200, 80, 80), bonusStr);
        DrawStatBar(panelX + 15, statStartY + 28, "智力", gen.Intelligence, progress.Level, new Color(80, 120, 200), bonusInt);
        DrawStatBar(panelX + 15, statStartY + 56, "统率", gen.Leadership, progress.Level, new Color(80, 200, 120), bonusCmd);
        DrawStatBar(panelX + 15, statStartY + 84, "速度", gen.Speed, progress.Level, new Color(200, 160, 80), bonusSpd);
        DrawStatBar(panelX + 15, statStartY + 112, "政治", gen.Politics, progress.Level, new Color(160, 120, 200), bonusPol);
        DrawStatBar(panelX + 15, statStartY + 140, "魅力", gen.Charisma, progress.Level, new Color(200, 140, 180), bonusCha);

        // 忠诚度 (使用progress的实时忠诚度)
        int loyaltyY = statStartY + 172;
        int loyaltyBarW = 150;
        int loyaltyBarH = 12;
        int loyalty = progress.Loyalty;
        float loyaltyRatio = MathHelper.Clamp(loyalty / 100f, 0, 1);
        Color loyaltyColor = loyalty >= 80 ? new Color(80, 200, 80) :
                             loyalty >= 50 ? new Color(200, 200, 80) : new Color(200, 80, 80);
        SpriteBatch.DrawString(_smallFont, "忠诚", new Vector2(panelX + 15, loyaltyY + 2), Color.White);
        SpriteBatch.Draw(_pixel, new Rectangle(panelX + 65, loyaltyY, loyaltyBarW, loyaltyBarH), new Color(20, 15, 10));
        int loyaltyFillW = (int)(loyaltyBarW * loyaltyRatio);
        if (loyaltyFillW > 0)
            SpriteBatch.Draw(_pixel, new Rectangle(panelX + 65, loyaltyY, loyaltyFillW, loyaltyBarH), loyaltyColor);
        SpriteBatch.DrawString(_smallFont, $"{loyalty}", new Vector2(panelX + 70 + loyaltyBarW, loyaltyY - 1), loyaltyColor);
        
        // 赏赐按钮（点击区域）
        int rewardBtnX = panelX + 120 + loyaltyBarW;
        var rewardRect = new Rectangle(rewardBtnX, loyaltyY - 2, 50, 16);
        bool hoverReward = Input.IsMouseInRect(rewardRect);
        Color rewardColor = hoverReward ? new Color(255, 220, 100) : new Color(180, 160, 100);
        SpriteBatch.Draw(_pixel, rewardRect, hoverReward ? new Color(60, 50, 30) : new Color(40, 35, 25));
        SpriteBatch.DrawString(_smallFont, "赏赐", new Vector2(rewardBtnX + 8, loyaltyY - 1), rewardColor);
        if (hoverReward && Input.IsMouseClicked())
            OpenRewardDialog();

        // XP Bar
        int xpBarY = statStartY + 200;
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

        int infoY = panelY + 290;
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

    private void DrawStatBar(int x, int y, string label, int baseValue, int level, Color color, int bonus = 0)
    {
        int barWidth = 150;
        int barHeight = 12;
        float effectiveValue = (baseValue + bonus) * (1f + (level - 1) * 0.03f);
        float ratio = MathHelper.Clamp(effectiveValue / 120f, 0, 1);

        SpriteBatch.DrawString(_smallFont, label, new Vector2(x, y + 2), Color.White);
        SpriteBatch.Draw(_pixel, new Rectangle(x + 50, y, barWidth, barHeight), new Color(20, 15, 10));
        int fillW = (int)(barWidth * ratio);
        if (fillW > 0)
        {
            SpriteBatch.Draw(_pixel, new Rectangle(x + 50, y, fillW, barHeight), color);
            SpriteBatch.Draw(_pixel, new Rectangle(x + 50, y, fillW, 2), color * 0.6f);
        }
        string valueText = bonus > 0 ? $"{baseValue}+{bonus} ({(int)effectiveValue})" : $"{(int)effectiveValue}";
        SpriteBatch.DrawString(_smallFont, valueText, new Vector2(x + 55 + barWidth, y - 1), color);
    }

    private void DrawBorder(Rectangle rect, Color color, int thickness)
    {
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        SpriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
