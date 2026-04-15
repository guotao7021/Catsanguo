using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CatSanguo.Core;

public abstract class Scene
{
    protected CatSanguoGame Game { get; private set; }
    protected SpriteBatch SpriteBatch => Game.SpriteBatch;
    protected InputManager Input => Game.Input;
    protected ContentManager Content => Game.Content;
    protected GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

    public void Initialize(CatSanguoGame game)
    {
        Game = game;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void Update(GameTime gameTime);
    public abstract void Draw(GameTime gameTime);
}
