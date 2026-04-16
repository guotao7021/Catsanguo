using System;
using System.Linq;
using CatSanguo.Data;
using CatSanguo.Data.Schemas;
using CatSanguo.Generals;

namespace CatSanguo.Core;

/// <summary>
/// 俘虏管理器
/// 处理战斗中的撤退和俘获逻辑（替代原阵亡机制）
/// </summary>
public class CaptureManager
{
    private readonly EventBus _eventBus;
    private readonly Random _random = new();

    public CaptureManager(EventBus eventBus)
    {
        _eventBus = eventBus;

        // 订阅撤退和俘获事件
        _eventBus.Subscribe(GameEventType.OnRetreat, OnGeneralRetreat);
        _eventBus.Subscribe(GameEventType.OnCapture, OnGeneralCapture);
    }

    /// <summary>
    /// 计算撤退概率
    /// 撤退概率 = 基础概率(30%) + (速度差 * 2%) + (武力差 * 1%)
    /// </summary>
    public float CalculateRetreatChance(General retreatingGeneral, General pursuingGeneral)
    {
        float baseChance = 0.3f; // 30% 基础撤退概率

        // 速度差加成
        int speedDiff = retreatingGeneral.Speed - pursuingGeneral.Speed;
        float speedBonus = speedDiff * 0.02f;

        // 武力差加成
        int strDiff = retreatingGeneral.Strength - pursuingGeneral.Strength;
        float strBonus = strDiff * 0.01f;

        float totalChance = baseChance + speedBonus + strBonus;
        return Math.Clamp(totalChance, 0.1f, 0.8f); // 10% ~ 80%
    }

    /// <summary>
    /// 计算俘获概率（当武将未能撤退时）
    /// 俘获概率 = 基础概率(20%) + (追击方智力 - 逃跑方智力) * 1%
    /// </summary>
    public float CalculateCaptureChance(General capturedGeneral, General capturingGeneral)
    {
        float baseChance = 0.2f; // 20% 基础俘获概率

        // 智力差加成
        int intDiff = capturingGeneral.Intelligence - capturedGeneral.Intelligence;
        float intBonus = intDiff * 0.01f;

        float totalChance = baseChance + intBonus;
        return Math.Clamp(totalChance, 0.1f, 0.6f); // 10% ~ 60%
    }

    /// <summary>
    /// 尝试让武将撤退
    /// 返回 true 表示撤退成功
    /// </summary>
    public bool TryRetreat(General retreatingGeneral, General pursuingGeneral)
    {
        float chance = CalculateRetreatChance(retreatingGeneral, pursuingGeneral);
        float roll = (float)_random.NextDouble();

        bool success = roll < chance;

        if (success)
        {
            _eventBus.Publish(new GameEvent(GameEventType.OnRetreat,
                tag: retreatingGeneral.Id));
        }

        return success;
    }

    /// <summary>
    /// 尝试俘获武将
    /// 返回 true 表示俘获成功
    /// </summary>
    public bool TryCapture(General capturedGeneral, General capturingGeneral)
    {
        float chance = CalculateCaptureChance(capturedGeneral, capturingGeneral);
        float roll = (float)_random.NextDouble();

        bool success = roll < chance;

        if (success)
        {
            _eventBus.Publish(new GameEvent(GameEventType.OnCapture,
                tag: capturedGeneral.Id));

            // 添加到俘虏列表
            var gs = GameState.Instance;
            if (!gs.CaptiveGeneralIds.Contains(capturedGeneral.Id))
            {
                gs.CaptiveGeneralIds.Add(capturedGeneral.Id);
            }

            // 更新武将状态
            var progress = gs.GetGeneralProgress(capturedGeneral.Id);
            if (progress != null)
            {
                progress.Status = GeneralStatus.Captive;
            }

            gs.Save();
        }

        return success;
    }

    /// <summary>
    /// 处理撤退事件
    /// </summary>
    private void OnGeneralRetreat(GameEvent evt)
    {
        string generalId = evt.Tag;
        System.Diagnostics.Debug.WriteLine($"[CaptureManager] General {generalId} retreated from battle");

        var gs = GameState.Instance;
        var progress = gs.GetGeneralProgress(generalId);
        if (progress != null)
        {
            // 撤退的武将返回所属城池
            if (!string.IsNullOrEmpty(progress.CurrentCityId))
            {
                // 武将已回到城池
                progress.IsOnExpedition = false;
            }
            gs.Save();
        }
    }

    /// <summary>
    /// 处理俘获事件
    /// </summary>
    private void OnGeneralCapture(GameEvent evt)
    {
        string generalId = evt.Tag;
        System.Diagnostics.Debug.WriteLine($"[CaptureManager] General {generalId} was captured");
    }

    /// <summary>
    /// 招降俘虏
    /// </summary>
    public bool RecruitCaptive(string generalId, out string errorMsg)
    {
        return GameState.Instance.RecruitCaptive(generalId, out errorMsg);
    }

    /// <summary>
    /// 释放俘虏
    /// </summary>
    public bool ReleaseCaptive(string generalId)
    {
        var gs = GameState.Instance;
        var progress = gs.GetGeneralProgress(generalId);
        if (progress == null || progress.Status != GeneralStatus.Captive)
            return false;

        // 释放后变为在野状态
        progress.Status = GeneralStatus.Available;
        gs.CaptiveGeneralIds.Remove(generalId);
        gs.Save();
        return true;
    }
}
