using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Patches;
[HarmonyPatch(typeof(DiskLogListener))]
internal static class DiskLogListenerPatch {
    [HarmonyPatch(nameof(DiskLogListener.LogEvent)), HarmonyPrefix]
    static bool PreventBiodiversityLogsInMainLog(object sender, LogEventArgs eventArgs) {
        if(BiodiversityPlugin.Instance.LogFile == null) return true;
        return sender != BiodiversityPlugin.Logger;
    }
}
