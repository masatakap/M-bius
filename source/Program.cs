namespace VideoMosaic;
internal static class Program
{
    [System.Runtime.InteropServices.DllImport("shell32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    internal static readonly System.Diagnostics.Stopwatch Startup=System.Diagnostics.Stopwatch.StartNew();
    static Program() { }
    [STAThread]
    static void Main(string[] args)
    {
        Trace("starting");
        // A process-identified preview lets Windows UI test tools target the app
        // when their AppUserModelID window binding is unavailable.
        if(!args.Contains("--ui-preview")) SetCurrentProcessExplicitAppUserModelID("Mobius.Player");
        AppContext.SetSwitch("System.Windows.Forms.ApplyParentFontToMenus", true);
        ApplicationConfiguration.Initialize();
        Trace("initialized");
        Trace("font start");
        Application.SetDefaultFont(Theme.Font(9));
        Trace("font end");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => LogError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogError(e.ExceptionObject as Exception ?? new Exception("Unknown error"));
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "libmpv-2.dll")))
        {
            MessageBox.Show(Language.T("再生エンジン libmpv-2.dll が見つかりません。配布フォルダーをまとめて展開してください。"), "Möbius"); return;
        }
        if(args.Contains("--verify")){Verification.Run(args.Where(a=>!a.StartsWith("--")).ToArray());return;}
        bool test = args.Contains("--self-test");
        var files=args.Where(a=>!string.IsNullOrWhiteSpace(a)&&!a.StartsWith("--")).Select(Path.GetFullPath).ToArray();
        bool isolated=test||args.Any(a=>a is "--startup-test" or "--shutdown-test" or "--media-test" or "--ui-preview" || a.StartsWith("--language="));
        using var instance=isolated?null:new DesktopInstance();
        if(instance?.Primary==false){if(!instance.Forward(files)){MessageBox.Show(Language.T("起動中のMöbiusに接続できませんでした。少し待ってから再度開いてください。"),"Möbius");Environment.ExitCode=1;}return;}
        var window = new MainWindow(files, test);
        if(instance!=null)window.Shown+=(_,_)=>instance.Listen(incoming=>window.BeginInvoke(()=>window.ReceiveFiles(incoming)));
        Trace("window constructed");
        if(args.Contains("--startup-test"))window.Shown+=(_,_)=>
        {
            double shown=Startup.Elapsed.TotalMilliseconds;
            var timer=new System.Windows.Forms.Timer{Interval=500};
            timer.Tick+=(_,_)=>{timer.Stop();timer.Dispose();File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"startup-test.json"),System.Text.Json.JsonSerializer.Serialize(new{ShownMs=shown}));window.Close();};timer.Start();
        };
        Application.Run(window);
    }
    internal static void Trace(string text) { if (Environment.GetCommandLineArgs().Contains("--self-test")) File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "self-test-trace.log"), $"{DateTime.Now:O} {text}\n"); }
    internal static void InputTrace(string text) { if (Environment.GetEnvironmentVariable("VIDEOMOSAIC_TRACE_INPUT") == "1") File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "input-trace.log"), $"{DateTime.Now:O} {text}\n"); }
    static void LogError(Exception ex)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(Preferences.Location)!); File.AppendAllText(Path.Combine(Path.GetDirectoryName(Preferences.Location)!, "error.log"), $"{DateTime.Now:O}\n{ex}\n"); } catch { }
        MessageBox.Show(Language.T("エラーが発生しました。\n") + ex.Message, "Möbius");
    }
}




