using System;
using System.Collections.Generic;

namespace MMXOnline
{
    // Browser implementation of the window and input event API described in Platform/WindowEvents.cs.
    // webgl.js forwards DOM events to WebMain, which queues them here until the game calls DispatchEvents().

    public partial class RenderWindow
    {
        public event EventHandler Closed;
        public event EventHandler<SizeEventArgs> Resized;
        public event EventHandler<KeyEventArgs> KeyPressed;
        public event EventHandler<KeyEventArgs> KeyReleased;
        public event EventHandler<MouseMoveEventArgs> MouseMoved;
        public event EventHandler<MouseButtonEventArgs> MouseButtonPressed;
        public event EventHandler<MouseButtonEventArgs> MouseButtonReleased;
        public event EventHandler<MouseWheelScrollEventArgs> MouseWheelScrolled;
        public event EventHandler<JoystickButtonEventArgs> JoystickButtonPressed;
        public event EventHandler<JoystickButtonEventArgs> JoystickButtonReleased;
        public event EventHandler<JoystickMoveEventArgs> JoystickMoved;
        public event EventHandler<JoystickConnectEventArgs> JoystickConnected;
        public event EventHandler<JoystickConnectEventArgs> JoystickDisconnected;

        internal void raiseKey(Key key, bool down)
        {
            if (down) WebInput.keyPressedThisFrame(key);
            (down ? KeyPressed : KeyReleased)?.Invoke(this, new KeyEventArgs { Code = key });
        }
        internal void raiseMouseMoved(int x, int y) => MouseMoved?.Invoke(this, new MouseMoveEventArgs { X = x, Y = y });
        internal void raiseMouseButton(Mouse.Button button, bool down, int x, int y) =>
            (down ? MouseButtonPressed : MouseButtonReleased)?.Invoke(this, new MouseButtonEventArgs { Button = button, X = x, Y = y });
        internal void raiseMouseWheel(float delta) => MouseWheelScrolled?.Invoke(this, new MouseWheelScrollEventArgs { Delta = delta });
    }

    public static partial class Mouse
    {
        // Browsers don't allow moving the cursor
        public static void SetPosition(Vector2i position, RenderWindow window) { }
    }

    // Gamepads aren't supported in the browser yet
    public static partial class Joystick
    {
        public static void Update() { }
        public static bool IsConnected(uint joystickId) => false;
        public static string GetName(uint joystickId) => "";
    }

    internal static class WebInput
    {
        static readonly Queue<Action<RenderWindow>> pendingEvents = new Queue<Action<RenderWindow>>();

        // KeyboardEvent.code values that don't map to a Key of the same name
        static readonly Dictionary<string, Key> specialKeys = new Dictionary<string, Key>
        {
            ["ArrowLeft"] = Key.Left, ["ArrowRight"] = Key.Right, ["ArrowUp"] = Key.Up, ["ArrowDown"] = Key.Down,
            ["ControlLeft"] = Key.LControl, ["ShiftLeft"] = Key.LShift, ["AltLeft"] = Key.LAlt, ["MetaLeft"] = Key.LSystem,
            ["ControlRight"] = Key.RControl, ["ShiftRight"] = Key.RShift, ["AltRight"] = Key.RAlt, ["MetaRight"] = Key.RSystem,
            ["ContextMenu"] = Key.Menu, ["BracketLeft"] = Key.LBracket, ["BracketRight"] = Key.RBracket,
            ["Backquote"] = Key.Tilde, ["Minus"] = Key.Hyphen, ["NumpadEnter"] = Key.Enter,
            ["NumpadAdd"] = Key.Add, ["NumpadSubtract"] = Key.Subtract, ["NumpadMultiply"] = Key.Multiply, ["NumpadDivide"] = Key.Divide,
        };

        static Key toKey(string code)
        {
            if (specialKeys.TryGetValue(code, out Key key)) return key;
            string name = code;
            if (code.StartsWith("Key")) name = code.Substring(3);
            else if (code.StartsWith("Digit")) name = "Num" + code.Substring(5);
            return Enum.TryParse(name, out key) ? key : Key.Unknown;
        }

        public static void onKey(string code, bool down)
        {
            Key key = toKey(code);
            if (key == Key.Unknown) return;
            if (down) pendingEvents.Enqueue(window => window.raiseKey(key, true));
            else pendingEvents.Enqueue(window => releaseKey(window, key));
        }

        // Keys pressed in the current dispatch. A release of one of them waits for the next frame,
        // otherwise a quick tap would be pressed and released before the game sees it.
        static readonly HashSet<Key> keysPressedThisFrame = new HashSet<Key>();
        static readonly List<Action<RenderWindow>> deferredEvents = new List<Action<RenderWindow>>();

        static void releaseKey(RenderWindow window, Key key)
        {
            if (keysPressedThisFrame.Contains(key)) deferredEvents.Add(w => releaseKey(w, key));
            else window.raiseKey(key, false);
        }

        public static void onMouseMove(int x, int y) => pendingEvents.Enqueue(window => window.raiseMouseMoved(x, y));

        // DOM button numbers: 0 left, 1 middle, 2 right, 3 back, 4 forward
        public static void onMouseButton(int button, bool down, int x, int y)
        {
            Mouse.Button mouseButton = button switch
            {
                1 => Mouse.Button.Middle,
                2 => Mouse.Button.Right,
                3 => Mouse.Button.XButton1,
                4 => Mouse.Button.XButton2,
                _ => Mouse.Button.Left
            };
            pendingEvents.Enqueue(window => window.raiseMouseButton(mouseButton, down, x, y));
        }

        // DOM wheel deltas are positive downwards, SFML's upwards
        public static void onMouseWheel(float delta) => pendingEvents.Enqueue(window => window.raiseMouseWheel(-delta));

        public static void keyPressedThisFrame(Key key) => keysPressedThisFrame.Add(key);

        public static void dispatch(RenderWindow window)
        {
            keysPressedThisFrame.Clear();
            while (pendingEvents.Count > 0)
            {
                pendingEvents.Dequeue()(window);
            }
            foreach (var deferred in deferredEvents) pendingEvents.Enqueue(deferred);
            deferredEvents.Clear();
        }
    }
}
