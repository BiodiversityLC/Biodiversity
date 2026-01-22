using System.Collections.Generic;
using System.Linq;

namespace Biodiversity.Items.JunkRadar.BuriedScrap
{
    internal class BuriedScrapsList
    {
        /// <summary>
        /// Dictionary of possible buried scraps (real scraps when spawned) and their underground position
        /// </summary>
        public static Dictionary<string, float> AllItems = new()
        {
            { "V-Type Engine", -0.0855f },
        };


        /// <summary>
        /// List all possible buried scraps (real scraps when spawned) names
        /// </summary>
        public static List<string> AllItemsNames = [.. AllItems.Keys];
    }
}
