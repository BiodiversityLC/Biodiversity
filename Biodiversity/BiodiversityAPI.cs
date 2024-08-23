using System;
using System.Collections.Generic;
using System.Text;
using LethalLib.Modules;

namespace Biodiversity
{
    public static class BiodiversityAPI
    {

        public static void AddBioScrap(Item item, int rarity, Levels.LevelTypes moon)
        {
            LethalLib.Modules.Items.RegisterScrap(item, rarity, moon);
        }
    }
}
