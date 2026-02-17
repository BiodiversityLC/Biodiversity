using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Biodiversity.Items.JunkRadar.BuriedScrap
{
    internal class BuriedScrapsList
    {
        /// <summary>
        /// Describe the properties of a buried scrap
        /// </summary>
        internal class BuriedScrapProperties
        {
            /// <summary>
            /// The buried scrap origin can be used to determine the method of spawning the item
            /// </summary>
            public BuriedScrapOrigin Origin;

            /// <summary>
            /// The specific underground Y position of the item in all possible stages
            /// </summary>
            public (float buried, float halfBuried, float dugged) UndergroundPosition;

            /// <summary>
            /// The specific underground Z rotation of the item
            /// </summary>
            public float UndergroundRotation;

            /// <summary>
            /// The prefab of the buried scrap, used for spawning the item in case Origin is not VanillaItem
            /// </summary>
            public GameObject scrapPrefab;
        }


        /// <summary>
        /// Describe the origin of a buried scrap
        /// </summary>
        internal enum BuriedScrapOrigin
        {
            /// <summary>
            /// Vanilla items are spawned by looking into the vanilla items list with the wanted item name
            /// </summary>
            VanillaItem,

            /// <summary>
            /// Biodiversity items are spawned by getting their defined item prefab with code
            /// </summary>
            BioItem,

            /// <summary>
            /// Biodiversity enemies are spawned with a static item prefab, the enemy is spawned as soon as the item is completely dugged
            /// </summary>
            BioEnemy,
        }



        /// <summary>
        /// Dictionary of possible buried scraps (real scraps when spawned) and their properties
        /// </summary>
        public static Dictionary<string, BuriedScrapProperties> AllItems = new()
        {
            /*{ "V-type engine",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.VanillaItem,
                    UndergroundPosition = (-1f, -0.7f, -0.3f),
                    UndergroundRotation = 30,
                }
            },
            { "Old vase",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    UndergroundPosition = (-1f, -0.6f, -0.15f),
                    UndergroundRotation = -10,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.OldVaseItem.spawnPrefab,
                }
            },*/
            { "Motherboard",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    UndergroundPosition = (-1f, -0.5f, 0.15f),
                    UndergroundRotation = 70,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.MotherboardItem.spawnPrefab,
                }
            },
        };


        /// <summary>
        /// List all possible buried scraps (real scraps when spawned) names
        /// </summary>
        public static List<string> AllItemsNames = [.. AllItems.Keys];


        /// <summary>
        /// Select a random item from the AllItems dictionary and returns its prefab
        /// </summary>
        /// <returns>A randmly selected item prefab</returns>
        public static GameObject GetRandomItem()
        {
            var selectedItem = AllItemsNames[Random.Range(0, AllItemsNames.Count)];
            var properties = AllItems[selectedItem];
            return properties.Origin switch
            {
                BuriedScrapOrigin.VanillaItem => StartOfRound.Instance.allItemsList.itemsList.FirstOrDefault(i => i.itemName.ToLower().Equals(selectedItem.ToLower())).spawnPrefab,
                BuriedScrapOrigin.BioItem => properties.scrapPrefab,
                BuriedScrapOrigin.BioEnemy => null,
                _ => null,
            };
        }
    }
}
