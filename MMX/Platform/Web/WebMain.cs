using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices.JavaScript;

namespace MMXOnline
{
    // Entry point of the browser build. main.js loads the assets, then calls start() once and frame() on every animation frame.
    public static partial class WebMain
    {
        public static void Main()
        {
        }

        // Extracts the zipped assets folder into the in-memory file system, where the game reads them with File/Directory as usual
        [JSExport]
        public static void loadAssets(byte[] assetsZip)
        {
            using var stream = new MemoryStream(assetsZip);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            zip.ExtractToDirectory("assets", overwriteFiles: true);
        }

        // Writes a file next to the game, e.g. the region.txt published with the page
        [JSExport]
        public static void writeFile(string name, string content)
        {
            File.WriteAllText(name, content);
        }

        [JSExport]
        public static bool start()
        {
            WebNetwork.install();
            return Program.init();
        }

        [JSExport]
        public static void frame()
        {
            Program.runFrame();
        }

        [JSExport]
        public static void onKey(string code, bool down) => WebInput.onKey(code, down);

        [JSExport]
        public static void onMouseMove(int x, int y) => WebInput.onMouseMove(x, y);

        [JSExport]
        public static void onMouseButton(int button, bool down, int x, int y) => WebInput.onMouseButton(button, down, x, y);

        [JSExport]
        public static void onMouseWheel(float delta) => WebInput.onMouseWheel(delta);
    }

    // Calls into webgl.js (imported in main.js as the "platform" module's "gl" object)
    internal static partial class WebGL
    {
        [JSImport("gl.maxTextureSize", "platform")]
        internal static partial int maxTextureSize();

        [JSImport("gl.canvasSize", "platform")]
        internal static partial void canvasSize(int width, int height);

        [JSImport("gl.createTexture", "platform")]
        internal static partial int createTexture(int width, int height, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgbaPixels);

        [JSImport("gl.createRenderTexture", "platform")]
        internal static partial int createRenderTexture(int width, int height);

        [JSImport("gl.deleteTexture", "platform")]
        internal static partial void deleteTexture(int texture);

        // Returns 0 if the shader doesn't compile
        [JSImport("gl.createShader", "platform")]
        internal static partial int createShader(string fragmentSource);

        [JSImport("gl.setUniformFloat", "platform")]
        internal static partial void setUniformFloat(int shader, string name, float x);

        [JSImport("gl.setUniformInt", "platform")]
        internal static partial void setUniformInt(int shader, string name, int x);

        [JSImport("gl.setUniformVec4", "platform")]
        internal static partial void setUniformVec4(int shader, string name, float x, float y, float z, float w);

        [JSImport("gl.setUniformTexture", "platform")]
        internal static partial void setUniformTexture(int shader, string name, int texture);

        // target 0 is the canvas
        [JSImport("gl.clear", "platform")]
        internal static partial void clear(int target, float r, float g, float b, float a);

        // vertices are triangles of 8 floats each: x, y (clip space), u, v, r, g, b, a
        [JSImport("gl.draw", "platform")]
        internal static partial void draw(int target, int viewportX, int viewportY, int viewportW, int viewportH,
            int texture, int shader, int blendMode, [JSMarshalAs<JSType.MemoryView>] Span<byte> vertices, int vertexCount);
    }
}
