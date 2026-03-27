using Silk.NET.GLFW;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;

namespace Drawing;

/// <summary>
/// Off-screen 2D drawing canvas backed by an OpenGL 3.3 Core FBO.
/// Uses Silk.NET.GLFW directly so context creation and MakeContextCurrent
/// happen in the right order — no Silk.NET.Windowing SDL fallback involved.
///
/// Coordinate system: pixel coordinates with (0,0) at top-left.
///
/// macOS note: GLFW must be called from the main thread on macOS.
/// </summary>
public sealed unsafe class Canvas : IDisposable
{
    // ── GLFW is initialised once per process ──────────────────────────────
    private static readonly Glfw s_glfw;

    static Canvas()
    {
        s_glfw = Glfw.GetApi();
        if (!s_glfw.Init())
            throw new InvalidOperationException("GLFW initialisation failed.");
    }

    // ── Instance fields ───────────────────────────────────────────────────
    private readonly int _width;
    private readonly int _height;
    private readonly WindowHandle* _glfwWindow;
    private readonly GL _gl;
    private readonly uint _fbo;
    private readonly uint _colorRbo;
    private readonly uint _shaderProgram;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly int _uResolution;
    private readonly int _uColor;
    private bool _disposed;

    private const string VertexShaderSource = """
        #version 330 core
        layout(location = 0) in vec2 position;
        uniform vec2 uResolution;
        void main()
        {
            // pixel coords (0,0 top-left) -> clip space (0,0 centre, Y-up)
            vec2 clip = (position / uResolution) * 2.0 - 1.0;
            clip.y = -clip.y;
            gl_Position = vec4(clip, 0.0, 1.0);
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        uniform vec4 uColor;
        out vec4 fragColor;
        void main() { fragColor = uColor; }
        """;

    public int Width  => _width;
    public int Height => _height;

    public Canvas(int width, int height)
    {
        _width  = width;
        _height = height;

        // ── 1. Configure GLFW window hints ────────────────────────────────
        s_glfw.WindowHint(WindowHintBool.Visible, false);
        s_glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        s_glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        s_glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        // macOS requires forward-compatible Core Profile for OpenGL 3.2+
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            s_glfw.WindowHint(WindowHintBool.OpenGLForwardCompat, true);

        // ── 2. Create a 1×1 invisible window (just for the GL context) ────
        _glfwWindow = s_glfw.CreateWindow(1, 1, string.Empty, null, null);
        if (_glfwWindow == null)
        {
            byte* desc = null;
            var code = s_glfw.GetError(out desc);
            var msg  = desc != null ? Marshal.PtrToStringAnsi((nint)desc) ?? "unknown" : "unknown";
            throw new InvalidOperationException(
                $"Failed to create GLFW window (error {code}): {msg}");
        }

        // ── 3. Make the GL context current BEFORE loading entry points ────
        s_glfw.MakeContextCurrent(_glfwWindow);

        // ── 4. Load GL entry points via GLFW's proc-address function ──────
        _gl = GL.GetApi(procName => (nint)s_glfw.GetProcAddress(procName));

        // ── 5. Build shader program ───────────────────────────────────────
        _shaderProgram = CreateShaderProgram(VertexShaderSource, FragmentShaderSource);
        _uResolution   = _gl.GetUniformLocation(_shaderProgram, "uResolution");
        _uColor        = _gl.GetUniformLocation(_shaderProgram, "uColor");

        // ── 6. VAO + streaming VBO ────────────────────────────────────────
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false,
            2 * sizeof(float), 0);
        _gl.BindVertexArray(0);

        // ── 7. FBO + renderbuffer at target resolution ────────────────────
        _fbo      = _gl.GenFramebuffer();
        _colorRbo = _gl.GenRenderbuffer();

        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _colorRbo);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8,
            (uint)width, (uint)height);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            RenderbufferTarget.Renderbuffer, _colorRbo);

        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
            throw new InvalidOperationException($"Framebuffer incomplete: {status}");

        _gl.Viewport(0, 0, (uint)width, (uint)height);
        _gl.UseProgram(_shaderProgram);
        _gl.Uniform2(_uResolution, (float)width, (float)height);
    }

    // ── Public drawing API ────────────────────────────────────────────────

    /// <summary>Makes this canvas's GL context current on the calling thread.</summary>
    public void MakeCurrent() => s_glfw.MakeContextCurrent(_glfwWindow);

    /// <summary>Clears the canvas to <paramref name="color"/>.</summary>
    public void Clear(RgbaColor color)
    {
        _gl.ClearColor(color.R, color.G, color.B, color.A);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
    }

    /// <summary>
    /// Draws a filled convex polygon using GL_TRIANGLE_FAN.
    /// Voronoi cells are always convex so this produces correct results.
    /// </summary>
    public void DrawFilledPolygon(ReadOnlySpan<(float x, float y)> points, RgbaColor color)
    {
        if (points.Length < 3) return;
        UploadVertices(points);
        SetColor(color);
        _gl.DrawArrays(PrimitiveType.TriangleFan, 0, (uint)points.Length);
    }

    /// <summary>Convenience overload accepting float[2] coordinate pairs.</summary>
    public void DrawFilledPolygon(float[][] coordinates, RgbaColor color)
        => DrawFilledPolygon(ToTuples(coordinates), color);

    /// <summary>Draws a polyline (GL_LINE_STRIP).</summary>
    public void DrawPolyline(ReadOnlySpan<(float x, float y)> points, RgbaColor color)
    {
        if (points.Length < 2) return;
        UploadVertices(points);
        SetColor(color);
        _gl.DrawArrays(PrimitiveType.LineStrip, 0, (uint)points.Length);
    }

    /// <summary>Convenience overload accepting float[2] coordinate pairs.</summary>
    public void DrawPolyline(float[][] coordinates, RgbaColor color)
        => DrawPolyline(ToTuples(coordinates), color);

    /// <summary>
    /// Reads pixels from the FBO and saves as a PNG file.
    /// Flips vertically so (0,0) is top-left in the output image.
    /// </summary>
    public void SaveAsPng(string path)
    {
        using var img = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(GetPixelsTopLeft(), _width, _height);
        img.SaveAsPng(path);
    }

    /// <summary>Returns raw RGBA8 pixels (OpenGL bottom-left origin).</summary>
    public byte[] GetPixels()
    {
        var pixels = new byte[_width * _height * 4];
        fixed (byte* ptr = pixels)
            _gl.ReadPixels(0, 0, (uint)_width, (uint)_height,
                PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
        return pixels;
    }

    /// <summary>Returns RGBA8 pixels flipped to top-left origin.</summary>
    public byte[] GetPixelsTopLeft() => FlipVertically(GetPixels(), _width, _height);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // GL objects are freed automatically when the context is destroyed.
        _gl?.Dispose();

        s_glfw.MakeContextCurrent(null);
        if (_glfwWindow != null)
            s_glfw.DestroyWindow(_glfwWindow);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void UploadVertices(ReadOnlySpan<(float x, float y)> points)
    {
        var vertices = new float[points.Length * 2];
        for (int i = 0; i < points.Length; i++)
        {
            vertices[i * 2]     = points[i].x;
            vertices[i * 2 + 1] = points[i].y;
        }
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* ptr = vertices)
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertices.Length * sizeof(float)), ptr, BufferUsageARB.StreamDraw);
    }

    private void SetColor(RgbaColor c)
        => _gl.Uniform4(_uColor, c.R, c.G, c.B, c.A);

    private static (float x, float y)[] ToTuples(float[][] coords)
    {
        var pts = new (float, float)[coords.Length];
        for (int i = 0; i < coords.Length; i++)
            pts[i] = (coords[i][0], coords[i][1]);
        return pts;
    }

    private static byte[] FlipVertically(byte[] pixels, int width, int height)
    {
        var result = new byte[pixels.Length];
        int stride = width * 4;
        for (int y = 0; y < height; y++)
            Array.Copy(pixels, (height - 1 - y) * stride, result, y * stride, stride);
        return result;
    }

    private uint CreateShaderProgram(string vertSrc, string fragSrc)
    {
        uint vert = CompileShader(ShaderType.VertexShader,   vertSrc);
        uint frag = CompileShader(ShaderType.FragmentShader, fragSrc);

        uint prog = _gl.CreateProgram();
        _gl.AttachShader(prog, vert);
        _gl.AttachShader(prog, frag);
        _gl.LinkProgram(prog);

        _gl.GetProgram(prog, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = _gl.GetProgramInfoLog(prog);
            _gl.DeleteProgram(prog);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }

        _gl.DeleteShader(vert);
        _gl.DeleteShader(frag);
        return prog;
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);

        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new InvalidOperationException($"{type} compile failed: {log}");
        }
        return shader;
    }
}
