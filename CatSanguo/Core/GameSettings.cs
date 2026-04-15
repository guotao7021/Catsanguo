namespace CatSanguo.Core;

public static class GameSettings
{
    public static int ScreenWidth = 1280;
    public static int ScreenHeight = 720;
    public const string GameTitle = "猫三国";

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
}
