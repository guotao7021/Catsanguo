using System;
using Microsoft.Xna.Framework;
using CatSanguo.Data;

namespace CatSanguo.Core;

public enum GamePhase
{
    Login,
    City,
    Battle,
    Result
}

public class GameRoot
{
    private static GameRoot? _instance;
    public static GameRoot Instance => _instance ?? throw new InvalidOperationException("GameRoot not initialized");

    public CatSanguoGame Game { get; private set; } = null!;
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Login;
    public DataManager Data { get; private set; } = null!;
    public SystemManager Systems { get; private set; } = null!;

    public static GameRoot Create()
    {
        _instance = new GameRoot();
        return _instance;
    }

    public void Initialize(CatSanguoGame game)
    {
        Game = game;

        // 初始化数据管理器
        Data = DataManager.Create();
        Data.LoadAll();

        // 初始化 GameState
        GameState.Instance.Initialize(Data.AllGenerals);

        // 初始化系统管理器
        Systems = new SystemManager();
        Systems.InitializeAll();
    }

    public void TransitionTo(GamePhase phase)
    {
        CurrentPhase = phase;
    }

    public void Update(GameTime gameTime)
    {
        Systems.UpdateAll(gameTime, Game.Input);
    }
}
