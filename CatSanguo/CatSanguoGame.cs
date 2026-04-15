using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using CatSanguo.Core;
using CatSanguo.Core.Animation;
using CatSanguo.Scenes;

namespace CatSanguo;

public class CatSanguoGame : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private FontSystem _fontSystem;

    public SpriteBatch SpriteBatch => _spriteBatch;
    public SceneManager SceneManager { get; private set; }
    public InputManager Input { get; private set; }
    public Texture2D Pixel { get; private set; }
    public SpriteFontBase SmallFont { get; private set; }
    public SpriteFontBase Font { get; private set; }
    public SpriteFontBase NotifyFont { get; private set; }
    public SpriteFontBase TitleFont { get; private set; }
    public SpriteSheetManager SpriteSheets { get; private set; }

    public CatSanguoGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = GameSettings.GameTitle;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnClientSizeChanged;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = GameSettings.ScreenWidth;
        _graphics.PreferredBackBufferHeight = GameSettings.ScreenHeight;
        _graphics.ApplyChanges();

        Input = new InputManager();
        SceneManager = new SceneManager(this);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create 1x1 white pixel texture
        Pixel = new Texture2D(GraphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });

        // Load fonts via FontStashSharp
        string fontPath = Path.Combine(Content.RootDirectory, "simhei.ttf");
        if (!File.Exists(fontPath))
            fontPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Content", "simhei.ttf");

        byte[] fontData = File.ReadAllBytes(fontPath);
        _fontSystem = new FontSystem();
        _fontSystem.AddFont(fontData);

        SmallFont = _fontSystem.GetFont(14);
        Font = _fontSystem.GetFont(20);
        NotifyFont = _fontSystem.GetFont(32);
        TitleFont = _fontSystem.GetFont(42);

        // Load sprite sheets
        SpriteSheets = new SpriteSheetManager();
        string spritesPath = Path.Combine(Content.RootDirectory, "Sprites");
        if (!Directory.Exists(spritesPath))
            spritesPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Content", "Sprites");
        SpriteSheets.LoadAll(GraphicsDevice, spritesPath);

        // Initialize GameRoot (loads all data, initializes systems)
        GameRoot.Create().Initialize(this);

        // Start with main menu
        SceneManager.ChangeScene(new MainMenuScene());
    }

    protected override void Update(GameTime gameTime)
    {
        Input.Update();

        // Update GameRoot systems
        GameRoot.Instance.Update(gameTime);

        if (Input.IsKeyPressed(Keys.Escape))
        {
            if (SceneManager.CurrentScene is not MainMenuScene)
            {
                SceneManager.ChangeScene(new MainMenuScene());
            }
        }

        SceneManager.Update(gameTime);
        base.Update(gameTime);
    }

    private void OnClientSizeChanged(object? sender, System.EventArgs e)
    {
        if (Window.ClientBounds.Width > 0 && Window.ClientBounds.Height > 0)
        {
            GameSettings.ScreenWidth = Window.ClientBounds.Width;
            GameSettings.ScreenHeight = Window.ClientBounds.Height;
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        SceneManager.Draw(gameTime);

        // Draw fade overlay
        if (SceneManager.IsFading)
        {
            _spriteBatch.Begin();
            _spriteBatch.Draw(Pixel,
                new Rectangle(0, 0, GameSettings.ScreenWidth, GameSettings.ScreenHeight),
                Color.Black * SceneManager.FadeAlpha);
            _spriteBatch.End();
        }

        // Draw debug overlay (on top of everything)
        GameRoot.Instance.Systems.Debug.Draw(_spriteBatch, SmallFont, Pixel);

        base.Draw(gameTime);
    }
}
