using System.Diagnostics.CodeAnalysis;
using HarmonyLib;

namespace Biodiversity.Creatures.Aloe.Patches;

[HarmonyPatch(typeof(KnifeItem))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class DevKnifePatch
{
    // [HarmonyPatch(nameof(KnifeItem.ItemActivate))]
    // [HarmonyPostfix]
    private static void DealDamageToSelf(GrabbableObject __instance)
    {
        // NOTE: Don't be an idiot (talking to myself) and add the compiler directives #if DEBUG etc, because it doesn't work when other people compile the mod you dunce
        // Just comment and uncomment the HarmonyPatch attributes
        
        if (__instance.playerHeldBy == null) return;
        __instance.playerHeldBy.DamagePlayer(5);
    }
}