using System;

namespace MMXOnline
{
    // Window and input event API the game uses, modeled on SFML's. RenderWindow raises these events from DispatchEvents():
    //   Closed  Resized  KeyPressed  KeyReleased  MouseMoved  MouseButtonPressed  MouseButtonReleased  MouseWheelScrolled
    //   JoystickButtonPressed  JoystickButtonReleased  JoystickMoved  JoystickConnected  JoystickDisconnected
    // Each backend also implements Mouse.SetPosition(position, window), Joystick.Update(), Joystick.IsConnected(id) and Joystick.GetName(id).

    public static partial class Mouse
    {
        public enum Button
        {
            Left,
            Right,
            Middle,
            XButton1,
            XButton2
        }
    }

    public static partial class Joystick
    {
        // The values match SFML's, which saved controller mappings rely on (axes are stored as 1000 + axis)
        public enum Axis
        {
            X,
            Y,
            Z,
            R,
            U,
            V,
            PovX,
            PovY
        }
    }

    public class SizeEventArgs : EventArgs
    {
        public uint Width;
        public uint Height;
    }

    public class KeyEventArgs : EventArgs
    {
        public Key Code;
    }

    public class MouseMoveEventArgs : EventArgs
    {
        public int X;
        public int Y;
    }

    public class MouseButtonEventArgs : EventArgs
    {
        public Mouse.Button Button;
        public int X;
        public int Y;
    }

    public class MouseWheelScrollEventArgs : EventArgs
    {
        public float Delta;
    }

    public class JoystickButtonEventArgs : EventArgs
    {
        public uint JoystickId;
        public uint Button;
    }

    public class JoystickMoveEventArgs : EventArgs
    {
        public uint JoystickId;
        public Joystick.Axis Axis;
        // -100 to 100
        public float Position;
    }

    public class JoystickConnectEventArgs : EventArgs
    {
        public uint JoystickId;
    }
}
