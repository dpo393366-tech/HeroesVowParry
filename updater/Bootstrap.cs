using System;
using System.Diagnostics;
using System.IO;
using BepInEx;
using BepInEx.Preloader.Core.Patching;

[PatcherPluginInfo("local.heroesvow.parry.updater", "Heroes Vow Parry Startup Update", "1.0.0")]
public sealed class ParryStartupUpdate : BasePatcher
{
    public override void Initialize()
    {
        try
        {
            // This runs before the plugin chainloader, never from a combat frame.
            var enabled = Config.Bind("Update", "Enabled", true, "Check the official Parry release at game startup. Settings and other mods are preserved.");
            var root = Paths.GameRootPath;
            Action<string> log = s => {
                Log.LogInfo(s);
                try { File.AppendAllText(Path.Combine(Paths.BepInExRootPath, "Parry-update.log"), DateTime.Now.ToString("s") + " " + s + Environment.NewLine); } catch { }
            };
            using var current = Process.GetCurrentProcess();
            foreach (var other in Process.GetProcessesByName(current.ProcessName))
            {
                using (other)
                {
                    if (other.Id == current.Id) continue;
                    try { if (!other.HasExited && string.Equals(Path.GetDirectoryName(other.MainModule.FileName), root, StringComparison.OrdinalIgnoreCase)) { log("Another game instance is active; update skipped."); return; } }
                    catch { log("Could not exclude another game instance; update skipped."); return; }
                }
            }
            Vow.Updater.Engine.Run(root, enabled.Value, log);
        }
        catch (Exception ex) { Log.LogWarning("Startup update unavailable; existing mods retained: " + ex.Message); }
    }
}
