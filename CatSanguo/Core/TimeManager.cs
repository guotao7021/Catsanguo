using System;

namespace CatSanguo.Core;

/// <summary>
/// 时间管理器，统一处理游戏内时间流逝、时间缩放和现实时间转换。
/// 所有时间单位统一使用秒。
/// </summary>
public class TimeManager
{
    /// <summary>游戏内时间流逝（秒）</summary>
    public float GameTimeSeconds { get; private set; }

    /// <summary>现实时间流逝（秒）</summary>
    public float RealTimeSeconds { get; private set; }

    /// <summary>当前时间缩放倍率</summary>
    public float TimeScale { get; private set; } = 1.0f;

    /// <summary>是否暂停</summary>
    public bool IsPaused { get; private set; }

    /// <summary>上一帧的现实时间（秒）</summary>
    private float _lastRealTime;

    /// <summary>累计的未处理时间（用于精确推进）</summary>
    private float _accumulatedDelta;

    /// <summary>
    /// 创建时间管理器
    /// </summary>
    public TimeManager()
    {
        GameTimeSeconds = 0f;
        RealTimeSeconds = 0f;
        TimeScale = GameSettings.WorldMapTimeScale;
        IsPaused = false;
        _lastRealTime = 0f;
        _accumulatedDelta = 0f;
    }

    /// <summary>
    /// 更新时间（每帧调用）
    /// </summary>
    /// <param name="deltaTime">帧间隔（秒）</param>
    public void Update(float deltaTime)
    {
        if (IsPaused)
        {
            _lastRealTime += deltaTime;
            return;
        }

        RealTimeSeconds += deltaTime;
        float scaledDelta = deltaTime * TimeScale;
        GameTimeSeconds += scaledDelta;
        _accumulatedDelta += scaledDelta;

        _lastRealTime += deltaTime;
    }

    /// <summary>
    /// 设置时间缩放
    /// </summary>
    /// <param name="scale">时间倍率（0.5=半速，1=正常，2=双倍，4=四倍）</param>
    public void SetTimeScale(float scale)
    {
        TimeScale = Math.Max(0.1f, Math.Min(10f, scale));
    }

    /// <summary>
    /// 切换时间缩放（循环：1x -> 2x -> 4x -> 1x）
    /// </summary>
    /// <returns>新的时间倍率</returns>
    public float ToggleTimeScale()
    {
        TimeScale = TimeScale switch
        {
            1f => 2f,
            2f => 4f,
            _ => 1f
        };
        return TimeScale;
    }

    /// <summary>
    /// 暂停/恢复
    /// </summary>
    public void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    /// <summary>
    /// 设置暂停状态
    /// </summary>
    public void SetPaused(bool paused)
    {
        IsPaused = paused;
    }

    /// <summary>
    /// 获取经过的游戏时间（秒），并清空累计
    /// </summary>
    public float ConsumeGameTimeDelta()
    {
        float delta = _accumulatedDelta;
        _accumulatedDelta = 0f;
        return delta;
    }

    /// <summary>
    /// 将游戏时间转换为显示格式（时:分）
    /// 注意：游戏内时间与现实时间1:1对应，仅用于显示
    /// </summary>
    public string GetTimeDisplayString()
    {
        int totalMinutes = (int)(GameTimeSeconds / 60);
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        return $"{hours:D2}:{minutes:D2}";
    }

    /// <summary>
    /// 重置时间
    /// </summary>
    public void Reset()
    {
        GameTimeSeconds = 0f;
        RealTimeSeconds = 0f;
        _accumulatedDelta = 0f;
        _lastRealTime = 0f;
        TimeScale = GameSettings.WorldMapTimeScale;
        IsPaused = false;
    }
}
