using System;

namespace MMXOnline
{
    // Platform independent vector and rectangle types, with the same members as their SFML counterparts.
    // Each graphics backend converts them to its own types.

    public partial struct Vector2f : IEquatable<Vector2f>
    {
        public float X;
        public float Y;

        public Vector2f(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vector2f operator -(Vector2f v) => new Vector2f(-v.X, -v.Y);
        public static Vector2f operator -(Vector2f a, Vector2f b) => new Vector2f(a.X - b.X, a.Y - b.Y);
        public static Vector2f operator +(Vector2f a, Vector2f b) => new Vector2f(a.X + b.X, a.Y + b.Y);
        public static Vector2f operator *(Vector2f v, float x) => new Vector2f(v.X * x, v.Y * x);
        public static Vector2f operator *(float x, Vector2f v) => new Vector2f(v.X * x, v.Y * x);
        public static Vector2f operator /(Vector2f v, float x) => new Vector2f(v.X / x, v.Y / x);

        public bool Equals(Vector2f other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vector2f other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Vector2f left, Vector2f right) => left.Equals(right);
        public static bool operator !=(Vector2f left, Vector2f right) => !left.Equals(right);
        public override string ToString() => $"[Vector2f] X({X}) Y({Y})";
    }

    public partial struct Vector2i
    {
        public int X;
        public int Y;

        public Vector2i(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"[Vector2i] X({X}) Y({Y})";
    }

    public partial struct Vector2u
    {
        public uint X;
        public uint Y;

        public Vector2u(uint x, uint y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"[Vector2u] X({X}) Y({Y})";
    }

    public partial struct IntRect
    {
        public int Left;
        public int Top;
        public int Width;
        public int Height;

        public IntRect(int left, int top, int width, int height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"[IntRect] Left({Left}) Top({Top}) Width({Width}) Height({Height})";
    }

    public partial struct FloatRect
    {
        public float Left;
        public float Top;
        public float Width;
        public float Height;

        public FloatRect(float left, float top, float width, float height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public override string ToString() => $"[FloatRect] Left({Left}) Top({Top}) Width({Width}) Height({Height})";
    }

    // Shader uniform with four floats (vec4 in GLSL)
    public partial struct Vec4
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public Vec4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }
}
