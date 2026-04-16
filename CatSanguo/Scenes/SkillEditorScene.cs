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
using CatSanguo.UI.Battle;

namespace CatSanguo.Scenes;

/// <summary>
/// 技能编辑器场景 - 可视化编辑技能配置
/// </summary>
public class SkillEditorScene : Scene
{
    private List<SkillData> _skills = new();
    private int _selectedIndex = -1;
    private SkillData? _selectedSkill;
    
    // 编辑状态
    private bool _isEditing;
    private string _editField = "";
    private string _editValue = "";
    private bool _showEditDialog;
    
    // UI元素
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private SpriteFontBase _smallFont;
    
    // 滚动
    private int _scrollOffset = 0;
    private const int ItemHeight = 50;
    private const int MaxVisibleItems = 12;
    
    // 按钮
    private List<EditorButton> _buttons = new();
    
    // 新技能模板
    private bool _showNewSkillDialog;
    private string _newSkillName = "";
    private string _newSkillType = "active";
    private string _newSkillEffectType = "damage";
    private string _newSkillTargetMode = "SingleTarget";
    
    // 搜索
    private string _searchText = "";
    private bool _isSearching;

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;
        _smallFont = Game.SmallFont;
        
        // 加载技能数据
        LoadSkills();
        
        // 初始化按钮
        InitializeButtons();
    }

    private void LoadSkills()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "skills.json");
        if (File.Exists(path))
        {
            _skills = DataLoader.Load<List<SkillData>>(path) ?? new();
        }
        else
        {
            _skills = new();
        }
    }

    private void SaveSkills()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "skills.json");
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            string json = System.Text.Json.JsonSerializer.Serialize(_skills, options);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SkillEditor] Save failed: {ex.Message}");
        }
    }

    private void InitializeButtons()
    {
        _buttons.Clear();
        
        int x = GameSettings.ScreenWidth - 180;
        int y = 20;
        int w = 160;
        int h = 40;
        
        _buttons.Add(new EditorButton("新建技能", new Rectangle(x, y, w, h), CreateNewSkill));
        y += h + 5;
        _buttons.Add(new EditorButton("删除技能", new Rectangle(x, y, w, h), DeleteSelectedSkill));
        y += h + 5;
        _buttons.Add(new EditorButton("保存", new Rectangle(x, y, w, h), SaveSkills));
        y += h + 5;
        _buttons.Add(new EditorButton("返回列表", new Rectangle(x, y, w, h), () => 
        {
            Game.SceneManager.ChangeScene(new StageSelectScene());
        }));
    }

    private void CreateNewSkill()
    {
        _showNewSkillDialog = true;
        _newSkillName = "";
        _newSkillType = "active";
        _newSkillEffectType = "damage";
        _newSkillTargetMode = "SingleTarget";
    }

    private void ConfirmNewSkill()
    {
        if (string.IsNullOrEmpty(_newSkillName)) return;
        
        var newSkill = new SkillData
        {
            Id = _newSkillName.ToLower().Replace(" ", "_"),
            Name = _newSkillName,
            Description = "新技能",
            Type = _newSkillType,
            EffectType = _newSkillEffectType,
            TargetMode = _newSkillTargetMode,
            Coefficient = 1.0f,
            Cooldown = 10f,
            CastTime = 0.5f,
            MaxLevel = 10,
            LevelUpCost = 50,
            CooldownReductionPerLevel = 0.5f,
            CoefficientIncreasePerLevel = 0.1f
        };
        
        _skills.Add(newSkill);
        _showNewSkillDialog = false;
        SaveSkills();
    }

    private void DeleteSelectedSkill()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _skills.Count) return;
        
        _skills.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        _selectedSkill = null;
        SaveSkills();
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // 更新按钮
        foreach (var btn in _buttons)
            btn.Update(Input);
        
        // 搜索框
        if (_isSearching)
        {
            // 简单的键盘输入处理
            var keys = Keyboard.GetState();
            foreach (var k in keys.GetPressedKeys())
            {
                if (Input.IsKeyPressed(k))
                {
                    if (k == Keys.Back && _searchText.Length > 0)
                        _searchText = _searchText.Substring(0, _searchText.Length - 1);
                    else if (k == Keys.Enter)
                        _isSearching = false;
                    else if (k.ToString().Length == 1)
                        _searchText += k.ToString();
                }
            }
        }
        
        // 技能列表选择
        if (!_showNewSkillDialog && !_showEditDialog && !_isSearching)
        {
            if (Input.IsMouseClicked())
            {
                Vector2 mp = Input.MousePosition;
                
                // 检查技能列表点击
                int listX = 20;
                int listY = 80 - _scrollOffset * ItemHeight;
                
                var filteredSkills = GetFilteredSkills();
                for (int i = 0; i < filteredSkills.Count; i++)
                {
                    Rectangle itemRect = new Rectangle(listX, listY + i * ItemHeight, 400, ItemHeight - 5);
                    if (itemRect.Contains(mp.ToPoint()))
                    {
                        _selectedIndex = _skills.IndexOf(filteredSkills[i]);
                        _selectedSkill = filteredSkills[i];
                        break;
                    }
                }
                
                // 滚动条
                int scrollX = 430;
                int scrollY = 80;
                int scrollH = MaxVisibleItems * ItemHeight;
                if (mp.X >= scrollX && mp.X <= scrollX + 15 && mp.Y >= scrollY && mp.Y <= scrollY + scrollH)
                {
                    // 简单的滚动条交互
                }
            }
            
            // 键盘滚动
            if (Input.IsKeyPressed(Keys.Down))
                _scrollOffset = Math.Min(_scrollOffset + 1, Math.Max(0, GetFilteredSkills().Count - MaxVisibleItems));
            if (Input.IsKeyPressed(Keys.Up))
                _scrollOffset = Math.Max(0, _scrollOffset - 1);
        }
        
        // 编辑对话框
        if (_showEditDialog && Input.IsKeyPressed(Keys.Escape))
        {
            _showEditDialog = false;
        }
        
        // 新技能对话框
        if (_showNewSkillDialog)
        {
            // 简单的键盘输入
            var keys = Keyboard.GetState();
            foreach (var k in keys.GetPressedKeys())
            {
                if (Input.IsKeyPressed(k))
                {
                    if (k == Keys.Back && _newSkillName.Length > 0)
                        _newSkillName = _newSkillName.Substring(0, _newSkillName.Length - 1);
                    else if (k == Keys.Enter)
                        ConfirmNewSkill();
                    else if (k == Keys.Escape)
                        _showNewSkillDialog = false;
                    else if (k.ToString().Length == 1)
                        _newSkillName += k.ToString();
                }
            }
        }
        
        // 双击编辑技能
        if (Input.IsMouseDoubleClicked() && _selectedSkill != null)
        {
            _showEditDialog = true;
            _editField = "";
        }
    }

    private List<SkillData> GetFilteredSkills()
    {
        if (string.IsNullOrEmpty(_searchText))
            return _skills;
        
        return _skills.Where(s => 
            s.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
            s.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
            s.EffectType.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
        ).ToList();
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(30, 28, 25));
        SpriteBatch.Begin();
        
        // 标题
        SpriteBatch.DrawString(_titleFont, "技能编辑器", new Vector2(20, 15), new Color(255, 220, 100));
        
        // 搜索框
        DrawSearchBox();
        
        // 技能列表
        DrawSkillList();
        
        // 技能详情
        if (_selectedSkill != null && !_showNewSkillDialog)
        {
            DrawSkillDetail();
        }
        
        // 按钮
        foreach (var btn in _buttons)
            btn.Draw(SpriteBatch, _font, _pixel);
        
        // 新技能对话框
        if (_showNewSkillDialog)
        {
            DrawNewSkillDialog();
        }
        
        // 编辑对话框
        if (_showEditDialog && _selectedSkill != null)
        {
            DrawEditDialog();
        }
        
        // 提示
        SpriteBatch.DrawString(_smallFont, "双击技能编辑 | 方向键滚动 | ESC关闭对话框", 
            new Vector2(20, GameSettings.ScreenHeight - 25), new Color(120, 110, 90));
        
        SpriteBatch.End();
    }

    private void DrawSearchBox()
    {
        int x = 20;
        int y = 55;
        int w = 400;
        int h = 25;
        
        SpriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), new Color(40, 35, 30));
        UIHelper.DrawBorder(SpriteBatch, _pixel, new Rectangle(x, y, w, h), new Color(80, 70, 50), 1);
        
        string displayText = _isSearching ? _searchText + "|" : (_searchText != "" ? _searchText : "搜索技能...");
        SpriteBatch.DrawString(_smallFont, displayText, new Vector2(x + 5, y + 3), new Color(200, 180, 140));
        
        if (Input.IsMouseClicked() && new Rectangle(x, y, w, h).Contains(Input.MousePosition.ToPoint()))
        {
            _isSearching = true;
        }
    }

    private void DrawSkillList()
    {
        int x = 20;
        int y = 80;
        int w = 400;
        var filtered = GetFilteredSkills();
        int visibleCount = Math.Min(filtered.Count, MaxVisibleItems);
        
        // 列表背景
        SpriteBatch.Draw(_pixel, new Rectangle(x, y, w, visibleCount * ItemHeight), new Color(25, 22, 18));
        UIHelper.DrawBorder(SpriteBatch, _pixel, new Rectangle(x, y, w, visibleCount * ItemHeight), new Color(60, 50, 40), 1);
        
        int drawY = y - _scrollOffset * ItemHeight;
        for (int i = 0; i < filtered.Count; i++)
        {
            if (i < _scrollOffset || i >= _scrollOffset + MaxVisibleItems + 1) continue;
            
            var skill = filtered[i];
            Rectangle itemRect = new Rectangle(x + 2, drawY + (i - _scrollOffset) * ItemHeight, w - 4, ItemHeight - 5);
            
            // 选中高亮
            if (_skills.IndexOf(skill) == _selectedIndex)
            {
                SpriteBatch.Draw(_pixel, itemRect, new Color(60, 50, 35));
                UIHelper.DrawBorder(SpriteBatch, _pixel, itemRect, new Color(255, 220, 100), 2);
            }
            
            // 技能名称
            string typeIcon = skill.Type == "active" ? "[主]" : "[被]";
            Color typeColor = skill.Type == "active" ? new Color(255, 150, 50) : new Color(100, 180, 255);
            SpriteBatch.DrawString(_smallFont, typeIcon, new Vector2(itemRect.X + 5, itemRect.Y + 5), typeColor);
            SpriteBatch.DrawString(_font, skill.Name, new Vector2(itemRect.X + 40, itemRect.Y + 3), new Color(240, 220, 170));
            
            // 技能类型
            Color effectColor = GetEffectTypeColor(skill.EffectType);
            SpriteBatch.DrawString(_smallFont, skill.EffectType, new Vector2(itemRect.X + 5, itemRect.Y + 28), effectColor);
            
            // 冷却时间
            SpriteBatch.DrawString(_smallFont, $"CD:{skill.Cooldown}s", new Vector2(itemRect.X + 120, itemRect.Y + 28), new Color(150, 140, 120));
        }
    }

    private void DrawSkillDetail()
    {
        if (_selectedSkill == null) return;
        
        int x = 460;
        int y = 20;
        int w = GameSettings.ScreenWidth - 480;
        
        // 背景
        SpriteBatch.Draw(_pixel, new Rectangle(x, y, w, GameSettings.ScreenHeight - 40), new Color(35, 30, 25));
        UIHelper.DrawBorder(SpriteBatch, _pixel, new Rectangle(x, y, w, GameSettings.ScreenHeight - 40), new Color(80, 70, 50), 2);
        
        // 标题
        SpriteBatch.DrawString(_titleFont, _selectedSkill.Name, new Vector2(x + 15, y + 10), new Color(255, 220, 100));
        SpriteBatch.DrawString(_smallFont, $"ID: {_selectedSkill.Id}", new Vector2(x + 15, y + 50), new Color(150, 140, 120));
        
        // 属性列表
        int detailY = y + 80;
        int lineHeight = 28;
        
        DrawDetailLine(ref detailY, x + 15, "类型", _selectedSkill.Type);
        DrawDetailLine(ref detailY, x + 15, "效果类型", _selectedSkill.EffectType);
        DrawDetailLine(ref detailY, x + 15, "目标模式", _selectedSkill.TargetMode);
        DrawDetailLine(ref detailY, x + 15, "系数", _selectedSkill.Coefficient.ToString("F2"));
        DrawDetailLine(ref detailY, x + 15, "冷却时间", $"{_selectedSkill.Cooldown}s");
        DrawDetailLine(ref detailY, x + 15, "施法时间", $"{_selectedSkill.CastTime}s");
        DrawDetailLine(ref detailY, x + 15, "半径", _selectedSkill.Radius.ToString("F1"));
        DrawDetailLine(ref detailY, x + 15, "Buff统计", _selectedSkill.BuffStat);
        DrawDetailLine(ref detailY, x + 15, "Buff数值", _selectedSkill.BuffPercent.ToString("F2"));
        DrawDetailLine(ref detailY, x + 15, "Buff持续时间", $"{_selectedSkill.BuffDuration}s");
        DrawDetailLine(ref detailY, x + 15, "士气变化", _selectedSkill.MoraleChange.ToString("F1"));
        DrawDetailLine(ref detailY, x + 15, "最大等级", _selectedSkill.MaxLevel.ToString());
        DrawDetailLine(ref detailY, x + 15, "升级消耗", _selectedSkill.LevelUpCost.ToString());
        DrawDetailLine(ref detailY, x + 15, "描述", _selectedSkill.Description);
    }

    private void DrawDetailLine(ref int y, float x, string label, string value)
    {
        SpriteBatch.DrawString(_smallFont, $"{label}:", new Vector2(x, y), new Color(180, 160, 120));
        SpriteBatch.DrawString(_smallFont, value, new Vector2(x + 120, y), new Color(240, 220, 170));
        y += 28;
    }

    private void DrawNewSkillDialog()
    {
        int w = 400;
        int h = 300;
        int x = (GameSettings.ScreenWidth - w) / 2;
        int y = (GameSettings.ScreenHeight - h) / 2;
        
        // 背景
        SpriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), new Color(40, 35, 30, 240));
        UIHelper.DrawBorder(SpriteBatch, _pixel, new Rectangle(x, y, w, h), new Color(255, 220, 100), 2);
        
        // 标题
        SpriteBatch.DrawString(_titleFont, "新建技能", new Vector2(x + 15, y + 10), new Color(255, 220, 100));
        
        // 输入框
        int inputY = y + 60;
        SpriteBatch.Draw(_pixel, new Rectangle(x + 15, inputY, w - 30, 30), new Color(25, 22, 18));
        SpriteBatch.DrawString(_smallFont, _newSkillName + "|", new Vector2(x + 20, inputY + 5), new Color(240, 220, 170));
        SpriteBatch.DrawString(_smallFont, "技能名称:", new Vector2(x + 15, inputY - 18), new Color(180, 160, 120));
        
        // 类型选择
        inputY += 50;
        DrawTypeSelector(x + 15, inputY, "技能类型:", ref _newSkillType, new[] { "active", "passive" });
        
        inputY += 40;
        DrawTypeSelector(x + 15, inputY, "效果类型:", ref _newSkillEffectType, new[] { "damage", "buff", "morale", "heal" });
        
        inputY += 40;
        DrawTypeSelector(x + 15, inputY, "目标模式:", ref _newSkillTargetMode, new[] { "SingleTarget", "AOE_Circle", "Self", "AOE_Line" });
        
        // 按钮
        int btnY = y + h - 50;
        Rectangle confirmBtn = new Rectangle(x + 50, btnY, 120, 35);
        Rectangle cancelBtn = new Rectangle(x + w - 170, btnY, 120, 35);
        
        DrawButton(confirmBtn, "确认", new Color(60, 120, 60), new Color(80, 160, 80));
        DrawButton(cancelBtn, "取消", new Color(120, 60, 60), new Color(160, 80, 80));
        
        if (Input.IsMouseClicked())
        {
            Vector2 mp = Input.MousePosition;
            if (confirmBtn.Contains(mp.ToPoint()))
                ConfirmNewSkill();
            if (cancelBtn.Contains(mp.ToPoint()))
                _showNewSkillDialog = false;
        }
    }

    private void DrawTypeSelector(float x, float y, string label, ref string currentValue, string[] options)
    {
        SpriteBatch.DrawString(_smallFont, label, new Vector2(x, y), new Color(180, 160, 120));
        
        float optionX = x + 100;
        foreach (var opt in options)
        {
            bool isSelected = opt == currentValue;
            Color bgColor = isSelected ? new Color(80, 70, 40) : new Color(40, 35, 30);
            Color textColor = isSelected ? new Color(255, 220, 100) : new Color(180, 160, 120);
            
            Vector2 textSize = _smallFont.MeasureString(opt);
            Rectangle btnRect = new Rectangle((int)optionX, (int)y, (int)textSize.X + 16, 24);
            
            SpriteBatch.Draw(_pixel, btnRect, bgColor);
            UIHelper.DrawBorder(SpriteBatch, _pixel, btnRect, isSelected ? new Color(255, 220, 100) : new Color(80, 70, 50), 1);
            SpriteBatch.DrawString(_smallFont, opt, new Vector2(optionX + 8, y + 3), textColor);
            
            if (Input.IsMouseClicked() && btnRect.Contains(Input.MousePosition.ToPoint()))
            {
                currentValue = opt;
            }
            
            optionX += textSize.X + 24;
        }
    }

    private void DrawEditDialog()
    {
        // 简单的编辑提示
        int w = 300;
        int h = 100;
        int x = (GameSettings.ScreenWidth - w) / 2;
        int y = (GameSettings.ScreenHeight - h) / 2;
        
        SpriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), new Color(40, 35, 30, 240));
        UIHelper.DrawBorder(SpriteBatch, _pixel, new Rectangle(x, y, w, h), new Color(255, 220, 100), 2);
        
        SpriteBatch.DrawString(_font, "编辑功能", new Vector2(x + 15, y + 10), new Color(255, 220, 100));
        SpriteBatch.DrawString(_smallFont, "按ESC关闭", new Vector2(x + 15, y + 50), new Color(180, 160, 120));
    }

    private void DrawButton(Rectangle rect, string text, Color bgColor, Color hoverColor)
    {
        Vector2 mp = Input.MousePosition;
        Color drawColor = rect.Contains(mp.ToPoint()) ? hoverColor : bgColor;
        
        SpriteBatch.Draw(_pixel, rect, drawColor);
        UIHelper.DrawBorder(SpriteBatch, _pixel, rect, new Color(200, 180, 140), 1);
        
        Vector2 textSize = _font.MeasureString(text);
        Vector2 textPos = new Vector2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2);
        SpriteBatch.DrawString(_font, text, textPos, Color.White);
    }

    private Color GetEffectTypeColor(string effectType)
    {
        return effectType switch
        {
            "damage" => new Color(255, 100, 80),
            "buff" => new Color(100, 200, 255),
            "morale" => new Color(255, 200, 50),
            "heal" => new Color(100, 255, 100),
            _ => new Color(200, 180, 140)
        };
    }
}

// 编辑器按钮类
public class EditorButton
{
    public string Text { get; set; }
    public Rectangle Bounds { get; set; }
    public Action? OnClick { get; set; }
    
    public Color NormalColor { get; set; } = new Color(60, 50, 40);
    public Color HoverColor { get; set; } = new Color(90, 75, 55);
    public Color PressedColor { get; set; } = new Color(120, 100, 70);
    
    private bool _isHovered;
    private bool _isPressed;

    public EditorButton(string text, Rectangle bounds, Action? onClick = null)
    {
        Text = text;
        Bounds = bounds;
        OnClick = onClick;
    }

    public void Update(InputManager input)
    {
        Vector2 mp = input.MousePosition;
        _isHovered = Bounds.Contains(mp.ToPoint());
        _isPressed = _isHovered && input.IsMouseClicked();
        
        if (_isPressed)
        {
            OnClick?.Invoke();
        }
    }

    public void Draw(SpriteBatch spriteBatch, SpriteFontBase font, Texture2D pixel)
    {
        Color drawColor = _isPressed ? PressedColor : (_isHovered ? HoverColor : NormalColor);
        
        spriteBatch.Draw(pixel, Bounds, drawColor);
        UIHelper.DrawBorder(spriteBatch, pixel, Bounds, new Color(200, 180, 140), 1);
        
        Vector2 textSize = font.MeasureString(Text);
        Vector2 textPos = new Vector2(Bounds.X + (Bounds.Width - textSize.X) / 2, Bounds.Y + (Bounds.Height - textSize.Y) / 2);
        spriteBatch.DrawString(font, Text, textPos, Color.White);
    }
}
