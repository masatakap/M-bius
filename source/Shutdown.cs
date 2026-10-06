using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace VideoMosaic;

// Native destruction can wait for decoder threads. Keep a bounded number of
// dedicated workers so it cannot starve metadata/GPU task continuations.
internal static class PlayerDisposal
{
    static readonly BlockingCollection<(Action Work, TaskCompletionSource Done)> jobs = new();
    static PlayerDisposal()
    {
        for(int i=0;i<4;i++) new Thread(Work) { IsBackground=true, Name="Möbius media cleanup" }.Start();
    }
    static void Work()
    {
        foreach(var job in jobs.GetConsumingEnumerable())
            try { job.Work(); job.Done.SetResult(); }
            catch(Exception ex) { job.Done.SetException(ex); }
    }
    internal static Task Run(Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        jobs.Add((action,done)); return done.Task;
    }
}

internal sealed partial class MainWindow
{
    async void CloseWindow(object? sender, FormClosingEventArgs e)
    {
        if(shutdownComplete) return;
        e.Cancel = true;
        if(closing) return;
        closing = true;
        var elapsed = Stopwatch.StartNew();
        animation.Stop(); metadata.Stop(); pending.Clear();
        // Hide first, while retaining the native GPU window until cleanup ends.
        topChrome?.Hide(); bottomChrome?.Hide(); Hide();
        double hiddenMs = elapsed.Elapsed.TotalMilliseconds;
        var closingCards = cards.ToArray();
        int loaded = closingCards.Count(c=>c.Ready);
        int loading = closingCards.Count(c=>c.Loading);
        Exception? failure = null;
        try
        {
            // End drawing immediately. Native contexts must be released on the GPU
            // thread before the corresponding mpv handles can be destroyed.
            var rendererStopped = gpu.StopAsync();
            Clear();
            await Task.WhenAll(retiredPlayers.Append(rendererStopped));
        }
        catch(Exception ex)
        {
            failure = ex;
            Program.Trace("shutdown: "+ex);
        }
        finally
        {
            if(test || shutdownTest || mediaTest)
            {
                var result = new { success=failure==null, count=closingCards.Length, loaded, loading, hiddenMs, cleanupMs=elapsed.Elapsed.TotalMilliseconds,
                    gpuEngines=gpu.EngineCount, allDisposalsCompleted=closingCards.All(c=>c.Disposal.IsCompletedSuccessfully), error=failure?.ToString() };
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"shutdown-test.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
                if(failure!=null) Environment.ExitCode=1;
            }
            shutdownComplete = true; Close();
        }
    }
    readonly bool shutdownTest = Environment.GetCommandLineArgs().Contains("--shutdown-test");
    void RunShutdownTest(double now)
    {
        bool duringLoad = Environment.GetCommandLineArgs().Contains("--close-during-load");
        if(duringLoad ? cards.Any(c=>c.Loading) : pending.Count==0 && cards.All(c=>c.Ready || c.Error.Length>0))
        { Close(); return; }
        if(now-testStarted>90) { Environment.ExitCode=1; Close(); }
    }
}
