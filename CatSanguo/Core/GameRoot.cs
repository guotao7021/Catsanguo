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

    // 全局 Manager（新）
    public TurnManager TurnManager { get; private set; } = null!;
    public ScenarioManager ScenarioManager { get; private set; } = null!;
    public DiplomacyManager DiplomacyManager { get; private set; } = null!;
    public CaptureManager CaptureManager { get; private set; } = null!;
    public SpecialSkillManager SpecialSkillManager { get; private set; } = null!;
    public GeneralAppearanceManager AppearanceManager { get; private set; } = null!;
    public EventBus EventBus { get; private set; } = null!;

    public static GameRoot Create()
    {
        _instance = new GameRoot();
        return _instance;
    }

    // Demo scene helpers
    private string? _demoCityId;
    public PendingMarchData? PendingMarch { get; set; }

    public string? GetDemoCityId() => _demoCityId;
    public void SetDemoCityId(string cityId) => _demoCityId = cityId;

    public void Initialize(CatSanguoGame game)
    {
        Game = game;

        // 初始化数据管理器
        Data = DataManager.Create();
        Data.LoadAll();

        // 初始化事件总线
        EventBus = new EventBus();

        // 初始化全局 Manager
        TurnManager = new TurnManager(EventBus);
        ScenarioManager = new ScenarioManager();
        DiplomacyManager = new DiplomacyManager(EventBus);
        CaptureManager = new CaptureManager(EventBus);
        SpecialSkillManager = new SpecialSkillManager(EventBus);
        AppearanceManager = new GeneralAppearanceManager(Data.AllGenerals);

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
