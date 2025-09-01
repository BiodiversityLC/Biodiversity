using Biodiversity.Core.Attributes;
using Biodiversity.Core.Config;
using JetBrains.Annotations;
using Biodiversity.Items.DeveloperItems;
using Biodiversity.Util;
using System.Reflection;

namespace Biodiversity.Items.Developeritems;

[UsedImplicitly]
internal class DeveloperScrapHandler : BiodiverseItemHandler<DeveloperScrapHandler>
{
    internal DeveloperScrapAssets Assets { get; set; }
    internal DeveloperScrapConfig Config { get; set; }

    public DeveloperScrapHandler()
    {
        Assets = new DeveloperScrapAssets("developer_items");
        Config = new DeveloperScrapConfig(BiodiversityPlugin.Instance.CreateConfig("developer_scrap_items"));

        for (int i = 0; i < typeof(DeveloperScrapAssets).GetFields().Length; i++)
        {
            FieldInfo field = typeof(DeveloperScrapAssets).GetFields()[i];
            LoadFromBundleAttribute loadFromBundleAttribute = field.GetCustomAttribute<LoadFromBundleAttribute>();
            if (loadFromBundleAttribute == null) continue;

            // Registers items that are `GenericScrapItem`s
            for (int j = 0; j < typeof(DeveloperScrapConfig).GetProperties().Length; j++)
            {
                PropertyInfo property = typeof(DeveloperScrapConfig).GetProperties()[j];
                if (property.PropertyType == typeof(GenericScrapItem))
                {
                    GenericScrapItem scrapItem = (GenericScrapItem)property.GetValue(Config);
                    if (scrapItem.AssetName != loadFromBundleAttribute.BundleFile) continue;

                    Item item = (Item)field.GetValue(Assets);
                    if (!item) continue;
                    
                    item.isScrap = true;
                    item.weight = scrapItem.Weight;
                    item.minValue = scrapItem.MinimumValue;
                    item.maxValue = scrapItem.MaximumValue;

                    LethalLibUtils.RegisterScrapWithConfig(scrapItem.Rarity, item);
                }
            }
        }
    }
}