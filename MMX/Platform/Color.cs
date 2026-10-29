using System;

namespace MMXOnline
{
    // RGBA color with 0-255 components. Platform independent; each graphics backend converts it to its own color type.
    public partial struct Color : IEquatable<Color>
    {
        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Color(byte red, byte green, byte blue) : this(red, green, blue, 255) { }

        public Color(byte red, byte green, byte blue, byte alpha)
        {
            R = red;
            G = green;
            B = blue;
            A = alpha;
        }

        // From a 32-bit RRGGBBAA value
        public Color(uint rgba) : this((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba) { }

        public static readonly Color Black = new Color(0, 0, 0);
        public static readonly Color White = new Color(255, 255, 255);
        public static readonly Color Red = new Color(255, 0, 0);
        public static readonly Color Green = new Color(0, 255, 0);
        public static readonly Color Blue = new Color(0, 0, 255);
        public static readonly Color Yellow = new Color(255, 255, 0);
        public static readonly Color Magenta = new Color(255, 0, 255);
        public static readonly Color Cyan = new Color(0, 255, 255);
        public static readonly Color Transparent = new Color(0, 0, 0, 0);

        public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object obj) => obj is Color other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(R, G, B, A);
        public static bool operator ==(Color left, Color right) => left.Equals(right);
        public static bool operator !=(Color left, Color right) => !left.Equals(right);
        public override string ToString() => $"[Color] R({R}) G({G}) B({B}) A({A})";
    }
}
