namespace MMXOnline
{
    // Graphics API the game uses, modeled on SFML's. The types in this file are platform independent; each backend
    // (e.g. Platform/Sfml) implements the rest with the same public members:
    //   Texture(string path), Texture(Image)               Size  static MaximumSize
    //   Image(string path), Image(width, height)           Size  Pixels  Copy(source, destX, destY, sourceRect)  FlipHorizontally()  Dispose()
    //   Shader(string fragmentShaderCode)                  static IsAvailable  SetUniform(name, float | int | Vec4 | Texture)
    //   View(center, size)                                 Center  Size  Viewport
    //   Font(string path)
    //   RenderTarget                                       Draw(Drawable[, RenderStates])  Draw(Vertex[], PrimitiveType[, RenderStates])  Clear(Color)  SetView(View)
    //   RenderTexture(width, height) : RenderTarget        Display()  Texture
    //   RenderWindow : RenderTarget                        the game window
    //   TextureSprite, RectangleShape, CircleShape, ConvexShape, VertexArray, Text : Drawable

    // Something that can draw itself on a render target
    public interface Drawable
    {
        void Draw(RenderTarget target, RenderStates states);
    }

    public enum PrimitiveType
    {
        Points = 0,
        Lines = 1,
        LineStrip = 2,
        Triangles = 3,
        TriangleStrip = 4,
        TriangleFan = 5,
        Quads = 6
    }

    public struct Vertex
    {
        public Vector2f Position;
        public Color Color;
        public Vector2f TexCoords;

        public Vertex(Vector2f position) : this(position, Color.White, new Vector2f(0, 0)) { }
        public Vertex(Vector2f position, Color color) : this(position, color, new Vector2f(0, 0)) { }
        public Vertex(Vector2f position, Vector2f texCoords) : this(position, Color.White, texCoords) { }

        public Vertex(Vector2f position, Color color, Vector2f texCoords)
        {
            Position = position;
            Color = color;
            TexCoords = texCoords;
        }
    }

    public struct BlendMode
    {
        public enum Factor
        {
            Zero,
            One,
            SrcColor,
            OneMinusSrcColor,
            DstColor,
            OneMinusDstColor,
            SrcAlpha,
            OneMinusSrcAlpha,
            DstAlpha,
            OneMinusDstAlpha
        }

        public enum Equation
        {
            Add,
            Subtract,
            ReverseSubtract
        }

        public static readonly BlendMode Alpha = new BlendMode(Factor.SrcAlpha, Factor.OneMinusSrcAlpha, Equation.Add, Factor.One, Factor.OneMinusSrcAlpha, Equation.Add);
        public static readonly BlendMode Add = new BlendMode(Factor.SrcAlpha, Factor.One, Equation.Add, Factor.One, Factor.One, Equation.Add);
        public static readonly BlendMode Multiply = new BlendMode(Factor.DstColor, Factor.Zero);
        public static readonly BlendMode None = new BlendMode(Factor.One, Factor.Zero);

        public Factor ColorSrcFactor;
        public Factor ColorDstFactor;
        public Equation ColorEquation;
        public Factor AlphaSrcFactor;
        public Factor AlphaDstFactor;
        public Equation AlphaEquation;

        public BlendMode(Factor sourceFactor, Factor destinationFactor) : this(sourceFactor, destinationFactor, Equation.Add) { }

        public BlendMode(Factor sourceFactor, Factor destinationFactor, Equation blendEquation)
            : this(sourceFactor, destinationFactor, blendEquation, sourceFactor, destinationFactor, blendEquation) { }

        public BlendMode(Factor colorSourceFactor, Factor colorDestinationFactor, Equation colorBlendEquation,
            Factor alphaSourceFactor, Factor alphaDestinationFactor, Equation alphaBlendEquation)
        {
            ColorSrcFactor = colorSourceFactor;
            ColorDstFactor = colorDestinationFactor;
            ColorEquation = colorBlendEquation;
            AlphaSrcFactor = alphaSourceFactor;
            AlphaDstFactor = alphaDestinationFactor;
            AlphaEquation = alphaBlendEquation;
        }
    }

    // How something is drawn: shader, texture and blend mode
    public partial struct RenderStates
    {
        public static readonly RenderStates Default = new RenderStates(BlendMode.Alpha);

        public Shader Shader;
        public Texture Texture;
        // Nullable so a default(RenderStates) still blends with alpha, like SFML's
        private BlendMode? blendMode;

        public BlendMode BlendMode
        {
            get => blendMode ?? BlendMode.Alpha;
            set => blendMode = value;
        }

        public RenderStates(BlendMode blendMode)
        {
            Shader = null;
            Texture = null;
            this.blendMode = blendMode;
        }

        public RenderStates(Shader shader) : this(BlendMode.Alpha)
        {
            Shader = shader;
        }

        public RenderStates(Texture texture) : this(BlendMode.Alpha)
        {
            Texture = texture;
        }

        public RenderStates(RenderStates copy)
        {
            Shader = copy.Shader;
            Texture = copy.Texture;
            blendMode = copy.blendMode;
        }
    }

    public partial class Text
    {
        [System.Flags]
        public enum Styles
        {
            Regular = 0,
            Bold = 1,
            Italic = 2,
            Underlined = 4,
            StrikeThrough = 8
        }
    }
}
