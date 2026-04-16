using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.UI;
using CatSanguo.Data;

namespace CatSanguo.Scenes;

/// <summary>
/// 经典三国志风格主菜单
/// 2x2 按钮布局：开始游戏/继续游戏/设定选项/游戏结束
/// </summary>
public class MainMenuScene : Scene
{
    private Button _startButton;
    private Button _continueButton;
    private Button _settingsButton;
    private Button _exitButton;
    private Button _testBattleButton; // 临时：测试三国群英传战斗
    private Texture2D _pixel;
    private SpriteFontBase _font;
    private SpriteFontBase _titleFont;
    private SpriteFontBase _smallFont;
    private float _cloudOffset;
    private float _titleGlow;

    // 存档选择面板
    private SaveLoadPanel _saveLoadPanel = new();
    private string _tipText = "";
    private float _tipTimer = 0f;

    public override void Enter()
    {
        _pixel = Game.Pixel;
        _font = Game.Font;
        _titleFont = Game.TitleFont;
        _smallFont = Game.SmallFont;
        _cloudOffset = 0f;
        _titleGlow = 0f;

        int btnW = 200, btnH = 48;
        int startX = GameSettings.ScreenWidth / 2 - btnW - 30;
        int startY = 320;
        int gapX = 60;
        int gapY = 15;

        // 2x2 布局
        _startButton = new Button("开 始 游 戏", new Rectangle(startX, startY, btnW, btnH));
        _startButton.OnClick = () => Game.SceneManager.ChangeScene(new ScenarioSelectScene());

        _continueButton = new Button("继 续 游 戏", new Rectangle(startX + btnW + gapX, startY, btnW, btnH));
        _continueButton.OnClick = () => ContinueGame();

        _settingsButton = new Button("设 定 选 项", new Rectangle(startX, startY + btnH + gapY, btnW, btnH));
        _settingsButton.OnClick = () => OpenSettings();

        _exitButton = new Button("游 戏 结 束", new Rectangle(startX + btnW + gapX, startY + btnH + gapY, btnW, btnH));
        _exitButton.OnClick = () => Game.Exit();

        // 临时测试按钮
        _testBattleButton = new Button("测试战斗", new Rectangle(startX, startY + 2 * (btnH + gapY), btnW * 2 + gapX, btnH));
        _testBattleButton.OnClick = () => Game.SceneManager.ChangeScene(new SangoFieldBattleScene());
    }

    private void ContinueGame()
    {
        // 检查是否有任何存档
        var slots = GameState.Instance.GetSaveSlotInfos();
        bool hasAnySave = slots.Exists(s => !s.IsEmpty);
        if (!hasAnySave)
        {
            _tipText = "没有找到存档";
            _tipTimer = 2f;
            return;
        }

        // 打开存档面板（加载模式）
        _saveLoadPanel.OnOperationComplete = (slot, isLoad) =>
        {
            if (isLoad)
            {
                Game.SceneManager.ChangeScene(new WorldMapScene());
            }
        };
        _saveLoadPanel.Open(SaveLoadMode.Load);
    }

    private void OpenSettings()
    {
        // TODO: 打开设置面板
        System.Diagnostics.Debug.WriteLine("[MainMenu] 设定选项 - 待实现");
    }

    public override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _cloudOffset += dt * 15f;
        _titleGlow += dt;
        _tipTimer -= dt;

        // 存档面板激活时优先处理
        if (_saveLoadPanel.IsActive)
        {
            _saveLoadPanel.Update(Input, dt);
            return;
        }

        _startButton.Update(Input);
        _continueButton.Update(Input);
        _settingsButton.Update(Input);
        _exitButton.Update(Input);
        _testBattleButton.Update(Input);
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(135, 180, 220));

        SpriteBatch.Begin();

        // 天空背景（云朵渐变）
        DrawSkyBackground();

        // 底部装饰（草地/地面）
        DrawGroundDecoration();

        // 标题
        DrawTitle();

        // 按钮
        _startButton.Draw(SpriteBatch, _font, _pixel);
        _continueButton.Draw(SpriteBatch, _font, _pixel);
        _settingsButton.Draw(SpriteBatch, _font, _pixel);
        _exitButton.Draw(SpriteBatch, _font, _pixel);
        _testBattleButton.Draw(SpriteBatch, _font, _pixel);

        // 版本号
        SpriteBatch.DrawString(_font, "v0.3",
            new Vector2(GameSettings.ScreenWidth - 70, GameSettings.ScreenHeight - 25),
            new Color(80, 100, 120));

        // 提示文字
        if (_tipTimer > 0 && !string.IsNullOrEmpty(_tipText))
        {
            float alpha = Math.Min(1f, _tipTimer);
            var tipSize = _font.MeasureString(_tipText);
            SpriteBatch.DrawString(_font, _tipText,
                new Vector2((GameSettings.ScreenWidth - tipSize.X) / 2, 420),
                new Color(220, 100, 80) * alpha);
        }

        // 存档面板（最顶层）
        if (_saveLoadPanel.IsActive)
        {
            _saveLoadPanel.Draw(SpriteBatch, _pixel, _font, _smallFont);
        }

        SpriteBatch.End();
    }

    private void DrawSkyBackground()
    {
        // 天空渐变
        for (int y = 0; y < GameSettings.ScreenHeight - 80; y += 4)
        {
            float t = (float)y / (GameSettings.ScreenHeight - 80);
            byte r = (byte)MathHelper.Lerp(135, 180, t);
            byte g = (byte)MathHelper.Lerp(180, 210, t);
            byte b = (byte)MathHelper.Lerp(220, 235, t);
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, GameSettings.ScreenWidth, 4), new Color(r, g, b));
        }

        // 云朵效果（半透明圆形模拟）
        DrawCloud(200 + _cloudOffset % (GameSettings.ScreenWidth + 200) - 100, 100, 80, 0.3f);
        DrawCloud(500 + (_cloudOffset * 0.7f) % (GameSettings.ScreenWidth + 200) - 100, 180, 60, 0.25f);
        DrawCloud(800 + (_cloudOffset * 0.5f) % (GameSettings.ScreenWidth + 200) - 100, 80, 70, 0.35f);
        DrawCloud(1100 + (_cloudOffset * 0.8f) % (GameSettings.ScreenWidth + 200) - 100, 150, 50, 0.2f);
    }

    private void DrawCloud(float x, float y, float size, float alpha)
    {
        // 简化云朵绘制（使用多个半透明矩形模拟）
        for (int i = 0; i < 5; i++)
        {
            float offsetX = i * size * 0.4f;
            float offsetY = MathF.Sin(i * 0.8f) * size * 0.2f;
            SpriteBatch.Draw(_pixel,
                new Rectangle((int)(x + offsetX), (int)(y + offsetY), (int)(size * 0.6f), (int)(size * 0.3f)),
                Color.White * alpha);
        }
    }

    private void DrawGroundDecoration()
    {
        int groundY = GameSettings.ScreenHeight - 80;

        // 地面
        for (int y = groundY; y < GameSettings.ScreenHeight; y++)
        {
            float t = (float)(y - groundY) / 80f;
            byte r = (byte)MathHelper.Lerp(100, 60, t);
            byte g = (byte)MathHelper.Lerp(130, 90, t);
            byte b = (byte)MathHelper.Lerp(60, 40, t);
            SpriteBatch.Draw(_pixel, new Rectangle(0, y, GameSettings.ScreenWidth, 1), new Color(r, g, b));
        }

        // 草装饰
        SpriteBatch.Draw(_pixel, new Rectangle(0, groundY, GameSettings.ScreenWidth, 3), new Color(80, 120, 50));
    }

    private void DrawTitle()
    {
        string title = "猫 三 国";

        // 发光效果
        float glow = MathF.Sin(_titleGlow * 2f) * 0.1f + 0.9f;

        Vector2 titleSize = _titleFont.MeasureString(title);
        Vector2 titlePos = new Vector2(
            GameSettings.ScreenWidth / 2 - titleSize.X / 2,
            80);

        // 标题阴影
        SpriteBatch.DrawString(_titleFont, title,
            titlePos + new Vector2(4, 4),
            new Color(60, 80, 100) * 0.5f);

        // 标题主体（蓝白色）
        SpriteBatch.DrawString(_titleFont, title,
            titlePos,
            new Color(240, 250, 255) * glow);

        // 副标题
        string subtitle = "策 略 · 战 斗 · 猫 咪 三 国";
        Vector2 subSize = _font.MeasureString(subtitle);
        SpriteBatch.DrawString(_font, subtitle,
            new Vector2(GameSettings.ScreenWidth / 2 - subSize.X / 2, 170),
            new Color(100, 130, 160));
    }
}
