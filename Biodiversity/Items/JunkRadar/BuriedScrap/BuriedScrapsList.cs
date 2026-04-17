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
            /// The buried scrap status can be used to determine the color of the item on the Junk Radar screen
            /// </summary>
            public BuriedScrapStatus Status;

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

            /// <summary>
            /// The prefab of the buried scrap, used for spawning the enemy in case Origin is BioEnemy (the enemy is spawned as soon as the item is completely dugged)
            /// </summary>
            public GameObject enemyPrefab;
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
        /// Describe the status of the buried scrap
        /// </summary>
        public enum BuriedScrapStatus
        {
            Sturdy,
            Fragile,
            UltraFragile,
        }



        /// <summary>
        /// Dictionary of possible buried scraps (real scraps when spawned) and their properties
        /// </summary>
        public static Dictionary<string, BuriedScrapProperties> AllItems = new()
        {
            { "V-type engine",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.VanillaItem,
                    Status = BuriedScrapStatus.Sturdy,
                    UndergroundPosition = (-1f, -0.7f, -0.3f),
                    UndergroundRotation = 30,
                }
            },
            { "Bottles",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.VanillaItem,
                    Status = BuriedScrapStatus.Sturdy,
                    UndergroundPosition = (-1f, -0.7f, -0.2f),
                    UndergroundRotation = 50,
                }
            },
            { "Dust pan",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.VanillaItem,
                    Status = BuriedScrapStatus.Sturdy,
                    UndergroundPosition = (-0.5f, -0.15f, 0.05f),
                    UndergroundRotation = 80,
                }
            },
            { "Metal sheet",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.VanillaItem,
                    Status = BuriedScrapStatus.Sturdy,
                    UndergroundPosition = (-0.4f, -0.1f, 0.03f),
                    UndergroundRotation = -20,
                }
            },
            { "Old vase",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.UltraFragile,
                    UndergroundPosition = (-1.5f, -0.9f, -0.2f),
                    UndergroundRotation = -10,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.OldVaseItem.spawnPrefab,
                }
            },
            { "Motherboard",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.Fragile,
                    UndergroundPosition = (-0.8f, -0.25f, 0.05f),
                    UndergroundRotation = 70,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.MotherboardItem.spawnPrefab,
                }
            },
            { "Coil-crab",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioEnemy,
                    Status = BuriedScrapStatus.Sturdy,
                    UndergroundPosition = (-0.7f, -0.3f, -0.05f),
                    UndergroundRotation = 5,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.CoilCrabItem.spawnPrefab,
                    enemyPrefab = Creatures.CoilCrab.CoilCrabHandler.Instance.Assets.CoilCrabEnemy.enemyPrefab,
                }
            },
            { "Baboon Skull",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.UltraFragile,
                    UndergroundPosition = (-0.85f, -0.25f, 0.09f),
                    UndergroundRotation = -85,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.BaboonSkullItem.spawnPrefab,
                }
            },
            { "Skull",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.Fragile,
                    UndergroundPosition = (-0.7f, -0.33f, -0.02f),
                    UndergroundRotation = -30,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.SkullItem.spawnPrefab,
                }
            },
            { "Masked Mug",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.UltraFragile,
                    UndergroundPosition = (-0.7f, -0.37f, -0.06f),
                    UndergroundRotation = 30,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.MaskedMugItem.spawnPrefab,
                }
            },
            { "Ogopogo Trophy",
                new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.Fragile,
                    UndergroundPosition = (-0.6f, -0.26f, 0.08f),
                    UndergroundRotation = 35,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.OgopogoTrophy.spawnPrefab,
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
                BuriedScrapOrigin.BioEnemy => properties.scrapPrefab,
                _ => null,
            };
        }
    }
}
