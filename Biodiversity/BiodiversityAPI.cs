using System;
using System.Collections.Generic;
using System.Text;
using LethalLib.Modules;

namespace BiodiversityAPI
{
    public static class api
    {
        public static void AddBioScrap(Item item, int[] rarity, Levels.LevelTypes[] moons)
        {
            for (int i = 0; i < moons.Length; i++)
            {
                LethalLib.Modules.Items.RegisterScrap(item, rarity[i], moons[i]);
            }
        }
    }
}
