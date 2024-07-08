using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BepInEx.Logging;
using DunGen;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// A class of patches for the DungeonGenerator class.
/// It finds the bracken room in the dungeon if there is one
/// </summary>
[HarmonyPatch(typeof(DungeonGenerator))]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal class DungeonGenPatch
{
    private static readonly ManualLogSource Mls = new("AloeDunGenPatches");

    [HarmonyPatch("ChangeStatus")]
    [HarmonyPostfix]
    public static void OnChangeStatus(DungeonGenerator __instance)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        
        if (__instance.CurrentDungeon == null) LogDebug("CurrentDungeon is null");
        else if (__instance.CurrentDungeon.AllTiles == null) LogDebug("AllTiles is null");
        
        Tile tile = FindTileWithName(__instance.CurrentDungeon, "SmallRoom2");
        if (tile == null) return;
        
        CreateBrackenRoomAINodes(tile.transform);
        AloeSharedData.Instance.PopulateBrackenRoomAloeNodes(tile.transform);
        AloeSharedData.Instance.BrackenRoomDoorPosition = tile.transform.Find("Door1 (18)").position;
    }

    private static void CreateBrackenRoomAINodes(Transform brackenRoomTransform)
    {
        GameObject node1 = new() { name = "AINode", tag = "AINode" };
        GameObject node2 = new() { name = "AINode1", tag = "AINode" };
        GameObject node3 = new() { name = "AINode2", tag = "AINode" };
        
        node1.transform.position = brackenRoomTransform.position + new Vector3(-4.97f, 0f, -13.83f);
        node2.transform.position = brackenRoomTransform.position + new Vector3(1.27f, 0f, -10.89f);
        node3.transform.position = brackenRoomTransform.position + new Vector3(-3.76f, 0f, 1.92f);

        List<GameObject> nodes = [node1, node2, node3];
        nodes.ForEach(node =>
        {
            node.transform.SetParent(brackenRoomTransform, true);
            RoundManager.Instance.insideAINodes.AddItem(node);
        });
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