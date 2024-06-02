using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

internal class AloeSharedData
{
    private static AloeSharedData _instance;
    public static AloeSharedData Instance => _instance ??= new AloeSharedData();

    public Dictionary<AloeServer, PlayerControllerB> AloeBoundKidnaps { get; } = new();
    public Dictionary<PlayerControllerB, int> PlayersMaxHealth { get; } = new();

    public Transform BrackenRoomPosition;

    public static void FlushDictionaries()
    {
        Instance.AloeBoundKidnaps.Clear();
        Instance.PlayersMaxHealth.Clear();
    }
}