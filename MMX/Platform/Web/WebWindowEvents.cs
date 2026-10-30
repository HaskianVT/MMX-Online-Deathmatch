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

        internal void raiseJoystickConnected(uint id, bool connected) =>
            (connected ? JoystickConnected : JoystickDisconnected)?.Invoke(this, new JoystickConnectEventArgs { JoystickId = id });
        internal void raiseJoystickButton(uint id, uint button, bool pressed) =>
            (pressed ? JoystickButtonPressed : JoystickButtonReleased)?.Invoke(this, new JoystickButtonEventArgs { JoystickId = id, Button = button });
        internal void raiseJoystickMoved(uint id, Joystick.Axis axis, float position) =>
            JoystickMoved?.Invoke(this, new JoystickMoveEventArgs { JoystickId = id, Axis = axis, Position = position });
    }

    public static partial class Mouse
    {
        // Browsers don't allow moving the cursor
        public static void SetPosition(Vector2i position, RenderWindow window) { }
    }

    // Gamepads through the browser's Gamepad API (gamepad.js), polled every frame and turned into SFML-like joystick events.
    // Buttons keep the browser's standard layout numbers, which match the game's generic controller mapping (0 A, 1 B, 2 X, 3 Y,
    // 4 LB, 5 RB, 8 Back, 9 Start). The left stick and the d-pad both drive the X/Y axes; the right stick is the U/R axes.
    public static partial class Joystick
    {
        const int maxJoysticks = 4;
        const int buttonCount = 17;
        const int axisCount = 4;
        const float buttonThreshold = 0.5f;
        // Smallest axis change reported, out of 100, like SFML's joystick threshold
        const float axisThreshold = 1;

        class State
        {
            public bool connected;
            public readonly bool[] buttons = new bool[buttonCount];
            public readonly float[] axes = new float[Enum.GetValues<Axis>().Length];
        }

        static readonly State[] states = createStates();
        static readonly double[] polled = new double[1 + buttonCount + axisCount];

        static State[] createStates()
        {
            var result = new State[maxJoysticks];
            for (int i = 0; i < maxJoysticks; i++) result[i] = new State();
            return result;
        }

        public static void Update()
        {
            for (int i = 0; i < maxJoysticks; i++) poll((uint)i, null);
        }

        public static bool IsConnected(uint joystickId) => joystickId < maxJoysticks && states[joystickId].connected;

        public static string GetName(uint joystickId) => WebGamepad.name((int)joystickId);

        // Reads the gamepads and raises events on window for what changed
        internal static void dispatch(RenderWindow window)
        {
            for (int i = 0; i < maxJoysticks; i++) poll((uint)i, window);
        }

        static void poll(uint id, RenderWindow window)
        {
            WebGamepad.poll((int)id, polled);
            var state = states[id];
            bool connected = polled[0] != 0;
            if (connected != state.connected)
            {
                state.connected = connected;
                window?.raiseJoystickConnected(id, connected);
            }
            if (!connected) return;

            for (int b = 0; b < buttonCount; b++)
            {
                bool pressed = polled[1 + b] >= buttonThreshold;
                if (pressed == state.buttons[b]) continue;
                state.buttons[b] = pressed;
                window?.raiseJoystickButton(id, (uint)b, pressed);
            }

            float axis(int index) => (float)polled[1 + buttonCount + index] * 100;
            float dpad(int negativeButton, int positiveButton) => (state.buttons[positiveButton] ? 100 : 0) - (state.buttons[negativeButton] ? 100 : 0);
            float dpadX = dpad(14, 15);
            float dpadY = dpad(12, 13);

            setAxis(id, state, Axis.X, dpadX != 0 ? dpadX : axis(0), window);
            setAxis(id, state, Axis.Y, dpadY != 0 ? dpadY : axis(1), window);
            setAxis(id, state, Axis.U, axis(2), window);
            setAxis(id, state, Axis.R, axis(3), window);
        }

        static void setAxis(uint id, State state, Axis axis, float position, RenderWindow window)
        {
            ref float current = ref state.axes[(int)axis];
            // Always report coming back to rest, so a stick isn't left slightly pressed
            if (MathF.Abs(position - current) < axisThreshold && !(position == 0 && current != 0)) return;
            current = position;
            window?.raiseJoystickMoved(id, axis, position);
        }
    }

    // Calls into gamepad.js (imported in main.js as the "platform" module's "gamepad" object)
    internal static partial class WebGamepad
    {
        [System.Runtime.InteropServices.JavaScript.JSImport("gamepad.poll", "platform")]
        internal static partial void poll(int index,
            [System.Runtime.InteropServices.JavaScript.JSMarshalAs<System.Runtime.InteropServices.JavaScript.JSType.MemoryView>] Span<double> state);

        [System.Runtime.InteropServices.JavaScript.JSImport("gamepad.name", "platform")]
        internal static partial string name(int index);
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
            Joystick.dispatch(window);
        }
    }
}
