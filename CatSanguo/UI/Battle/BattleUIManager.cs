using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Battle;
using CatSanguo.Data.Schemas;

namespace CatSanguo.UI.Battle;

/// <summary>
/// 战斗UI模式：自动战斗 vs 手动战斗
/// </summary>
public enum BattleUIMode
{
    Auto,   // 自动战斗：简化UI，无技能/阵型操作
    Manual  // 手动战斗：完整UI，含技能/阵型操作
}

/// <summary>
/// 战斗UI总管理器，组装并协调所有子模块
/// </summary>
public class BattleUIManager
{
    // 子模块
    public BattleHUD HUD { get; private set; } = new();
    public UnitOverheadUI UnitUI { get; private set; } = new();
    public SkillPanel SkillPanel { get; private set; } = new();
    public FormationPanel FormationPanel { get; private set; } = new();
    public BattleInfoDisplay InfoDisplay { get; private set; } = new();
    public BattleResultPanel ResultPanel { get; private set; } = new();

    // 模式
    public BattleUIMode Mode { get; private set; }

    // 战斗数据引用
    private List<Squad> _playerSquads = new();
    private List<Squad> _enemySquads = new();
    private BuffSystem? _buffSystem;

    // 资源
    private Texture2D _pixel = null!;
    private SpriteFontBase _font = null!;
    private SpriteFontBase _titleFont = null!;
    private SpriteFontBase _smallFont = null!;

    public BattleUIManager(BattleUIMode mode)
    {
        Mode = mode;
    }

    public void Initialize(Texture2D pixel, SpriteFontBase font, SpriteFontBase titleFont, SpriteFontBase smallFont)
    {
        _pixel = pixel;
        _font = font;
        _titleFont = titleFont;
        _smallFont = smallFont;

        // HUD
        HUD.ShowAutoButton = Mode == BattleUIMode.Manual;
        HUD.ShowSkipButton = Mode == BattleUIMode.Auto;
        HUD.SpeedOptions = Mode == BattleUIMode.Auto ? new[] { 2f, 4f, 8f } : new[] { 1f, 2f };
        HUD.Initialize(pixel, font, smallFont);

        // Unit overhead UI
        UnitUI.DetailMode = Mode == BattleUIMode.Manual;
        UnitUI.Initialize(pixel, font, smallFont);

        // Info display
        InfoDisplay.Initialize(pixel, font, titleFont);

        // Result panel
        ResultPanel.Initialize(pixel, font, titleFont, smallFont);

        // Manual-only modules
        if (Mode == BattleUIMode.Manual)
        {
            SkillPanel.Initialize(pixel, font, smallFont);
            FormationPanel.Initialize(pixel, font, smallFont);
        }
    }

    public void SetSquadLists(List<Squad> playerSquads, List<Squad> enemySquads)
    {
        _playerSquads = playerSquads;
        _enemySquads = enemySquads;
    }

    public void SetBuffSystem(BuffSystem? buffSystem)
    {
        _buffSystem = buffSystem;
    }

    public void Update(float deltaTime, InputManager input, float battleTime, float speed, bool paused)
    {
        // HUD数据更新
        HUD.UpdateData(_playerSquads, _enemySquads, battleTime, speed, paused);
        HUD.Update(input);

        // Unit overhead
        UnitUI.Update(deltaTime);

        // Info display
        InfoDisplay.Update(deltaTime);

        // Result panel
        ResultPanel.Update(deltaTime, input);

        // Manual mode modules
        if (Mode == BattleUIMode.Manual)
        {
            SkillPanel.Update(input, _playerSquads);
            FormationPanel.Update(deltaTime, input);
        }
    }

    public void Draw(SpriteBatch sb, Vector2 screenOffset)
    {
        // 1. HUD (顶部)
        HUD.Draw(sb);

        // 2. Unit overhead (场景空间)
        UnitUI.DrawAll(sb, _playerSquads, _buffSystem, screenOffset);
        UnitUI.DrawAll(sb, _enemySquads, _buffSystem, screenOffset);

        // 3. Info display (上方中间)
        InfoDisplay.Draw(sb);

        // 4. Manual mode: Skill panel + Formation panel (底部)
        if (Mode == BattleUIMode.Manual)
        {
            SkillPanel.Draw(sb, _playerSquads, _enemySquads, 0);
            FormationPanel.Draw(sb);
        }

        // 5. Result panel (覆盖层)
        ResultPanel.Draw(sb);
    }

    /// <summary>显示结算界面</summary>
    public void ShowResult(BattleResultData data)
    {
        ResultPanel.Show(data);
    }

    /// <summary>显示战术提示</summary>
    public void ShowTacticalTip()
    {
        InfoDisplay.GenerateTacticalTip(_enemySquads);
    }

    /// <summary>添加战斗信息提示</summary>
    public void AddBattleInfo(string text, BattleInfoType type, float duration = 1.5f)
    {
        InfoDisplay.AddInfo(text, type, duration);
    }
}
