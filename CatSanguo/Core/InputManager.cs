using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CatSanguo.Core;

public class InputManager
{
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _currentMouse;
    private MouseState _previousMouse;

    // Double click detection
    private float _lastClickTime;
    private const float DoubleClickInterval = 0.35f; // seconds

    public Vector2 MousePosition => _currentMouse.Position.ToVector2();

    public int ScrollWheelDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    public Vector2 MouseDelta => (_currentMouse.Position - _previousMouse.Position).ToVector2();

    public bool IsRightMouseHeld()
        => _currentMouse.RightButton == ButtonState.Pressed;

    public bool IsLeftMouseHeld()
        => _currentMouse.LeftButton == ButtonState.Pressed;

    public bool IsLeftMouseReleased()
        => _currentMouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;

    public void Update()
    {
        _previousKeyboard = _currentKeyboard;
        _currentKeyboard = Keyboard.GetState();
        _previousMouse = _currentMouse;
        _currentMouse = Mouse.GetState();
    }

    public bool IsKeyPressed(Keys key)
        => _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    public bool IsKeyHeld(Keys key)
        => _currentKeyboard.IsKeyDown(key);

    public bool IsMouseClicked()
        => _currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;

    public bool IsRightMouseClicked()
        => _currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;

    public bool IsMouseInRect(Rectangle rect)
        => rect.Contains(_currentMouse.Position);

    public bool IsMouseDoubleClicked(float currentTime = 0f)
    {
        bool justClicked = _currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        if (justClicked)
        {
            bool isDoubleClick = (currentTime - _lastClickTime) < DoubleClickInterval && currentTime > 0;
            _lastClickTime = currentTime;
            return isDoubleClick;
        }
        return false;
    }
}
