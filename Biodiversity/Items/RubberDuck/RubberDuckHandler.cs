using Biodiversity.Creatures.Aloe;
using Biodiversity.Creatures;
using BiodiversityAPI;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Assertions;

namespace Biodiversity.Items.RubberDuck
{
    internal class RubberDuckHandler : BiodiverseAIHandler<RubberDuckHandler>
    {
        internal RubberDuckAssets Assets { get; set; }

        public RubberDuckHandler() 
        {
            Assets = new RubberDuckAssets("devitems/rubberduck");
            api.AddBioScrap(Assets.DuckAsset, new int[] { 1 }, new Levels.LevelTypes[] { Levels.LevelTypes.All });
        }
    }
}
