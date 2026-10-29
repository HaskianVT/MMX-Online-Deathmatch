namespace MMXOnline
{
    // Conversions between the platform independent types and SFML's, used by the SFML backend
    public partial struct Color
    {
        public static implicit operator SFML.Graphics.Color(Color c) => new SFML.Graphics.Color(c.R, c.G, c.B, c.A);
        public static implicit operator Color(SFML.Graphics.Color c) => new Color(c.R, c.G, c.B, c.A);
    }

    public partial struct Vector2f
    {
        public static implicit operator SFML.System.Vector2f(Vector2f v) => new SFML.System.Vector2f(v.X, v.Y);
        public static implicit operator Vector2f(SFML.System.Vector2f v) => new Vector2f(v.X, v.Y);
    }

    public partial struct Vector2i
    {
        public static implicit operator SFML.System.Vector2i(Vector2i v) => new SFML.System.Vector2i(v.X, v.Y);
        public static implicit operator Vector2i(SFML.System.Vector2i v) => new Vector2i(v.X, v.Y);
    }

    public partial struct Vector2u
    {
        public static implicit operator SFML.System.Vector2u(Vector2u v) => new SFML.System.Vector2u(v.X, v.Y);
        public static implicit operator Vector2u(SFML.System.Vector2u v) => new Vector2u(v.X, v.Y);
    }

    public partial struct IntRect
    {
        public static implicit operator SFML.Graphics.IntRect(IntRect r) => new SFML.Graphics.IntRect(r.Left, r.Top, r.Width, r.Height);
        public static implicit operator IntRect(SFML.Graphics.IntRect r) => new IntRect(r.Left, r.Top, r.Width, r.Height);
    }

    public partial struct FloatRect
    {
        public static implicit operator SFML.Graphics.FloatRect(FloatRect r) => new SFML.Graphics.FloatRect(r.Left, r.Top, r.Width, r.Height);
        public static implicit operator FloatRect(SFML.Graphics.FloatRect r) => new FloatRect(r.Left, r.Top, r.Width, r.Height);
    }

    public partial struct Vec4
    {
        public static implicit operator SFML.Graphics.Glsl.Vec4(Vec4 v) => new SFML.Graphics.Glsl.Vec4(v.X, v.Y, v.Z, v.W);
    }
}
