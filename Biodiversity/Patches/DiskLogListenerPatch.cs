using BepInEx.Logging;
using HarmonyLib;

namespace Biodiversity.Patches;
[HarmonyPatch(typeof(DiskLogListener))]
internal static class DiskLogListenerPatch {/*
    [HarmonyPatch(nameof(DiskLogListener.LogEvent)), HarmonyPrefix]
    static bool PreventBiodiversityLogsInMainLog(object sender, LogEventArgs eventArgs) {
        if(BiodiversityPlugin.Instance.LogFile == null) return true;
        if(sender == BiodiversityPlugin.Logger) {
            return false;
        }

        if(eventArgs.Data.ToString().ToLower().Contains("biodiversity")) {
            BiodiversityPlugin.Instance.LogFile.WriteLine(eventArgs.ToString());
            return true;
        }

        return true;
    }*/
}
