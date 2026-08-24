using Biodiversity.Util;
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
        /// Lazy-initialized dictionary of all buried scraps
        /// </summary>
        private static System.Lazy<Dictionary<string, BuriedScrapProperties>> _allItems = new(() => InitializeAllItems());

        /// <summary>
        /// Dictionary of possible buried scraps (real scraps when spawned) and their properties
        /// </summary>
        public static Dictionary<string, BuriedScrapProperties> AllItems => _allItems.Value;

        /// <summary>
        /// List all possible sturdy buried scraps names
        /// </summary>
        public static List<string> AllSturdyItemsNames => [.. AllItems.Where(item => item.Value.Status == BuriedScrapStatus.Sturdy).Select(item => item.Key)];

        /// <summary>
        /// List all possible fragile buried scraps names
        /// </summary>
        public static List<string> AllFragileItemsNames => [.. AllItems.Where(item => item.Value.Status == BuriedScrapStatus.Fragile).Select(item => item.Key)];

        /// <summary>
        /// List all possible ultra fragile buried scraps names
        /// </summary>
        public static List<string> AllUltraFragileItemsNames => [.. AllItems.Where(item => item.Value.Status == BuriedScrapStatus.UltraFragile).Select(item => item.Key)];

        /// <summary>
        /// The default chance to get a spawn for a sturdy item (is adjusted at runtime based on the current moon)
        /// Needs to sum up to 100 for these 3 chance values
        /// </summary>
        private static readonly int ChanceForSturdyItem = 60;

        /// <summary>
        /// The default chance to get a spawn for a fragile item (is adjusted at runtime based on the current moon)
        /// Needs to sum up to 100 for these 3 chance values
        /// </summary>
        private static readonly int ChanceForFragileItem = 30;

        /// <summary>
        /// The default chance to get a spawn for an ultra fragile item (is adjusted at runtime based on the current moon)
        /// Needs to sum up to 100 for these 3 chance values
        /// </summary>
        private static readonly int ChanceForUltraFragileItem = 10;

        /// <summary>
        /// The number at which all items rarities are adjusted based on the current moon
        /// The value here is set by the config at runtime
        /// </summary>
        private static int? ItemRaritiesFactor = null;

        /// <summary>
        /// Initialize all buried scraps
        /// </summary>
        private static Dictionary<string, BuriedScrapProperties> InitializeAllItems()
        {
            if (!JunkRadarHandler.Instance.Assets.Loaded)
            {
                return [];
            }

            Dictionary<string, BuriedScrapProperties> dict = new()
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
            };

            // Custom items
            if (JunkRadarHandler.Instance?.Assets != null)
            {
                dict["Old vase"] = new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.UltraFragile,
                    UndergroundPosition = (-1.5f, -0.9f, -0.2f),
                    UndergroundRotation = -10,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.OldVaseItem?.spawnPrefab,
                };

                dict["Motherboard"] = new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.Fragile,
                    UndergroundPosition = (-0.8f, -0.25f, 0.05f),
                    UndergroundRotation = 70,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.MotherboardItem?.spawnPrefab,
                };

                dict["Coil-crab"] = new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioEnemy,
                    Status = BuriedScrapStatus.Sturdy,
                    UndergroundPosition = (-0.7f, -0.3f, -0.05f),
                    UndergroundRotation = 5,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.CoilCrabItem?.spawnPrefab,
                    enemyPrefab = Creatures.CoilCrab.CoilCrabHandler.Instance?.Assets?.CoilCrabEnemy?.enemyPrefab,
                };

                dict["Baboon Skull"] = new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.UltraFragile,
                    UndergroundPosition = (-0.85f, -0.25f, 0.09f),
                    UndergroundRotation = -85,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.BaboonSkullItem?.spawnPrefab,
                };

                dict["Skull"] = new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.Fragile,
                    UndergroundPosition = (-0.7f, -0.33f, -0.02f),
                    UndergroundRotation = -30,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.SkullItem?.spawnPrefab,
                };

                dict["Masked Mug"] = new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.UltraFragile,
                    UndergroundPosition = (-0.7f, -0.37f, -0.06f),
                    UndergroundRotation = 30,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.MaskedMugItem?.spawnPrefab,
                };

                dict["Ogopogo Trophy"] = new BuriedScrapProperties()
                {
                    Origin = BuriedScrapOrigin.BioItem,
                    Status = BuriedScrapStatus.Fragile,
                    UndergroundPosition = (-0.6f, -0.26f, 0.08f),
                    UndergroundRotation = 35,
                    scrapPrefab = JunkRadarHandler.Instance.Assets.OgopogoTrophy?.spawnPrefab,
                };
            }

            return dict;
        }


        /// <summary>
        /// Calculate and select a random item from the AllItems dictionary and returns its prefab. The item is selected based on the current moon factory size multiplier
        /// </summary>
        /// <returns>A randomly selected item prefab</returns>
        public static GameObject GetRandomItem()
        {
            SelectableLevel level = StartOfRound.Instance.currentLevel;

            if (level == null)
            {
                return null;
            }

            int? price = MoonUtils.GetMoonRoutingPrice(level);

            if (!price.HasValue)
            {
                return null;
            }

            if (!ItemRaritiesFactor.HasValue)
            {
                ItemRaritiesFactor = JunkRadarHandler.Instance.Config.BuriedScrapsRarityPercentage;
            }

            int chanceModifier = (price.Value >= 250 ? ItemRaritiesFactor.Value * (price.Value / 250) : 0);
            int sturdyChance = ChanceForSturdyItem - chanceModifier;
            int fragileChance = ChanceForFragileItem;
            int ultraFragileChance = ChanceForUltraFragileItem + chanceModifier;

            if (sturdyChance <= 0 || ultraFragileChance >= 100)
            {
                sturdyChance = 0;
                ultraFragileChance = 70; // capped at 70 max
            }

            int selectedRandom = Random.Range(1, 101);
            string selectedItem = selectedRandom >= 1 && selectedRandom <= sturdyChance ?
                AllSturdyItemsNames[Random.Range(0, AllSturdyItemsNames.Count)] : (selectedRandom >= sturdyChance + 1 && selectedRandom <= sturdyChance + fragileChance ?
                AllFragileItemsNames[Random.Range(0, AllFragileItemsNames.Count)] :
                AllUltraFragileItemsNames[Random.Range(0, AllUltraFragileItemsNames.Count)]);

            BuriedScrapProperties properties = AllItems[selectedItem];

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
