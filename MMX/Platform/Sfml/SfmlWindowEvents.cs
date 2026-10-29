using System;

namespace MMXOnline
{
    // SFML implementation of the window and input event API described in Platform/WindowEvents.cs

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

        // Raises this window's events from SFML's, which fire during DispatchEvents()
        void forwardEvents()
        {
            sfmlWindow.Closed += (s, e) => Closed?.Invoke(this, EventArgs.Empty);
            sfmlWindow.Resized += (s, e) => Resized?.Invoke(this, new SizeEventArgs { Width = e.Width, Height = e.Height });
            sfmlWindow.KeyPressed += (s, e) => KeyPressed?.Invoke(this, new KeyEventArgs { Code = (Key)e.Code });
            sfmlWindow.KeyReleased += (s, e) => KeyReleased?.Invoke(this, new KeyEventArgs { Code = (Key)e.Code });
            sfmlWindow.MouseMoved += (s, e) => MouseMoved?.Invoke(this, new MouseMoveEventArgs { X = e.X, Y = e.Y });
            sfmlWindow.MouseButtonPressed += (s, e) => MouseButtonPressed?.Invoke(this, new MouseButtonEventArgs { Button = (Mouse.Button)e.Button, X = e.X, Y = e.Y });
            sfmlWindow.MouseButtonReleased += (s, e) => MouseButtonReleased?.Invoke(this, new MouseButtonEventArgs { Button = (Mouse.Button)e.Button, X = e.X, Y = e.Y });
            sfmlWindow.MouseWheelScrolled += (s, e) => MouseWheelScrolled?.Invoke(this, new MouseWheelScrollEventArgs { Delta = e.Delta });
            sfmlWindow.JoystickButtonPressed += (s, e) => JoystickButtonPressed?.Invoke(this, new JoystickButtonEventArgs { JoystickId = e.JoystickId, Button = e.Button });
            sfmlWindow.JoystickButtonReleased += (s, e) => JoystickButtonReleased?.Invoke(this, new JoystickButtonEventArgs { JoystickId = e.JoystickId, Button = e.Button });
            sfmlWindow.JoystickMoved += (s, e) => JoystickMoved?.Invoke(this, new JoystickMoveEventArgs { JoystickId = e.JoystickId, Axis = (Joystick.Axis)e.Axis, Position = e.Position });
            sfmlWindow.JoystickConnected += (s, e) => JoystickConnected?.Invoke(this, new JoystickConnectEventArgs { JoystickId = e.JoystickId });
            sfmlWindow.JoystickDisconnected += (s, e) => JoystickDisconnected?.Invoke(this, new JoystickConnectEventArgs { JoystickId = e.JoystickId });
        }
    }

    public static partial class Mouse
    {
        // Position relative to the window
        public static void SetPosition(Vector2i position, RenderWindow window) => SFML.Window.Mouse.SetPosition(position, window.sfmlWindow);
    }

    public static partial class Joystick
    {
        // Refreshes the joystick states outside of DispatchEvents
        public static void Update() => SFML.Window.Joystick.Update();

        public static bool IsConnected(uint joystickId) => SFML.Window.Joystick.IsConnected(joystickId);

        public static string GetName(uint joystickId) => SFML.Window.Joystick.GetIdentification(joystickId).Name;
    }
}
