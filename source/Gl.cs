using System.Runtime.InteropServices;
namespace VideoMosaic;
internal sealed partial class Gl : IDisposable
{
    const string Lib = "opengl32.dll";
    [DllImport("user32.dll")] static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] static extern int ChoosePixelFormat(nint dc, ref PixelFormat p);
    [DllImport("gdi32.dll")] static extern bool SetPixelFormat(nint dc, int format, ref PixelFormat p);
    [DllImport("gdi32.dll")] static extern bool SwapBuffers(nint dc);
    [DllImport(Lib)] static extern nint wglCreateContext(nint dc);
    [DllImport(Lib)] static extern bool wglMakeCurrent(nint dc, nint rc);
    [DllImport(Lib)] static extern bool wglDeleteContext(nint rc);
    [DllImport(Lib, CharSet = CharSet.Ansi)] static extern nint wglGetProcAddress(string name);
    [StructLayout(LayoutKind.Sequential)] struct PixelFormat
    {
        public ushort Size, Version; public uint Flags;
        public byte PixelType, ColorBits, RedBits, RedShift, GreenBits, GreenShift, BlueBits, BlueShift, AlphaBits, AlphaShift;
        public byte AccumBits, AccumRedBits, AccumGreenBits, AccumBlueBits, AccumAlphaBits, DepthBits, StencilBits, AuxBuffers, LayerType, Reserved;
        public uint LayerMask, VisibleMask, DamageMask;
    }
    [DllImport(Lib)] internal static extern void glViewport(int x, int y, int w, int h);
    [DllImport(Lib)] internal static extern void glClearColor(float r, float g, float b, float a);
    [DllImport(Lib)] internal static extern void glClear(uint mask);
    [DllImport(Lib)] internal static extern void glEnable(uint cap);
    [DllImport(Lib)] internal static extern void glDisable(uint cap);
    [DllImport(Lib)] internal static extern void glMatrixMode(uint mode);
    [DllImport(Lib)] internal static extern void glLoadIdentity();
    [DllImport(Lib)] internal static extern void glOrtho(double l, double r, double b, double t, double n, double f);
    [DllImport(Lib)] internal static extern void glBegin(uint mode);
    [DllImport(Lib)] internal static extern void glEnd();
    [DllImport(Lib)] internal static extern void glVertex2f(float x, float y);
    [DllImport(Lib)] internal static extern void glTexCoord2f(float x, float y);
    [DllImport(Lib)] internal static extern void glColor4f(float r, float g, float b, float a);
    [DllImport(Lib)] internal static extern void glBindTexture(uint target, uint texture);
    [DllImport(Lib)] internal static extern void glGenTextures(int n, out uint texture);
    [DllImport(Lib)] internal static extern void glDeleteTextures(int n, ref uint texture);
    [DllImport(Lib)] internal static extern void glTexParameteri(uint target, uint name, int value);
    [DllImport(Lib)] internal static extern void glTexImage2D(uint target, int level, int format, int width, int height, int border, uint pixelFormat, uint type, nint data);
    [DllImport(Lib)] internal static extern void glBlendFunc(uint source, uint destination);
    [DllImport(Lib)] internal static extern nint glGetString(uint name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void Gen(int n, out uint id);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void Delete(int n, ref uint id);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void Bind(uint target, uint id);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void Attach(uint target, uint attachment, uint textureTarget, uint texture, int level);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate uint Status(uint target);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate bool SwapInterval(int interval);
    internal Gen GenFramebuffers = null!;
    internal Delete DeleteFramebuffers = null!;
    internal Bind BindFramebuffer = null!;
    internal Attach FramebufferTexture = null!;
    internal Status FramebufferStatus = null!;
    nint window, dc, rc;
    static readonly nint module = NativeLibrary.Load(Lib);
    public string Renderer { get; }
    public Gl(nint hwnd)
    {
        window = hwnd; dc = GetDC(hwnd);
        var p = new PixelFormat { Size = (ushort)Marshal.SizeOf<PixelFormat>(), Version = 1, Flags = 0x4 | 0x20 | 1, ColorBits = 32, AlphaBits = 8 };
        int format = ChoosePixelFormat(dc, ref p);
        if (format == 0 || !SetPixelFormat(dc, format, ref p)) throw new Exception(Language.T("GPU描画面を作成できませんでした。"));
        rc = wglCreateContext(dc);
        if (rc == 0 || !wglMakeCurrent(dc, rc)) throw new Exception(Language.T("OpenGLを初期化できませんでした。"));
        Renderer = Marshal.PtrToStringAnsi(glGetString(0x1F01)) ?? "unknown";
        if (Renderer.Contains("GDI Generic")) throw new Exception(Language.T("GPUのOpenGLドライバーが利用できません。"));
        GenFramebuffers = Load<Gen>("glGenFramebuffers"); DeleteFramebuffers = Load<Delete>("glDeleteFramebuffers");
        BindFramebuffer = Load<Bind>("glBindFramebuffer"); FramebufferTexture = Load<Attach>("glFramebufferTexture2D"); FramebufferStatus = Load<Status>("glCheckFramebufferStatus");
        var swap = Address("wglSwapIntervalEXT"); if (swap != 0) Marshal.GetDelegateForFunctionPointer<SwapInterval>(swap)(1);
    }
    internal static nint Address(string name)
    {
        nint p = wglGetProcAddress(name);
        if (p == 0 || p == 1 || p == 2 || p == 3 || p == -1) NativeLibrary.TryGetExport(module, name, out p);
        return p;
    }
    static T Load<T>(string name) where T : Delegate
    {
        var p = Address(name); if (p == 0) throw new Exception(Language.T("GPU機能が不足しています: ") + name);
        return Marshal.GetDelegateForFunctionPointer<T>(p);
    }
    public uint Texture(int w, int h, nint data = 0)
    {
        glGenTextures(1, out var texture); glBindTexture(0x0DE1, texture);
        glTexParameteri(0x0DE1, 0x2801, 0x2601); glTexParameteri(0x0DE1, 0x2800, 0x2601);
        glTexParameteri(0x0DE1, 0x2802, 0x812F); glTexParameteri(0x0DE1, 0x2803, 0x812F);
        glTexImage2D(0x0DE1, 0, 0x8058, w, h, 0, 0x80E1, 0x1401, data);
        glBindTexture(0x0DE1, 0); return texture;
    }
    public void BeginCanvas(int width, int height)
    {
        BindFramebuffer(0x8D40, 0); glViewport(0, 0, width, height);
        glClearColor(0, 0, 0, 1); glClear(0x4000);
        glMatrixMode(0x1701); glLoadIdentity(); glOrtho(0, width, height, 0, -1, 1);
        glMatrixMode(0x1700); glLoadIdentity();
    }
    public void Image(uint texture, RectangleF r, bool flipped = false)
    {
        glEnable(0x0DE1); glBindTexture(0x0DE1, texture); glColor4f(1,1,1,1);
        glBegin(7);
        float top = flipped ? 0 : 1, bottom = 1 - top;
        glTexCoord2f(0, top); glVertex2f(r.Left, r.Top);
        glTexCoord2f(1, top); glVertex2f(r.Right, r.Top);
        glTexCoord2f(1, bottom); glVertex2f(r.Right, r.Bottom);
        glTexCoord2f(0, bottom); glVertex2f(r.Left, r.Bottom);
        glEnd(); glBindTexture(0x0DE1, 0); glDisable(0x0DE1);
    }
    public void Fill(RectangleF r, Color c)
    {
        glColor4f(c.R/255f,c.G/255f,c.B/255f,c.A/255f); glBegin(7);
        glVertex2f(r.Left,r.Top);glVertex2f(r.Right,r.Top);glVertex2f(r.Right,r.Bottom);glVertex2f(r.Left,r.Bottom);glEnd();
    }
    public void Reset()
    {
        glDisable(0x0DE1);glDisable(0x0BE2);glBindTexture(0x0DE1,0);
        glColor4f(1,1,1,1); glMatrixMode(0x1701);glLoadIdentity();glMatrixMode(0x1700);glLoadIdentity();
    }
    public void Present() => SwapBuffers(dc);
    public void Dispose() { wglMakeCurrent(0,0); if(rc!=0)wglDeleteContext(rc); if(dc!=0)ReleaseDC(window,dc); }
}


