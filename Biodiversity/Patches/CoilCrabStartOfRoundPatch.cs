using Biodiversity.Creatures.CoilCrab;
using Biodiversity.Util.Attributes;
using HarmonyLib;


namespace Biodiversity.Patches
{
    [CreaturePatch("CoilCrab")]
    [HarmonyPatch(typeof(StartOfRound))]
    internal class CoilCrabStartOfRoundPatch
    {
        [HarmonyPatch(nameof(StartOfRound.SwitchMapMonitorPurpose)), HarmonyPostfix]
        internal static void CoilCrabSounds()
        {
            foreach (Item item in StartOfRound.Instance.allItemsList.itemsList)
            {
                if (item.name == "ComedyMask")
                {
                    CoilCrabHandler.Instance.Assets.CoilShellItem.pocketSFX = item.pocketSFX;
                    CoilCrabHandler.Instance.Assets.CoilShellItem.grabSFX = item.grabSFX;
                    BiodiversityPlugin.LogVerbose("Found the select sfx for the shovel.");
                    break;
                }
            }
        }
    }
}
