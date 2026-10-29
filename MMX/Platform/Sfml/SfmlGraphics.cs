using System;

namespace MMXOnline
{
    // SFML implementation of the graphics API described in Platform/Graphics.cs

    public partial struct RenderStates
    {
        internal SFML.Graphics.RenderStates toSfml()
        {
            var blend = BlendMode;
            var sfmlBlend = new SFML.Graphics.BlendMode(
                (SFML.Graphics.BlendMode.Factor)blend.ColorSrcFactor, (SFML.Graphics.BlendMode.Factor)blend.ColorDstFactor, (SFML.Graphics.BlendMode.Equation)blend.ColorEquation,
                (SFML.Graphics.BlendMode.Factor)blend.AlphaSrcFactor, (SFML.Graphics.BlendMode.Factor)blend.AlphaDstFactor, (SFML.Graphics.BlendMode.Equation)blend.AlphaEquation);
            return new SFML.Graphics.RenderStates(sfmlBlend, SFML.Graphics.Transform.Identity, Texture?.native, Shader?.native);
        }
    }

    public class Texture
    {
        internal readonly SFML.Graphics.Texture native;

        public Texture(string path)
        {
            native = new SFML.Graphics.Texture(path);
        }

        public Texture(Image image)
        {
            native = new SFML.Graphics.Texture(image.native);
        }

        internal Texture(SFML.Graphics.Texture native)
        {
            this.native = native;
        }

        public Vector2u Size => native.Size;

        public static uint MaximumSize => SFML.Graphics.Texture.MaximumSize;
    }

    public class Image : IDisposable
    {
        internal readonly SFML.Graphics.Image native;

        public Image(string path)
        {
            native = new SFML.Graphics.Image(path);
        }

        public Image(uint width, uint height)
        {
            native = new SFML.Graphics.Image(width, height);
        }

        public Vector2u Size => native.Size;

        // RGBA bytes, row by row
        public byte[] Pixels => native.Pixels;

        public void Copy(Image source, uint destX, uint destY, IntRect sourceRect) => native.Copy(source.native, destX, destY, sourceRect);

        public void FlipHorizontally() => native.FlipHorizontally();

        public void Dispose() => native.Dispose();
    }

    public class Shader
    {
        internal readonly SFML.Graphics.Shader native;

        // Throws if the shader can't be compiled
        public Shader(string fragmentShaderCode)
        {
            native = SFML.Graphics.Shader.FromString(null, null, fragmentShaderCode);
        }

        public static bool IsAvailable => SFML.Graphics.Shader.IsAvailable;

        public void SetUniform(string name, float x) => native.SetUniform(name, x);
        public void SetUniform(string name, int x) => native.SetUniform(name, x);
        public void SetUniform(string name, Vec4 vector) => native.SetUniform(name, vector);
        public void SetUniform(string name, Texture texture) => native.SetUniform(name, texture.native);
    }

    public class View
    {
        internal readonly SFML.Graphics.View native;

        public View(Vector2f center, Vector2f size)
        {
            native = new SFML.Graphics.View(center, size);
        }

        public Vector2f Center
        {
            get => native.Center;
            set => native.Center = value;
        }

        public Vector2f Size
        {
            get => native.Size;
            set => native.Size = value;
        }

        // Part of the target the view is shown in, in 0-1 coordinates
        public FloatRect Viewport
        {
            get => native.Viewport;
            set => native.Viewport = value;
        }
    }

    public class Font
    {
        internal readonly SFML.Graphics.Font native;

        public Font(string path)
        {
            native = new SFML.Graphics.Font(path);
        }
    }

    public abstract class RenderTarget
    {
        internal abstract SFML.Graphics.RenderTarget nativeTarget { get; }

        public void Draw(Drawable drawable) => drawable.Draw(this, RenderStates.Default);
        public void Draw(Drawable drawable, RenderStates states) => drawable.Draw(this, states);

        public void Draw(Vertex[] vertices, PrimitiveType type) => Draw(vertices, type, RenderStates.Default);

        public void Draw(Vertex[] vertices, PrimitiveType type, RenderStates states)
        {
            var sfmlVertices = new SFML.Graphics.Vertex[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                sfmlVertices[i] = toSfml(vertices[i]);
            }
            nativeTarget.Draw(sfmlVertices, (SFML.Graphics.PrimitiveType)type, states.toSfml());
        }

        public void Clear(Color color) => nativeTarget.Clear(color);

        public void SetView(View view) => nativeTarget.SetView(view.native);

        internal void drawNative(SFML.Graphics.Drawable drawable, RenderStates states) => nativeTarget.Draw(drawable, states.toSfml());

        internal static SFML.Graphics.Vertex toSfml(Vertex vertex) => new SFML.Graphics.Vertex(vertex.Position, vertex.Color, vertex.TexCoords);
    }

    public class RenderTexture : RenderTarget, IDisposable
    {
        readonly SFML.Graphics.RenderTexture native;
        internal override SFML.Graphics.RenderTarget nativeTarget => native;

        public RenderTexture(uint width, uint height)
        {
            native = new SFML.Graphics.RenderTexture(width, height);
            Texture = new Texture(native.Texture);
        }

        // Updates Texture with what was drawn so far
        public void Display() => native.Display();

        public Texture Texture { get; }

        public void Dispose() => native.Dispose();
    }

    // Events are in SfmlWindowEvents.cs
    public partial class RenderWindow : RenderTarget
    {
        internal readonly SFML.Graphics.RenderWindow sfmlWindow;
        internal override SFML.Graphics.RenderTarget nativeTarget => sfmlWindow;

        public RenderWindow(uint width, uint height, string title, bool fullscreen)
        {
            var mode = new SFML.Window.VideoMode(width, height);
            sfmlWindow = fullscreen ?
                new SFML.Graphics.RenderWindow(mode, title, SFML.Window.Styles.Fullscreen) :
                new SFML.Graphics.RenderWindow(mode, title);
            forwardEvents();
        }

        public static Vector2u DesktopSize => new Vector2u(SFML.Window.VideoMode.DesktopMode.Width, SFML.Window.VideoMode.DesktopMode.Height);

        public Vector2u Size
        {
            get => sfmlWindow.Size;
            set => sfmlWindow.Size = value;
        }

        public bool IsOpen => sfmlWindow.IsOpen;
        public void Close() => sfmlWindow.Close();
        public void DispatchEvents() => sfmlWindow.DispatchEvents();
        public void Display() => sfmlWindow.Display();
        public void SetActive() => sfmlWindow.SetActive();
        public void SetIcon(Image image) => sfmlWindow.SetIcon(image.Size.X, image.Size.Y, image.Pixels);
        public void SetVerticalSyncEnabled(bool enabled) => sfmlWindow.SetVerticalSyncEnabled(enabled);
        public void SetFramerateLimit(uint limit) => sfmlWindow.SetFramerateLimit(limit);
        public void SetMouseCursorVisible(bool visible) => sfmlWindow.SetMouseCursorVisible(visible);
        public void SetMouseCursorGrabbed(bool grabbed) => sfmlWindow.SetMouseCursorGrabbed(grabbed);
    }

    // A textured rectangle (SFML's Sprite; the game's own Sprite class is the animated sprite)
    public class TextureSprite : Drawable
    {
        readonly SFML.Graphics.Sprite native;

        public TextureSprite(Texture texture)
        {
            native = new SFML.Graphics.Sprite(texture.native);
            Texture = texture;
        }

        public TextureSprite(Texture texture, IntRect rectangle)
        {
            native = new SFML.Graphics.Sprite(texture.native, rectangle);
            Texture = texture;
        }

        public Texture Texture { get; }

        public IntRect TextureRect
        {
            get => native.TextureRect;
            set => native.TextureRect = value;
        }

        public Vector2f Position
        {
            get => native.Position;
            set => native.Position = value;
        }

        public Vector2f Origin
        {
            get => native.Origin;
            set => native.Origin = value;
        }

        public Vector2f Scale
        {
            get => native.Scale;
            set => native.Scale = value;
        }

        // Degrees
        public float Rotation
        {
            get => native.Rotation;
            set => native.Rotation = value;
        }

        public Color Color
        {
            get => native.Color;
            set => native.Color = value;
        }

        public void Draw(RenderTarget target, RenderStates states) => target.drawNative(native, states);
    }

    // Base of the filled and outlined shapes
    public abstract class DrawableShape : Drawable
    {
        internal abstract SFML.Graphics.Shape nativeShape { get; }

        public Vector2f Position
        {
            get => nativeShape.Position;
            set => nativeShape.Position = value;
        }

        public Vector2f Origin
        {
            get => nativeShape.Origin;
            set => nativeShape.Origin = value;
        }

        public Color FillColor
        {
            get => nativeShape.FillColor;
            set => nativeShape.FillColor = value;
        }

        public Color OutlineColor
        {
            get => nativeShape.OutlineColor;
            set => nativeShape.OutlineColor = value;
        }

        public float OutlineThickness
        {
            get => nativeShape.OutlineThickness;
            set => nativeShape.OutlineThickness = value;
        }

        public void Draw(RenderTarget target, RenderStates states) => target.drawNative(nativeShape, states);
    }

    public class RectangleShape : DrawableShape
    {
        readonly SFML.Graphics.RectangleShape native;
        internal override SFML.Graphics.Shape nativeShape => native;

        public RectangleShape(Vector2f size)
        {
            native = new SFML.Graphics.RectangleShape(size);
        }
    }

    public class CircleShape : DrawableShape
    {
        readonly SFML.Graphics.CircleShape native;
        internal override SFML.Graphics.Shape nativeShape => native;

        public CircleShape(float radius)
        {
            native = new SFML.Graphics.CircleShape(radius);
        }

        public void SetPointCount(uint count) => native.SetPointCount(count);
    }

    public class ConvexShape : DrawableShape
    {
        readonly SFML.Graphics.ConvexShape native;
        internal override SFML.Graphics.Shape nativeShape => native;

        public ConvexShape(uint pointCount)
        {
            native = new SFML.Graphics.ConvexShape(pointCount);
        }

        public void SetPoint(uint index, Vector2f point) => native.SetPoint(index, point);
    }

    public class VertexArray : Drawable
    {
        readonly SFML.Graphics.VertexArray native = new SFML.Graphics.VertexArray();

        public PrimitiveType PrimitiveType
        {
            get => (PrimitiveType)native.PrimitiveType;
            set => native.PrimitiveType = (SFML.Graphics.PrimitiveType)value;
        }

        public void Append(Vertex vertex) => native.Append(RenderTarget.toSfml(vertex));

        public void Draw(RenderTarget target, RenderStates states) => target.drawNative(native, states);
    }

    // Text drawn with a TrueType font
    public partial class Text : Drawable
    {
        readonly SFML.Graphics.Text native;

        public Text(string str, Font font, uint characterSize)
        {
            native = new SFML.Graphics.Text(str, font.native, characterSize);
        }

        public Vector2f Position
        {
            get => native.Position;
            set => native.Position = value;
        }

        public Vector2f Scale
        {
            get => native.Scale;
            set => native.Scale = value;
        }

        public Color FillColor
        {
            get => native.FillColor;
            set => native.FillColor = value;
        }

        public Color OutlineColor
        {
            get => native.OutlineColor;
            set => native.OutlineColor = value;
        }

        public float OutlineThickness
        {
            get => native.OutlineThickness;
            set => native.OutlineThickness = value;
        }

        public Styles Style
        {
            get => (Styles)native.Style;
            set => native.Style = (SFML.Graphics.Text.Styles)value;
        }

        public FloatRect GetLocalBounds() => native.GetLocalBounds();

        public void Draw(RenderTarget target, RenderStates states) => target.drawNative(native, states);
    }
}
