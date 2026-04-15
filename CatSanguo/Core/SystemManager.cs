using Microsoft.Xna.Framework;
using CatSanguo.Systems;
using CatSanguo.UI;

namespace CatSanguo.Core;

public class SystemManager
{
    public CitySystem City { get; private set; } = new();
    public TeamBuilder Team { get; private set; } = new();
    public RewardSystem Rewards { get; private set; } = new();
    public DebugOverlay Debug { get; private set; } = new();

    public void InitializeAll()
    {
        // Systems are already constructed via property initializers
        // Additional initialization can go here if needed
    }

    public void UpdateAll(GameTime gameTime, InputManager input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        City.Update(dt);
        Debug.Update(gameTime, input);
    }
}
