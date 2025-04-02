using Biodiversity.Util.Attributes;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.Beetler.Patches
{
    [CreaturePatch("Beetler")]
    [HarmonyPatch(typeof(ButlerEnemyAI))]
    internal static class BeetlerPatch
    {

        // I love the part where he said it's beetlin time.
        [HarmonyPatch(nameof(ButlerEnemyAI.Start)), HarmonyPrefix]
        internal static void ItsBeetlinTime(ButlerEnemyAI __instance)
        {
            BiodiversityPlugin.Logger.LogInfo("He has been bugged");
            if (BeetlerHandler.Instance.Config.EnableBeetler) {
                __instance.gameObject.AddComponent<BeetlerEnemy>();
            }
        }
    }
}
