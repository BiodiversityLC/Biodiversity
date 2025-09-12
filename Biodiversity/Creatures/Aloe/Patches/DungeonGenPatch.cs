using Biodiversity.Core.Attributes;
using System.Collections.Generic;
using DunGen;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Patches;

/// <summary>
/// A class of patches for the DungeonGenerator class.
/// It finds the bracken room in the dungeon if there is one
/// </summary>
[CreaturePatch("Aloe")]
[HarmonyPatch(typeof(DungeonGenerator))]
internal static class DungeonGenPatch
{
    [HarmonyPatch("ChangeStatus")]
    [HarmonyPostfix]
    public static void OnChangeStatus(DungeonGenerator __instance)
    {
        //__instance.CurrentDungeon.AllTiles

        if (!NetworkManager.Singleton.IsServer) return;

        if (!__instance.CurrentDungeon) BiodiversityPlugin.LogVerbose("CurrentDungeon is null");
        else if (__instance.CurrentDungeon.AllTiles == null) BiodiversityPlugin.LogVerbose("AllTiles is null");

        Tile tile = FindTileWithName(__instance.CurrentDungeon, "SmallRoom2");
        if (!tile) return;

        CreateBrackenRoomAINodes(tile.transform);
        AloeSharedData.Instance.PopulateBrackenRoomAloeNodes(tile.transform);
        AloeSharedData.Instance.BrackenRoomDoorPosition = tile.transform.Find("Door1 (18)").position;
    }

    private static void CreateAlternateAINodes(IReadOnlyCollection<Tile> tiles)
    {
        // foreach (Tile tile in tiles)
        // {
        //     Bounds tileBounds = tile.Bounds;
        //     string key =
        // }
    }

    private static void CreateBrackenRoomAINodes(Transform brackenRoomTransform)
    {
        Vector3 localPosition1 = new(-4.97f, 0f, -13.83f);
        Vector3 localPosition2 = new(3.6215f, 0f, -10.8914f);
        Vector3 localPosition3 = new(-3.7622f, 0f, -1.8452f);

        GameObject node1 = new() { name = "AINode", tag = "AINode" };
        GameObject node2 = new() { name = "AINode1", tag = "AINode" };
        GameObject node3 = new() { name = "AINode2", tag = "AINode" };

        node1.transform.position = brackenRoomTransform.TransformPoint(localPosition1);
        node2.transform.position = brackenRoomTransform.TransformPoint(localPosition2);
        node3.transform.position = brackenRoomTransform.TransformPoint(localPosition3);

        List<GameObject> nodes = [node1, node2, node3];
        for (int i = 0; i < nodes.Count; i++)
        {
            GameObject node = nodes[i];
            node.transform.SetParent(brackenRoomTransform, true);
            RoundManager.Instance.insideAINodes.AddItem(node);
        }
    }

    private static Tile FindTileWithName(Dungeon dungeon, string nameContains)
    {
        if (dungeon)
        {
            for (int i = 0; i < dungeon.AllTiles.Count; i++)
            {
                Tile tile = dungeon.AllTiles[i];
                if (tile.name.Contains(nameContains)) return tile;
            }

            return null;
        }

        BiodiversityPlugin.LogVerbose("Dungeon is null");
        return null;
    }
}