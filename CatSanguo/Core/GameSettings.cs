namespace CatSanguo.Core;

public static class GameSettings
{
    public static int ScreenWidth = 1280;
    public static int ScreenHeight = 720;
    public const string GameTitle = "猫三国";

    // Time settings
    public static int DaysPerTurn = 10;
    public static float WorldMapTimeScale = 1.0f;

    // Battle settings
    public const float MeleeRange = 40f;
    public const float RangedRange = 200f;
    public const float SquadSeparationDistance = 45f;
    public const float SeparationForce = 50f;

    // Morale thresholds
    public const float MoraleNormal = 70f;
    public const float MoraleLow = 40f;
    public const float MoraleCritical = 20f;

    // AI settings
    public const float AIThinkInterval = 1.0f;

    // Sango Field Battle (三国群英传2风格战斗)
    public const int SangoBattlefieldWidth = 2560;
    public const int SangoBattlefieldHeight = 720;
    public const float SangoCollisionRadius = 16f;
    public const float SangoAttackInterval = 1.5f;
    public const float SangoRangedRange = 200f;
    public const int SangoSoldiersPerGeneral = 20;
    public const float SangoTopHUDHeight = 50f;
    public const float SangoBottomBarHeight = 120f;
    public const float SangoExecutionDuration = 5.0f;
}
