using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MMXOnline
{
    // Browser (WebGL2) implementation of the graphics API described in Platform/Graphics.cs.
    // Vertices are transformed on the CPU like SFML does, then drawn by webgl.js as triangles.

    public partial struct RenderStates
    {
        // Blend mode packed into one int for webgl.js: 4 bits per factor/equation
        internal int blendCode()
        {
            var b = BlendMode;
            return (int)b.ColorSrcFactor | (int)b.ColorDstFactor << 4 | (int)b.ColorEquation << 8 |
                (int)b.AlphaSrcFactor << 12 | (int)b.AlphaDstFactor << 16 | (int)b.AlphaEquation << 20;
        }
    }

    public class Texture
    {
        internal readonly int id;
        // Render texture contents are stored upside down, as in OpenGL, so they're sampled flipped
        internal readonly bool flipped;

        public Texture(string path) : this(new Image(path)) { }

        public Texture(Image image)
        {
            Size = image.Size;
            id = WebGL.createTexture((int)Size.X, (int)Size.Y, image.pixels);
        }

        internal Texture(int id, Vector2u size, bool flipped)
        {
            this.id = id;
            this.flipped = flipped;
            Size = size;
        }

        public Vector2u Size { get; }

        static uint? maximumSize;
        public static uint MaximumSize => maximumSize ??= (uint)WebGL.maxTextureSize();
    }

    public class Image : IDisposable
    {
        // RGBA bytes, row by row
        internal byte[] pixels;
        uint width;
        uint height;

        public Image(string path)
        {
            var image = ImageResult.FromMemory(File.ReadAllBytes(path), ColorComponents.RedGreenBlueAlpha);
            pixels = image.Data;
            width = (uint)image.Width;
            height = (uint)image.Height;
        }

        // Filled with opaque black, like SFML
        public Image(uint width, uint height)
        {
            this.width = width;
            this.height = height;
            pixels = new byte[width * height * 4];
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        }

        public Vector2u Size => new Vector2u(width, height);

        public byte[] Pixels => pixels;

        // Copies sourceRect of source to destX, destY. An empty sourceRect copies the whole source.
        public void Copy(Image source, uint destX, uint destY, IntRect sourceRect)
        {
            if (sourceRect.Width == 0 || sourceRect.Height == 0) sourceRect = new IntRect(0, 0, (int)source.width, (int)source.height);
            int srcLeft = Math.Max(sourceRect.Left, 0);
            int srcTop = Math.Max(sourceRect.Top, 0);
            int srcRight = Math.Min(sourceRect.Left + sourceRect.Width, (int)source.width);
            int srcBottom = Math.Min(sourceRect.Top + sourceRect.Height, (int)source.height);
            int copyWidth = Math.Min(srcRight - srcLeft, (int)width - (int)destX);
            int copyHeight = Math.Min(srcBottom - srcTop, (int)height - (int)destY);
            if (copyWidth <= 0 || copyHeight <= 0) return;

            for (int row = 0; row < copyHeight; row++)
            {
                int srcIndex = (((srcTop + row) * (int)source.width) + srcLeft) * 4;
                int destIndex = ((((int)destY + row) * (int)width) + (int)destX) * 4;
                Buffer.BlockCopy(source.pixels, srcIndex, pixels, destIndex, copyWidth * 4);
            }
        }

        public void FlipHorizontally()
        {
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * (int)width * 4;
                for (int x = 0; x < width / 2; x++)
                {
                    int left = rowStart + (x * 4);
                    int right = rowStart + (((int)width - 1 - x) * 4);
                    for (int c = 0; c < 4; c++)
                    {
                        (pixels[left + c], pixels[right + c]) = (pixels[right + c], pixels[left + c]);
                    }
                }
            }
        }

        public void Dispose()
        {
            pixels = null;
        }
    }

    public class Shader
    {
        internal readonly int id;

        // Throws if the shader can't be compiled
        public Shader(string fragmentShaderCode)
        {
            id = WebGL.createShader(toGlslEs3(fragmentShaderCode));
            if (id == 0) throw new Exception("Could not compile shader.");
        }

        public static bool IsAvailable => true;

        public void SetUniform(string name, float x) => WebGL.setUniformFloat(id, name, x);
        public void SetUniform(string name, int x) => WebGL.setUniformInt(id, name, x);
        public void SetUniform(string name, Vec4 vector) => WebGL.setUniformVec4(id, name, vector.X, vector.Y, vector.Z, vector.W);
        public void SetUniform(string name, Texture texture) => WebGL.setUniformTexture(id, name, texture.id);

        // The game's shaders are desktop GLSL written for SFML. WebGL2 needs GLSL ES 3.00 (WebGL1's GLSL ES 1.00 doesn't allow their loops).
        static string toGlslEs3(string code)
        {
            // texture() is a built-in function in GLSL ES 3.00, so SFML's current texture uniform is renamed
            code = Regex.Replace(code, @"\btexture\b", "u_texture");
            code = code.Replace("texture2D(", "texture(");
            code = code.Replace("gl_TexCoord[0]", "vec4(v_texCoord, 0.0, 1.0)");
            code = code.Replace("gl_FragColor", "fragColor");
            code = code.Replace("gl_Color", "v_color");
            return "#version 300 es\nprecision highp float;\nprecision highp int;\nin vec2 v_texCoord;\nin vec4 v_color;\nout vec4 fragColor;\n" + code;
        }
    }

    public class View
    {
        public View(Vector2f center, Vector2f size)
        {
            Center = center;
            Size = size;
        }

        public Vector2f Center { get; set; }
        public Vector2f Size { get; set; }

        // Part of the target the view is shown in, in 0-1 coordinates
        public FloatRect Viewport { get; set; } = new FloatRect(0, 0, 1, 1);
    }

    public class Font
    {
        internal readonly string path;

        public Font(string path)
        {
            this.path = path;
        }
    }

    // 2D affine transform, as in SFML: x' = a * x + c * y + tx, y' = b * x + d * y + ty
    internal struct Transform2D
    {
        public float a, b, c, d, tx, ty;

        public static readonly Transform2D Identity = new Transform2D { a = 1, d = 1 };

        // Same as SFML's Transformable.Transform
        public static Transform2D fromTransformable(Vector2f position, Vector2f origin, Vector2f scale, float rotation)
        {
            float angle = -rotation * MathF.PI / 180f;
            float cosine = MathF.Cos(angle);
            float sine = MathF.Sin(angle);
            float sxc = scale.X * cosine;
            float syc = scale.Y * cosine;
            float sxs = scale.X * sine;
            float sys = scale.Y * sine;
            return new Transform2D
            {
                a = sxc,
                c = sys,
                tx = -origin.X * sxc - origin.Y * sys + position.X,
                b = -sxs,
                d = syc,
                ty = origin.X * sxs - origin.Y * syc + position.Y
            };
        }

        public Vector2f apply(Vector2f p) => new Vector2f(a * p.X + c * p.Y + tx, b * p.X + d * p.Y + ty);
    }

    public abstract class RenderTarget
    {
        // 0 is the canvas
        internal abstract int targetId { get; }
        internal abstract Vector2u targetSize { get; }

        // SFML copies the view when it's set, so later changes to the View object don't apply until SetView is called again
        Vector2f viewCenter;
        Vector2f viewSize;
        FloatRect viewport = new FloatRect(0, 0, 1, 1);
        bool viewSet;

        public void Draw(Drawable drawable) => drawable.Draw(this, RenderStates.Default);
        public void Draw(Drawable drawable, RenderStates states) => drawable.Draw(this, states);

        public void Draw(Vertex[] vertices, PrimitiveType type) => Draw(vertices, type, RenderStates.Default);
        public void Draw(Vertex[] vertices, PrimitiveType type, RenderStates states) => drawVertices(vertices, type, states, Transform2D.Identity);

        public void Clear(Color color) => WebGL.clear(targetId, color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        public void SetView(View view)
        {
            viewCenter = view.Center;
            viewSize = view.Size;
            viewport = view.Viewport;
            viewSet = true;
        }

        static float[] vertexBuffer = new float[8 * 1024];

        // Converts the vertices to triangles in clip space and draws them
        internal void drawVertices(ReadOnlySpan<Vertex> vertices, PrimitiveType type, RenderStates states, Transform2D transform)
        {
            var size = targetSize;
            Vector2f center = viewSet ? viewCenter : new Vector2f(size.X / 2f, size.Y / 2f);
            Vector2f halfSize = viewSet ? viewSize / 2f : new Vector2f(size.X / 2f, size.Y / 2f);

            Texture texture = states.Texture;
            float texW = texture != null ? texture.Size.X : 1;
            float texH = texture != null ? texture.Size.Y : 1;
            bool flipped = texture?.flipped == true;

            int triangleVertexCount = type switch
            {
                PrimitiveType.Triangles => vertices.Length / 3 * 3,
                PrimitiveType.Quads => vertices.Length / 4 * 6,
                PrimitiveType.TriangleStrip or PrimitiveType.TriangleFan => Math.Max(0, vertices.Length - 2) * 3,
                _ => 0
            };
            if (triangleVertexCount == 0) return;
            if (vertexBuffer.Length < triangleVertexCount * 8) vertexBuffer = new float[triangleVertexCount * 8 * 2];

            int n = 0;
            void add(in Vertex v)
            {
                Vector2f p = transform.apply(v.Position);
                vertexBuffer[n++] = (p.X - center.X) / halfSize.X;
                vertexBuffer[n++] = -(p.Y - center.Y) / halfSize.Y;
                vertexBuffer[n++] = v.TexCoords.X / texW;
                vertexBuffer[n++] = flipped ? 1 - (v.TexCoords.Y / texH) : v.TexCoords.Y / texH;
                vertexBuffer[n++] = v.Color.R / 255f;
                vertexBuffer[n++] = v.Color.G / 255f;
                vertexBuffer[n++] = v.Color.B / 255f;
                vertexBuffer[n++] = v.Color.A / 255f;
            }

            switch (type)
            {
                case PrimitiveType.Triangles:
                    for (int i = 0; i + 2 < vertices.Length; i += 3)
                    {
                        add(vertices[i]); add(vertices[i + 1]); add(vertices[i + 2]);
                    }
                    break;
                case PrimitiveType.Quads:
                    for (int i = 0; i + 3 < vertices.Length; i += 4)
                    {
                        add(vertices[i]); add(vertices[i + 1]); add(vertices[i + 2]);
                        add(vertices[i]); add(vertices[i + 2]); add(vertices[i + 3]);
                    }
                    break;
                case PrimitiveType.TriangleStrip:
                    for (int i = 2; i < vertices.Length; i++)
                    {
                        if (i % 2 == 0) { add(vertices[i - 2]); add(vertices[i - 1]); }
                        else { add(vertices[i - 1]); add(vertices[i - 2]); }
                        add(vertices[i]);
                    }
                    break;
                case PrimitiveType.TriangleFan:
                    for (int i = 2; i < vertices.Length; i++)
                    {
                        add(vertices[0]); add(vertices[i - 1]); add(vertices[i]);
                    }
                    break;
            }

            // Same rounding as SFML's RenderTarget.GetViewport, flipped to OpenGL's bottom-left origin
            int vpX = (int)(0.5f + size.X * viewport.Left);
            int vpTop = (int)(0.5f + size.Y * viewport.Top);
            int vpW = (int)(0.5f + size.X * viewport.Width);
            int vpH = (int)(0.5f + size.Y * viewport.Height);
            int vpY = (int)size.Y - (vpTop + vpH);

            var bytes = MemoryMarshal.AsBytes(vertexBuffer.AsSpan(0, n));
            WebGL.draw(targetId, vpX, vpY, vpW, vpH, texture?.id ?? 0, states.Shader?.id ?? 0, states.blendCode(), bytes, n / 8);
        }
    }

    public class RenderTexture : RenderTarget, IDisposable
    {
        readonly int id;
        readonly Vector2u size;
        internal override int targetId => id;
        internal override Vector2u targetSize => size;

        public RenderTexture(uint width, uint height)
        {
            size = new Vector2u(width, height);
            id = WebGL.createRenderTexture((int)width, (int)height);
            Texture = new Texture(id, size, flipped: true);
        }

        // WebGL draws straight into the texture, so there's nothing to update
        public void Display() { }

        public Texture Texture { get; }

        public void Dispose() => WebGL.deleteTexture(id);
    }

    // The canvas. Events are in WebWindowEvents.cs
    public partial class RenderWindow : RenderTarget
    {
        Vector2u size;
        bool isOpen = true;
        internal override int targetId => 0;
        internal override Vector2u targetSize => size;

        public RenderWindow(uint width, uint height, string title, bool fullscreen)
        {
            Size = new Vector2u(width, height);
        }

        public static Vector2u DesktopSize => new Vector2u(1920, 1080);

        public Vector2u Size
        {
            get => size;
            set
            {
                size = value;
                WebGL.canvasSize((int)value.X, (int)value.Y);
            }
        }

        public bool IsOpen => isOpen;
        public void Close() => isOpen = false;
        public void DispatchEvents() => WebInput.dispatch(this);

        // The browser shows the canvas after each animation frame
        public void Display() { }

        public void SetActive() { }
        public void SetIcon(Image image) { }
        public void SetVerticalSyncEnabled(bool enabled) { }
        public void SetFramerateLimit(uint limit) { }
        public void SetMouseCursorVisible(bool visible) { }
        public void SetMouseCursorGrabbed(bool grabbed) { }
    }

    // A textured rectangle (SFML's Sprite; the game's own Sprite class is the animated sprite)
    public class TextureSprite : Drawable
    {
        public TextureSprite(Texture texture) : this(texture, new IntRect(0, 0, (int)texture.Size.X, (int)texture.Size.Y)) { }

        public TextureSprite(Texture texture, IntRect rectangle)
        {
            Texture = texture;
            TextureRect = rectangle;
        }

        public Texture Texture { get; }
        public IntRect TextureRect { get; set; }
        public Vector2f Position { get; set; }
        public Vector2f Origin { get; set; }
        public Vector2f Scale { get; set; } = new Vector2f(1, 1);
        // Degrees
        public float Rotation { get; set; }
        public Color Color { get; set; } = Color.White;

        readonly Vertex[] vertices = new Vertex[4];

        public void Draw(RenderTarget target, RenderStates states)
        {
            var rect = TextureRect;
            float width = MathF.Abs(rect.Width);
            float height = MathF.Abs(rect.Height);
            float left = rect.Left;
            float right = left + rect.Width;
            float top = rect.Top;
            float bottom = top + rect.Height;

            vertices[0] = new Vertex(new Vector2f(0, 0), Color, new Vector2f(left, top));
            vertices[1] = new Vertex(new Vector2f(0, height), Color, new Vector2f(left, bottom));
            vertices[2] = new Vertex(new Vector2f(width, 0), Color, new Vector2f(right, top));
            vertices[3] = new Vertex(new Vector2f(width, height), Color, new Vector2f(right, bottom));

            states.Texture = Texture;
            target.drawVertices(vertices, PrimitiveType.TriangleStrip, states, Transform2D.fromTransformable(Position, Origin, Scale, Rotation));
        }
    }

    // Base of the filled and outlined shapes. Fill and outline are built like in SFML's Shape.
    public abstract class DrawableShape : Drawable
    {
        public Vector2f Position { get; set; }
        public Vector2f Origin { get; set; }
        public Color FillColor { get; set; } = Color.White;
        public Color OutlineColor { get; set; } = Color.White;
        public float OutlineThickness { get; set; }

        internal abstract Vector2f[] getPoints();

        public void Draw(RenderTarget target, RenderStates states)
        {
            var points = getPoints();
            if (points.Length < 3) return;
            var transform = Transform2D.fromTransformable(Position, Origin, new Vector2f(1, 1), 0);
            states.Texture = null;

            // Fill: a fan around the center
            var center = new Vector2f(0, 0);
            foreach (var p in points) center += p;
            center /= points.Length;
            var fill = new Vertex[points.Length + 2];
            fill[0] = new Vertex(center, FillColor);
            for (int i = 0; i < points.Length; i++) fill[i + 1] = new Vertex(points[i], FillColor);
            fill[points.Length + 1] = fill[1];
            target.drawVertices(fill, PrimitiveType.TriangleFan, states, transform);

            if (OutlineThickness == 0) return;

            var outline = new Vertex[(points.Length + 1) * 2];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2f p0 = i == 0 ? points[points.Length - 1] : points[i - 1];
                Vector2f p1 = points[i];
                Vector2f p2 = points[(i + 1) % points.Length];

                Vector2f n1 = computeNormal(p0, p1);
                Vector2f n2 = computeNormal(p1, p2);
                // Make sure the normals point outside the shape
                if (dot(n1, center - p1) > 0) n1 = -n1;
                if (dot(n2, center - p1) > 0) n2 = -n2;

                float factor = 1f + dot(n1, n2);
                Vector2f normal = (n1 + n2) / factor;

                outline[i * 2] = new Vertex(p1, OutlineColor);
                outline[i * 2 + 1] = new Vertex(p1 + normal * OutlineThickness, OutlineColor);
            }
            outline[points.Length * 2] = outline[0];
            outline[points.Length * 2 + 1] = outline[1];
            target.drawVertices(outline, PrimitiveType.TriangleStrip, states, transform);
        }

        static float dot(Vector2f a, Vector2f b) => a.X * b.X + a.Y * b.Y;

        static Vector2f computeNormal(Vector2f p1, Vector2f p2)
        {
            var normal = new Vector2f(p1.Y - p2.Y, p2.X - p1.X);
            float length = MathF.Sqrt(dot(normal, normal));
            return length != 0 ? normal / length : normal;
        }
    }

    public class RectangleShape : DrawableShape
    {
        readonly Vector2f size;

        public RectangleShape(Vector2f size)
        {
            this.size = size;
        }

        internal override Vector2f[] getPoints() => new[] { new Vector2f(0, 0), new Vector2f(size.X, 0), new Vector2f(size.X, size.Y), new Vector2f(0, size.Y) };
    }

    public class CircleShape : DrawableShape
    {
        readonly float radius;
        uint pointCount = 30;

        public CircleShape(float radius)
        {
            this.radius = radius;
        }

        public void SetPointCount(uint count) => pointCount = count;

        internal override Vector2f[] getPoints()
        {
            var points = new Vector2f[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                float angle = (i * 2 * MathF.PI / pointCount) - (MathF.PI / 2);
                points[i] = new Vector2f(radius + MathF.Cos(angle) * radius, radius + MathF.Sin(angle) * radius);
            }
            return points;
        }
    }

    public class ConvexShape : DrawableShape
    {
        readonly Vector2f[] points;

        public ConvexShape(uint pointCount)
        {
            points = new Vector2f[pointCount];
        }

        public void SetPoint(uint index, Vector2f point) => points[index] = point;

        internal override Vector2f[] getPoints() => points;
    }

    public class VertexArray : Drawable
    {
        readonly List<Vertex> vertices = new List<Vertex>();

        public PrimitiveType PrimitiveType { get; set; }

        public void Append(Vertex vertex) => vertices.Add(vertex);

        public void Draw(RenderTarget target, RenderStates states) => target.drawVertices(CollectionsMarshal.AsSpan(vertices), PrimitiveType, states, Transform2D.Identity);
    }

    // Text drawn with a TrueType font. Not drawn in the browser yet; the game uses its bitmap fonts by default.
    public partial class Text : Drawable
    {
        readonly string str;
        readonly uint characterSize;

        public Text(string str, Font font, uint characterSize)
        {
            this.str = str;
            this.characterSize = characterSize;
        }

        public Vector2f Position { get; set; }
        public Vector2f Scale { get; set; } = new Vector2f(1, 1);
        public Color FillColor { get; set; } = Color.White;
        public Color OutlineColor { get; set; } = Color.Black;
        public float OutlineThickness { get; set; }
        public Styles Style { get; set; }

        // Rough estimate until fonts are rendered
        public FloatRect GetLocalBounds() => new FloatRect(0, 0, str.Length * characterSize * 0.6f, characterSize);

        public void Draw(RenderTarget target, RenderStates states) { }
    }
}
