using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CatSanguo.Core;

namespace CatSanguo.WorldMap;

public class MapBackgroundRenderer
{
    private RenderTarget2D? _cached;
    private bool _dirty = true;
    private Texture2D? _backgroundImage;
    private bool _imageLoadAttempted;

    private const int WorldWidth = 2000;
    private const int WorldHeight = 1400;

    public void Invalidate() => _dirty = true;

    public void LoadBackground(GraphicsDevice gd)
    {
        if (_imageLoadAttempted) return;
        _imageLoadAttempted = true;

        string bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "Sprites", "map_background.png");
        if (File.Exists(bgPath))
        {
            using var stream = File.OpenRead(bgPath);
            _backgroundImage = Texture2D.FromStream(gd, stream);
        }
    }

    public void EnsureCache(GraphicsDevice gd, SpriteBatch sb, Texture2D pixel, List<CityNode> cities)
    {
        if (!_dirty && _cached != null) return;

        LoadBackground(gd);

        int w = WorldWidth;
        int h = WorldHeight;

        if (_cached == null || _cached.Width != w || _cached.Height != h)
        {
            _cached?.Dispose();
            _cached = new RenderTarget2D(gd, w, h);
        }

        gd.SetRenderTarget(_cached);
        gd.Clear(Color.Transparent);
        sb.Begin();

        if (_backgroundImage != null)
        {
            sb.Draw(_backgroundImage, new Rectangle(0, 0, w, h), Color.White);
        }
        else
        {
            sb.Draw(pixel, new Rectangle(0, 0, w, h), new Color(45, 80, 45));
        }

        // Subtle edge vignette
        DrawVignette(sb, pixel, w, h);

        sb.End();
        gd.SetRenderTarget(null);
        _dirty = false;
    }

    private void DrawVignette(SpriteBatch sb, Texture2D pixel, int w, int h)
    {
        int vignetteSize = 80;
        for (int i = 0; i < vignetteSize; i++)
        {
            float t = (float)i / vignetteSize;
            float alpha = (1f - t) * 0.3f;
            Color vc = new Color(20, 15, 10, alpha);
            sb.Draw(pixel, new Rectangle(i, 0, 1, h), vc);
            sb.Draw(pixel, new Rectangle(w - 1 - i, 0, 1, h), vc);
            sb.Draw(pixel, new Rectangle(0, i, w, 1), vc);
            sb.Draw(pixel, new Rectangle(0, h - 1 - i, w, 1), vc);
        }
    }

    public void Draw(SpriteBatch sb)
    {
        if (_cached != null)
        {
            sb.Draw(_cached, Vector2.Zero, Color.White);
        }
    }
}
