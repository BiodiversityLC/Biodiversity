using JetBrains.Annotations;
using Biodiversity.Creatures;
using Biodiversity.Items.DeveloperItems;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.Config;
using System.Reflection;
using UnityEngine;

namespace Biodiversity.Items.Developeritems;

[UsedImplicitly]
internal class DeveloperScrapHandler : BiodiverseAIHandler<DeveloperScrapHandler>
{
    internal DeveloperScrapAssets Assets { get; set; }
    internal DeveloperScrapConfig Config { get; set; }

    public DeveloperScrapHandler()
    {
        Assets = new DeveloperScrapAssets("devitems");
        Config = new DeveloperScrapConfig(BiodiversityPlugin.Instance.CreateConfig("developer_scrap_items"));
        
        foreach (FieldInfo field in typeof(DeveloperScrapAssets).GetFields())
        {
            LoadFromBundleAttribute loadFromBundleAttribute = field.GetCustomAttribute<LoadFromBundleAttribute>();
            if (loadFromBundleAttribute == null) continue;

            foreach (PropertyInfo property in typeof(DeveloperScrapConfig).GetProperties())
            {
                if (property.PropertyType == typeof(GenericScrapItem))
                {
                    GenericScrapItem scrapItem = (GenericScrapItem)property.GetValue(Config);
                    if (scrapItem.AssetName == loadFromBundleAttribute.BundleFile)
                    {
                        Item item = (Item)field.GetValue(Assets);

                        item.isScrap = true;
                        item.weight = Mathf.Max(0, Mathf.Ceil(scrapItem.Weight / 105 + 1));
                        item.minValue = scrapItem.MinimumValue;
                        item.maxValue = scrapItem.MaximumValue;
                        
                        RegisterScrapWithConfig(scrapItem.Rarity, item);
                    }
                }
            }
        }
    }
}