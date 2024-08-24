using Biodiversity.Creatures.Aloe;
using Biodiversity.Creatures;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Assertions;
using Steamworks.Ugc;
using static LethalLib.Modules.ContentLoader;

namespace Biodiversity.Items.Developeritems
{
    internal class DeveloperScarpHandler : BiodiverseAIHandler<DeveloperScarpHandler>
    {
        internal DeveloperScarpAssets Assets { get; set; }

        public DeveloperScarpHandler()
        {
            Assets = new DeveloperScarpAssets("devitems");
            Item[] items = {Assets.DuckAsset};
            for (int i = 0; i < items.Length; i++)
            {
                LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(items[i].spawnPrefab);
                LethalLib.Modules.Items.RegisterScrap(items[i], 1, Levels.LevelTypes.All);
            }
        }
    }
}
