using System.Text.Json;
namespace VideoMosaic;
internal static class Verification
{
    public static void Run(string[] files)
    {
        var results=new List<object>();bool success=true;
        void Check(string name,bool valid,object? detail=null){results.Add(new{name,valid,detail});if(!valid)success=false;}
        foreach(string code in Language.Codes)
        {
            Language.Code=code;
            Check("language-"+code,Language.Translations.All(p=>p.Value.Length==5&&p.Value.All(v=>v.Length>0)));
            using var dialog=new SettingsDialog(new Preferences{UiLanguage=code});
            foreach(var label in dialog.Controls.OfType<Label>())
            {
                var measured=TextRenderer.MeasureText(label.Text,label.Font,new Size(label.Width,int.MaxValue),TextFormatFlags.WordBreak);
                Check("label-fits-"+code,measured.Height<=label.Height,new{label.Text,measured.Height,available=label.Height});
            }
            Check("settings-preserve-language-"+code,dialog.Value.UiLanguage==code);
        }
        // Independently constructed raw and Snappy literal/copy blocks.
        var chrome = new ChromeVisibility();
        var activity = new ChromeActivity(); activity.Observe(new(20,30),0);
        Check("chrome-not-idle-before-three-seconds",!activity.IsIdle(2.99));
        Check("chrome-idle-at-three-seconds",activity.IsIdle(3));
        Check("chrome-stationary-does-not-reset",!activity.Observe(new(20,30),4)&&activity.IsIdle(4));
        Check("chrome-motion-resets-idle",activity.Observe(new(21,30),4)&&!activity.IsIdle(4));
        activity.KeepAlive(7); Check("chrome-click-resets-idle",!activity.IsIdle(9));
        Check("menu-preserves-activating-click",ClickThroughMenuStrip.KeepActivatingClick(2)==1);
        Check("menu-preserves-other-activation-results",new nint[]{1,3,4}.All(v=>ClickThroughMenuStrip.KeepActivatingClick(v)==v));
        Check("chrome-initial-hidden",chrome.Step(0,.016,false)==0);
        Check("chrome-fade-midpoint",Math.Abs(chrome.Step(.09,.09,true)-.5)<.0001);
        Check("chrome-fade-completes",chrome.Step(.18,.09,true)==1);
        Check("chrome-exit-grace",chrome.Step(.4,.22,false)==1);
        Check("chrome-fade-out",Math.Abs(chrome.Step(.5,.09,false)-.5)<.0001);
        Check("chrome-reenter-reverses",chrome.Step(.55,.09,true)==1);
        Check("chrome-hide-when-disabled",chrome.Step(.6,.01,true,false)==0);
        Check("chrome-legacy-preference",JsonSerializer.Deserialize<Preferences>("{\"Volume\":37}") is {AlwaysShowControls:false,Volume:37});
        Check("chrome-preference-roundtrip",JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences{AlwaysShowControls=true}))!.AlwaysShowControls);
        VerifySourceFiles(Check);
        byte[] block=[0x00,0xf8,0,0,0,0,0,0];
        byte[] raw=[8,0,0,0xab,..block];
        Check("raw-bc1",HapPlayback.Decode(raw)[0].Data.SequenceEqual(block));
        byte[] snappy=[8,28,..block];byte[] encoded=[10,0,0,0xbb,..snappy];
        Check("snappy-literal",HapPlayback.Decode(encoded)[0].Data.SequenceEqual(block));
        byte[] copy=[6,0,0,0xbb,8,0,7,26,1,0];
        Check("snappy-overlap-copy",HapPlayback.Decode(copy)[0].Data.SequenceEqual(Enumerable.Repeat((byte)7,8)));
        try{HapPlayback.Decode([4,0,0,0xbb,255]);Check("truncated-rejected",false);}catch(InvalidDataException){Check("truncated-rejected",true);}
        foreach(var path in files)
        {
            try
            {
                using var hap=HapPlayback.Open(path);if(hap==null){Check("hap-open",false,path);continue;}
                bool Wait(Func<bool> predicate){var deadline=DateTime.UtcNow.AddSeconds(5);while(DateTime.UtcNow<deadline){if(hap.Error!=null)throw new Exception(hap.Error);if(predicate())return true;Thread.Sleep(5);}return false;}
                Check("hap-first-frame",Wait(()=>hap.Latest!=null),path);
                hap.Seek(5);Check("hap-seek",Wait(()=>Math.Abs((hap.Latest?.Timestamp??-1)-5)<.05),path);
                hap.Seek(5+1/hap.Fps);Check("hap-next-frame",Wait(()=>Math.Abs((hap.Latest?.Timestamp??-1)-5-1/hap.Fps)<.005),path);
                Check("hap-texture-size",hap.Latest!.Planes.All(p=>p.Data.Length==((hap.Width+3)/4)*((hap.Height+3)/4)*(p.Format is 1 or 11?8:16)),new{path,formats=hap.Latest.Planes.Select(p=>p.Format).ToArray()});
                if(Path.GetFileName(path).Contains("audio"))
                {
                    using var player=new Mpv(path);player.Command("loadfile",path);Thread.Sleep(500);player.Poll();player.Play(true);
                    for(int i=0;i<12;i++){Thread.Sleep(100);player.Poll();}
                    double audio=double.TryParse(player.Raw("time-pos"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var seconds)?seconds:-1;
                    Check("hap-audio-sync",player.Raw("aid") is not ("no" or "")&&Math.Abs(player.Hap!.Position-audio)<.2,new{audio,video=player.Hap!.Position});
                    player.Play(false);player.Command("seek","5","absolute+exact");Thread.Sleep(300);player.Command("frame-step");Thread.Sleep(300);
                    Check("hap-audio-frame-step",Math.Abs(player.Hap.Position-5-1/player.Hap.Fps)<.005);
                }
            }
            catch(Exception ex){Check("hap-error",false,new{path,error=ex.Message});}
        }
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"verification.json"),JsonSerializer.Serialize(new{success,results},new JsonSerializerOptions{WriteIndented=true}));Environment.ExitCode=success?0:1;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    static extern bool SHGetPathFromIDListEx(nint item, System.Text.StringBuilder path, uint count, uint flags);

    static void VerifySourceFiles(Action<string,bool,object?> check)
    {
        string folder=Path.Combine(AppContext.BaseDirectory,"source-file-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string[] paths=[Path.Combine(folder,"映像 01, 比較 & test.mp4"),Path.Combine(folder,"vídeo français 中文.mov")];
        try
        {
            foreach(string path in paths)
            {
                byte[] original=[1,2,3,4,5];File.WriteAllBytes(path,original);
                var data=SourceFileActions.CopyData(path);
                check("copy-original-file-drop",data.GetFileDropList()?.Cast<string>().SequenceEqual([path])==true,Path.GetFileName(path));
                using var effect=(MemoryStream)data.GetData("Preferred DropEffect")!;
                check("copy-effect-not-move",BitConverter.ToInt32(effect.ToArray())==1,null);
                check("copy-leaves-source-intact",File.ReadAllBytes(path).SequenceEqual(original),null);
                nint item=SourceFileActions.ParseItem(path);
                try
                {
                    var result=new System.Text.StringBuilder(32768);
                    check("explorer-item-exact-file",SHGetPathFromIDListEx(item,result,(uint)result.Capacity,0)&&result.ToString()==path,Path.GetFileName(path));
                }
                finally{System.Runtime.InteropServices.Marshal.FreeCoTaskMem(item);}
            }
            string missing=Path.Combine(folder,"removed.mp4");
            try{SourceFileActions.CopyData(missing);check("copy-missing-file",false,null);}catch(FileNotFoundException){check("copy-missing-file",true,null);}
            try{SourceFileActions.ParseItem(missing);check("explorer-missing-file",false,null);}catch(FileNotFoundException){check("explorer-missing-file",true,null);}
        }
        catch(Exception ex){check("source-file-actions",false,ex.Message);}
        finally{foreach(string path in paths)File.Delete(path);Directory.Delete(folder);}
    }
}
