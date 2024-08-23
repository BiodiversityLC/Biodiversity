using Biodiversity.Creatures.Aloe;
using Biodiversity.Creatures;
using BiodiversityAPI;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Assertions;
using Steamworks.Ugc;

namespace Biodiversity.Items.RubberDuck
{
    internal class RubberDuckHandler : BiodiverseAIHandler<RubberDuckHandler>
    {
        internal RubberDuckAssets Assets { get; set; }

        public RubberDuckHandler() 
        {
            Assets = new RubberDuckAssets("developeritems/rubberduck");
            api.AddBioScrap(Assets.DuckAsset, new int[] { 1 }, new Levels.LevelTypes[] { Levels.LevelTypes.All });
            LethalLib.Modules.Items.RegisterScrap(Assets.DuckAsset, 100, Levels.LevelTypes.All);
        }
    }
}
