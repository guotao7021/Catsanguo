using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;

namespace CatSanguo.UI;

/// <summary>
/// 对话框基类，提供统一的IsActive状态、输入拦截、绘制顺序管理。
/// 所有UI对话框应继承此类。
/// </summary>
public class DialogBase
{
    /// <summary>对话框是否激活（激活时拦截输入）</summary>
    public bool IsActive { get; protected set; }

    /// <summary>对话框是否模态（模态时阻止背景交互）</summary>
    public bool IsModal { get; protected set; } = true;

    /// <summary>对话框深度（用于排序，值越大越靠上）</summary>
    public int Depth { get; set; } = 0;

    /// <summary>打开时回调</summary>
    public Action? OnOpen { get; set; }

    /// <summary>关闭时回调</summary>
    public Action? OnClose { get; set; }

    /// <summary>
    /// 打开对话框
    /// </summary>
    public virtual void Open()
    {
        IsActive = true;
        OnOpen?.Invoke();
    }

    /// <summary>
    /// 关闭对话框
    /// </summary>
    public virtual void Close()
    {
        IsActive = false;
        OnClose?.Invoke();
    }

    /// <summary>
    /// 获取世界鼠标位置（由Scene设置）
    /// </summary>
    public Vector2 WorldMousePos { get; set; }
}
