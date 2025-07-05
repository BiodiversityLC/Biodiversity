using System.Collections.Generic;

namespace Biodiversity.Util;

public class VanillaLayersUtil
{
    public static readonly string[] layerNames =
    [
        "Default",                // 0
        "TransparentFX",          // 1
        "Ignore Raycast",         // 2
        "Player",                 // 3
        "Water",                  // 4
        "UI",                     // 5
        "Props",                  // 6
        "HelmetVisor",            // 7
        "Room",                   // 8
        "InteractableObject",     // 9
        "Foliage",                // 10
        "Colliders",              // 11
        "PhysicsObject",          // 12
        "Triggers",               // 13
        "MapRadar",               // 14
        "NavigationSurface",      // 15
        "MoldSpore",              // 16
        "Anomaly",                // 17
        "LineOfSight",            // 18
        "Enemies",                // 19
        "PlayerRagdoll",          // 20
        "MapHazards",             // 21
        "ScanNode",               // 22
        "EnemiesNotRendered",     // 23
        "MiscLevelGeometry",      // 24
        "Terrain",                // 25
        "PlaceableShipObjects",   // 26
        "PlacementBlocker",       // 27
        "Railing",                // 28
        "DecalStickableSurface",  // 29
        "Vehicle",                // 30
    ];
    
    /// <summary>
    /// Returns a list of "index: name" entries for every bit set in the mask.
    /// </summary>
    public static List<string> DecodeMask(int mask)
    {
        uint bits = unchecked((uint)mask);
        List<string> result = [];

        for (int i = 0; i < layerNames.Length; i++)
        {
            if ((bits & (1u << i)) != 0)
            {
                string name = string.IsNullOrEmpty(layerNames[i])
                    ? "<unnamed>"
                    : layerNames[i];
                result.Add($"{i}: {name}");
            }
        }

        return result;
    }
    
    /* ### Mask variables in StartOfRound ###
     *
     * collidersAndRoomMask = 1107298560 -> Room, Colliders, Terrain and Vehicle
     * 
     * collidersAndRoomMaskAndPlayers = 1107298568 -> Players and collidersAndRoomMask
     * 
     * collidersRoomMaskDefaultAndPlayers = 1107298569 -> Default and collidersAndRoomMaskAndPlayers
     * 
     * collidersRoomDefaultAndFoliage = 1107299585 -> Default, Foliage and  collidersAndRoomMask
     * 
     * allPlayersCollideWithMask = -1111790665
     * 
     * walkableSurfacesMask = 1375734025 -> Railing and collidersRoomMaskDefaultAndPlayers
     *
     *
     * ### Mask variables used in vanilla items ###
     *
     * shotgunMask = 524288 -> Enemies
     *
     * shovelMask = 1084754248 -> Player, Props, Room, Colliders, Enemies, MapHazards, EnemiesNotRendered and Vehicle
     */
}