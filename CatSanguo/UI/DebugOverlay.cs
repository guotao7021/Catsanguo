using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Data;

namespace CatSanguo.UI;

public class DebugOverlay
{
    public bool IsVisible { get; set; } = false;

    private readonly float[] _fpsHistory = new float[60];
    private int _fpsIndex;
    private int _battleUnitCount;

    public void SetBattleUnitCount(int count) => _battleUnitCount = count;

    public void Update(GameTime gameTime, InputManager input)
    {
        if (input.IsKeyPressed(Keys.F1))
            IsVisible = !IsVisible;

        float fps = 1f / (float)gameTime.ElapsedGameTime.TotalSeconds;
        _fpsHistory[_fpsIndex] = fps;
        _fpsIndex = (_fpsIndex + 1) % _fpsHistory.Length;
    }

    public void Draw(SpriteBatch sb, SpriteFontBase font, Texture2D pixel)
    {
        if (!IsVisible) return;

        sb.Begin();

        // 半透明面板
        int panelW = 220, panelH = 120;
        sb.Draw(pixel, new Rectangle(5, 5, panelW, panelH), new Color(0, 0, 0, 180));

        // 边框
        sb.Draw(pixel, new Rectangle(5, 5, panelW, 1), new Color(80, 65, 45));
        sb.Draw(pixel, new Rectangle(5, 5 + panelH, panelW, 1), new Color(80, 65, 45));
        sb.Draw(pixel, new Rectangle(5, 5, 1, panelH), new Color(80, 65, 45));
        sb.Draw(pixel, new Rectangle(5 + panelW, 5, 1, panelH), new Color(80, 65, 45));

        int x = 12, y = 10;
        Color textColor = new Color(200, 200, 200);
        Color labelColor = new Color(160, 140, 100);

        // FPS
        float avgFps = 0;
        for (int i = 0; i < _fpsHistory.Length; i++) avgFps += _fpsHistory[i];
        avgFps /= _fpsHistory.Length;
        Color fpsColor = avgFps >= 55 ? new Color(80, 200, 80) : avgFps >= 30 ? new Color(200, 200, 80) : new Color(200, 80, 80);
        sb.DrawString(font, $"FPS: {(int)avgFps}", new Vector2(x, y), fpsColor);

        // GamePhase
        y += 20;
        string phase = "N/A";
        try { phase = GameRoot.Instance.CurrentPhase.ToString(); } catch { }
        sb.DrawString(font, $"Phase: {phase}", new Vector2(x, y), labelColor);

        // Scene
        y += 20;
        string sceneName = "N/A";
        try
        {
            var scene = GameRoot.Instance.Game.SceneManager.CurrentScene;
            if (scene != null) sceneName = scene.GetType().Name;
        }
        catch { }
        sb.DrawString(font, $"Scene: {sceneName}", new Vector2(x, y), textColor);

        // Units
        y += 20;
        sb.DrawString(font, $"Units: {_battleUnitCount}", new Vector2(x, y), textColor);

        // Resources
        y += 20;
        try
        {
            var cityId = GameState.Instance.OwnedCityIds.Count > 0 ? GameState.Instance.OwnedCityIds[0] : "";
            if (!string.IsNullOrEmpty(cityId))
            {
                var cp = GameState.Instance.GetCityProgress(cityId);
                if (cp != null)
                {
                    sb.DrawString(font, $"G:{cp.Gold} F:{cp.Food} W:{cp.Wood} I:{cp.Iron}", new Vector2(x, y), labelColor);
                }
            }
        }
        catch { }

        sb.End();
    }
}
