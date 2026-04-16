using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FontStashSharp;
using CatSanguo.Core;

namespace CatSanguo.UI;

/// <summary>
/// 对话框管理器，使用栈模式管理多个对话框。
/// 支持模态对话框堆叠，顶层对话框拦截输入。
/// </summary>
public class DialogManager
{
    private readonly Stack<DialogBase> _dialogStack = new();

    /// <summary>当前激活的对话框数量</summary>
    public int ActiveCount => _dialogStack.Count;

    /// <summary>是否有激活的对话框</summary>
    public bool HasActiveDialog => _dialogStack.Count > 0;

    /// <summary>当前最顶层对话框（拦截输入）</summary>
    public DialogBase? TopDialog => _dialogStack.Count > 0 ? _dialogStack.Peek() : null;

    /// <summary>
    /// 打开对话框（推入栈顶）
    /// </summary>
    public void Push(DialogBase dialog)
    {
        if (dialog == null) return;

        dialog.Depth = _dialogStack.Count;
        dialog.Open();
        _dialogStack.Push(dialog);
    }

    /// <summary>
    /// 关闭顶层对话框
    /// </summary>
    public void Pop()
    {
        if (_dialogStack.Count == 0) return;

        var dialog = _dialogStack.Pop();
        dialog.Close();
    }

    /// <summary>
    /// 检查是否应该拦截背景输入
    /// </summary>
    public bool ShouldBlockInput()
    {
        return HasActiveDialog && TopDialog?.IsModal == true;
    }
}
