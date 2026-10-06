using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
namespace VideoMosaic;

internal static class DesktopIntegration
{
    public static bool IsRegistered { get { using var key=Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications");return key?.GetValue("Möbius") is string; } }
    public static void OpenDefaults(IWin32Window owner)
    {
        try
        {
            if(!IsRegistered){MessageBox.Show(owner,Language.T("既定のアプリに設定するには、先にインストーラーでMöbiusをインストールしてください。"),"Möbius");return;}
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser="+Uri.EscapeDataString("Möbius")){UseShellExecute=true});
        }
        catch(Exception ex){MessageBox.Show(owner,Language.T("Windowsの設定を開けませんでした。")+"\n"+ex.Message,"Möbius");}
    }
}

internal sealed class DesktopInstance : IDisposable
{
    readonly Mutex mutex;
    readonly Mutex running=new(false,"Mobius.Running");
    readonly string pipeName;
    readonly CancellationTokenSource cancel=new();
    public bool Primary {get;}
    [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(int processId);
    public DesktopInstance()
    {
        string identity=Environment.UserDomainName+"\\"+Environment.UserName+"|"+Path.GetFullPath(AppContext.BaseDirectory).ToUpperInvariant();
        pipeName="Mobius."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..24];
        mutex=new Mutex(true,"Local\\"+pipeName,out bool first);Primary=first;
    }
    public bool Forward(string[] files)
    {
        try
        {
            using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);
            pipe.Connect(8000);using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
            if(int.TryParse(reader.ReadLine(),out int pid))AllowSetForegroundWindow(pid);
            writer.WriteLine(JsonSerializer.Serialize(files));return reader.ReadLine()=="OK";
        }
        catch{return false;}
    }
    public void Listen(Action<string[]> receive)
    {
        _=Task.Run(async()=>
        {
            while(!cancel.IsCancellationRequested)
            {
                try
                {
                    using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(cancel.Token);
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    using var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true);using var writer=new StreamWriter(pipe,new UTF8Encoding(false),1024,true){AutoFlush=true};
                    await writer.WriteLineAsync(Environment.ProcessId.ToString());
                    string? line=await reader.ReadLineAsync(timeout.Token);
                    if(line==null||line.Length>1024*1024)continue;
                    var files=JsonSerializer.Deserialize<string[]>(line)??[];
                    if(files.Length>1000||files.Any(p=>p==null||!Path.IsPathFullyQualified(p)))continue;
                    receive(files);await writer.WriteLineAsync("OK");
                }
                catch(OperationCanceledException){}
                catch(IOException){}
                catch(JsonException){}
                catch(InvalidOperationException){}
            }
        });
    }
    public void Dispose(){cancel.Cancel();if(Primary)mutex.ReleaseMutex();mutex.Dispose();running.Dispose();}
}
