using System.Linq;
using BepInEx.Logging;
using DunGen;
using HarmonyLib;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// A class of patches for the DungeonGenerator class.
/// It finds the bracken room in the dungeon if there is one
/// </summary>
[HarmonyPatch(typeof(DungeonGenerator))]
internal class DungeonGenPatch
{
    private static readonly ManualLogSource Mls = new("AloeDunGenPatches");

    [HarmonyPatch("ChangeStatus")]
    [HarmonyPostfix]
    public static void OnChangeStatus(DungeonGenerator __instance)
    {
        if (__instance.CurrentDungeon == null)
        {
            LogDebug("CurrentDungeon is null");
        }
        
        else if (__instance.CurrentDungeon.AllTiles == null)
        {
            LogDebug("AllTiles is null");
        }
        
        Tile tile = FindTileWithName(__instance.CurrentDungeon, "SmallRoom2");
        if (tile == null) return;
        
        AloeSharedData.Instance.BrackenRoomPosition = tile.transform;
        GameObject brackenRoomAiNode = new()
        {
            name = "BrackenRoomAINode",
            tag = "AINode"
        };
        
        brackenRoomAiNode.transform.SetParent(tile.transform);
        RoundManager.Instance.insideAINodes.AddItem(brackenRoomAiNode);

        LogDebug("We found the Bracken room tile at: " + tile.name);
    }

    private static Tile FindTileWithName(Dungeon dungeon, string nameContains)
    {
        if (dungeon != null) return dungeon.AllTiles.FirstOrDefault(tile => tile.name.Contains(nameContains));
        LogDebug("Dungeon is null");
        return null;
    }
    
    private static void LogDebug(string msg)
    {
        #if DEBUG
        Mls?.LogInfo($"{msg}");
        #endif
    }
}