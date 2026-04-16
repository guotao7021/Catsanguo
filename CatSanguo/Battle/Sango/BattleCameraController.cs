using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using CatSanguo.Core;

namespace CatSanguo.Battle.Sango;

/// <summary>
/// 战场相机控制器 - 自动跟随战斗焦点 + 鼠标边缘滚动 + 滚轮缩放
/// </summary>
public class BattleCameraController
{
    private readonly Camera2D _camera;
    private Vector2 _targetPos;
    private float _targetZoom;
    private bool _manualOverride;
    private float _manualOverrideTimer;

    // 边缘滚动
    private const float EdgeScrollZone = 40f;   // 屏幕边缘触发区域(px)
    private const float EdgeScrollSpeed = 400f;  // 边缘滚动速度(px/s)

    // 缩放
    private const float ZoomSpeed = 0.1f;        // 每次滚轮缩放量
    private const float ZoomLerp = 5f;           // 缩放平滑速度

    // 跟随
    private const float FollowLerp = 4f;         // 自动跟随平滑速度
    private const float ManualOverrideTime = 3f; // 手动操作后锁定自动跟随的时间(s)

    public Camera2D Camera => _camera;

    public BattleCameraController(Camera2D camera)
    {
        _camera = camera;
        _targetPos = camera.Position;
        _targetZoom = camera.Zoom;
    }

    public void Update(InputManager input, float dt, SangoBattlePhase phase,
                       ArmyGroup playerArmy, ArmyGroup enemyArmy)
    {
        // 滚轮缩放
        int scrollDelta = input.ScrollWheelDelta;
        if (scrollDelta != 0)
        {
            float zoomChange = scrollDelta > 0 ? ZoomSpeed : -ZoomSpeed;
            _targetZoom = MathHelper.Clamp(_targetZoom + zoomChange, _camera.MinZoom, _camera.MaxZoom);
        }
        _camera.Zoom = MathHelper.Lerp(_camera.Zoom, _targetZoom, ZoomLerp * dt);

        // 右键拖拽
        if (input.IsRightMouseHeld())
        {
            Vector2 delta = input.MouseDelta;
            if (delta.LengthSquared() > 0.1f)
            {
                _camera.Position -= delta / _camera.Zoom;
                _camera.ClampPosition();
                _manualOverride = true;
                _manualOverrideTimer = ManualOverrideTime;
                _targetPos = _camera.Position;
                return;
            }
        }

        // 鼠标边缘滚动 (仅在非部署/结果阶段)
        if (phase == SangoBattlePhase.Charge || phase == SangoBattlePhase.Melee
            || phase == SangoBattlePhase.RoundCommand || phase == SangoBattlePhase.RoundExecution)
        {
            Vector2 edgeScroll = GetEdgeScrollDirection(input.MousePosition);
            if (edgeScroll.LengthSquared() > 0.01f)
            {
                _camera.Position += edgeScroll * EdgeScrollSpeed / _camera.Zoom * dt;
                _camera.ClampPosition();
                _manualOverride = true;
                _manualOverrideTimer = ManualOverrideTime;
                _targetPos = _camera.Position;
                return;
            }
        }

        // 手动覆盖计时器
        if (_manualOverride)
        {
            _manualOverrideTimer -= dt;
            if (_manualOverrideTimer <= 0)
                _manualOverride = false;
        }

        // 自动跟随
        if (!_manualOverride)
        {
            _targetPos = ComputeFocusPoint(phase, playerArmy, enemyArmy);
        }

        _camera.Position = Vector2.Lerp(_camera.Position, _targetPos, FollowLerp * dt);
        _camera.ClampPosition();
    }

    /// <summary>重置手动覆盖，强制跟随焦点</summary>
    public void ResetManualOverride()
    {
        _manualOverride = false;
        _manualOverrideTimer = 0;
    }

    /// <summary>设置初始位置</summary>
    public void SetPosition(Vector2 pos)
    {
        _camera.Position = pos;
        _targetPos = pos;
    }

    private Vector2 GetEdgeScrollDirection(Vector2 mousePos)
    {
        Vector2 dir = Vector2.Zero;
        if (mousePos.X < EdgeScrollZone) dir.X = -1;
        else if (mousePos.X > GameSettings.ScreenWidth - EdgeScrollZone) dir.X = 1;
        if (mousePos.Y < EdgeScrollZone) dir.Y = -1;
        else if (mousePos.Y > GameSettings.ScreenHeight - EdgeScrollZone) dir.Y = 1;
        return dir;
    }

    private Vector2 ComputeFocusPoint(SangoBattlePhase phase, ArmyGroup playerArmy, ArmyGroup enemyArmy)
    {
        switch (phase)
        {
            case SangoBattlePhase.Deploy:
            case SangoBattlePhase.Countdown:
            case SangoBattlePhase.Result:
                return new Vector2(
                    GameSettings.SangoBattlefieldWidth / 2f,
                    GameSettings.SangoBattlefieldHeight / 2f);

            case SangoBattlePhase.Charge:
            case SangoBattlePhase.Melee:
            case SangoBattlePhase.RoundCommand:
            case SangoBattlePhase.RoundExecution:
            {
                var fighters = playerArmy.GetAllAliveSoldiers()
                    .Concat(enemyArmy.GetAllAliveSoldiers())
                    .Where(s => s.State == SoldierState.Fighting ||
                               s.State == SoldierState.Charging ||
                               s.State == SoldierState.Shooting)
                    .ToList();

                if (fighters.Count > 0)
                {
                    Vector2 centroid = Vector2.Zero;
                    foreach (var s in fighters) centroid += s.Position;
                    return centroid / fighters.Count;
                }
                return _camera.Position;
            }

            default:
                return _camera.Position;
        }
    }
}
