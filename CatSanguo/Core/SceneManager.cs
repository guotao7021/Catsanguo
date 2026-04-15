using Microsoft.Xna.Framework;

namespace CatSanguo.Core;

public class SceneManager
{
    private readonly CatSanguoGame _game;
    private Scene _currentScene;
    private Scene _nextScene;
    private float _fadeAlpha;
    private bool _isFading;
    private bool _fadeOut;
    private const float FadeSpeed = 4f;

    public Scene CurrentScene => _currentScene;

    public SceneManager(CatSanguoGame game)
    {
        _game = game;
    }

    public void ChangeScene(Scene scene)
    {
        _nextScene = scene;
        _nextScene.Initialize(_game);
        if (_currentScene != null)
        {
            _isFading = true;
            _fadeOut = true;
            _fadeAlpha = 0f;
        }
        else
        {
            _currentScene = _nextScene;
            _nextScene = null;
            _currentScene.Enter();
        }
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_isFading)
        {
            if (_fadeOut)
            {
                _fadeAlpha += FadeSpeed * dt;
                if (_fadeAlpha >= 1f)
                {
                    _fadeAlpha = 1f;
                    _fadeOut = false;
                    _currentScene?.Exit();
                    _currentScene = _nextScene;
                    _nextScene = null;
                    _currentScene.Enter();
                }
            }
            else
            {
                _fadeAlpha -= FadeSpeed * dt;
                if (_fadeAlpha <= 0f)
                {
                    _fadeAlpha = 0f;
                    _isFading = false;
                }
            }
        }

        _currentScene?.Update(gameTime);
    }

    public void Draw(GameTime gameTime)
    {
        _currentScene?.Draw(gameTime);
    }

    public float FadeAlpha => _isFading ? _fadeAlpha : 0f;
    public bool IsFading => _isFading;
}
