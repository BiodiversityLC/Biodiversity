using BepInEx.Configuration;
using System.Text.RegularExpressions;

namespace Biodiversity.Util.Config;

public struct GenericScrapItem(
    string assetName,
    string itemName,
    string rarity = "All:2",
    float weight = 1f,
    int minimumValue = 20,
    int maximumValue = 60)
{
    private const string RarityTooltip = "Spawn weight (rarity) of the {0} scrap.";
    private const string WeightTooltip = "Weight of the {0} scrap.";
    private const string MinimumValueTooltip = "Minimum value that the {0} scrap can spawn with.";
    private const string MaximumValueTooltip = "Maximum value that the {0} scrap can spawn with.";

    public string AssetName { get; private set; } = assetName;
    public string ItemName { get; private set; } = itemName;
    public string Rarity { get; private set; } = rarity;
    public float Weight { get; private set; } = weight;
    public int MinimumValue { get; private set; } = minimumValue;
    public int MaximumValue { get; private set; } = maximumValue;

    private static readonly AcceptableValueRange<float> WeightRange = new(0f, 1000f);
    private static readonly AcceptableValueRange<int> MinimumValueRange = new(0, 1000);
    private static readonly AcceptableValueRange<int> MaximumValueRange = new(0, 1000);

    public void Bind(ConfigFile file, string section)
    {
        Rarity = file.Bind(section, CleanConfigString($"{ItemName} Rarity"), Rarity,
            new ConfigDescription(string.Format(RarityTooltip, ItemName))).Value;

        Weight = file.Bind(section, CleanConfigString($"{ItemName} Weight"), Weight,
            new ConfigDescription(string.Format(WeightTooltip, ItemName), WeightRange)).Value;
        
        MinimumValue = file.Bind(section, CleanConfigString($"{ItemName} Minimum Value"), MinimumValue,
            new ConfigDescription(string.Format(MinimumValueTooltip, ItemName), MinimumValueRange)).Value;
        
        MaximumValue = file.Bind(section, CleanConfigString($"{ItemName} Maximum Value"), MaximumValue,
            new ConfigDescription(string.Format(MaximumValueTooltip, ItemName), MaximumValueRange)).Value;
    }

    private static string CleanConfigString(string str)
    {
        const string pattern = @"[\n\t\\\""'\[\]]";
        return Regex.Replace(str, pattern, "");
    }
}