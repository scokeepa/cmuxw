using Microsoft.Win32;

namespace Cmux.Daemon;

// Cross-platform SSOT with cmuxO (macOS sleepwatcher hook):
//   Suspend / lid-close -> create %TEMP%\cmux-paused.flag
//   Resume  / lid-open  -> delete the flag
// Orchestration skills poll this file to pause/resume worker activity.
internal static class LidPauseWatcher
{
    private static readonly string FlagPath =
        Path.Combine(Path.GetTempPath(), "cmux-paused.flag");

    public static void Start(Action<string> log)
    {
        SystemEvents.PowerModeChanged += (_, e) =>
        {
            try
            {
                switch (e.Mode)
                {
                    case PowerModes.Suspend:
                        File.WriteAllText(FlagPath, string.Empty);
                        log($"[lid-watcher] Suspend -> {FlagPath}");
                        break;
                    case PowerModes.Resume:
                        if (File.Exists(FlagPath)) File.Delete(FlagPath);
                        log($"[lid-watcher] Resume -> removed {FlagPath}");
                        break;
                }
            }
            catch (Exception ex)
            {
                log($"[lid-watcher] Error handling {e.Mode}: {ex.Message}");
            }
        };
        log($"[lid-watcher] Subscribed to SystemEvents.PowerModeChanged (flag: {FlagPath})");
    }
}
