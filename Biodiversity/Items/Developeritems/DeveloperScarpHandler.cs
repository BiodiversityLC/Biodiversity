using Biodiversity.Creatures.Aloe;
using Biodiversity.Creatures;
using BiodiversityAPI;
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
            LethalLib.Modules.Utilities.FixMixerGroups(Assets.DuckAsset.spawnPrefab);
            LethalLib.Modules.Items.RegisterScrap(Assets.DuckAsset, 100, Levels.LevelTypes.All);
        }
    }
}
