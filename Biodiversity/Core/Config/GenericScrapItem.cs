using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Biodiversity.Core.Config;

public class GenericScrapItem
{
    private abstract class CustomSettingDefinition 
    {
        public string Name { get; }
        public string Description { get; }
        
        protected CustomSettingDefinition(string name, string description) 
        {
            Name = name;
            Description = description;
        }
        
        public abstract void Bind(ConfigFile file, string section);
    }
    
    private class CustomSettingDefinition<T> : CustomSettingDefinition
    {
        public T DefaultValue { get; }
        public AcceptableValueBase AcceptableValues { get; }
        public ConfigEntry<T> BoundEntry { get; private set; }

        public CustomSettingDefinition(string name, T defaultValue, string description, AcceptableValueBase acceptableValues = null)
            : base(name, description)
        {
            DefaultValue = defaultValue;
            AcceptableValues = acceptableValues;
        }

        public override void Bind(ConfigFile file, string section)
        {
            BoundEntry = file.Bind(section, Name, DefaultValue, new ConfigDescription(Description, AcceptableValues));
        }
    }
    
    public string AssetName { get; private set; }
    public string ItemName { get; private set; }
    public string Rarity { get; private set; }
    public float Weight { get; private set; }
    public int MinimumValue { get; private set; }
    public int MaximumValue { get; private set; }

    private readonly List<CustomSettingDefinition> _customSettingDefinitions = [];
    private readonly Dictionary<string, CustomSettingDefinition> _boundCustomSettings = new();

    private static readonly AcceptableValueRange<float> WeightRange = new(1f, 4f);
    private static readonly AcceptableValueRange<int> MinimumValueRange = new(0, 1000);
    private static readonly AcceptableValueRange<int> MaximumValueRange = new(0, 1000);
    
    private const string RarityTooltip = "Spawn weight (rarity) of the {0} scrap.";
    private const string WeightTooltip = "Weight of the {0} scrap.";
    private const string MinimumValueTooltip = "Minimum value that the {0} scrap can spawn with.";
    private const string MaximumValueTooltip = "Maximum value that the {0} scrap can spawn with.";

    public GenericScrapItem(
        string assetName,
        string itemName,
        string rarity = "All:2",
        float weight = 1f,
        int minimumValue = 20,
        int maximumValue = 60)
    {
        AssetName = assetName;
        ItemName = itemName;
        Rarity = rarity;
        Weight = weight;
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
    }
    
    public GenericScrapItem WithCustomSetting<T>(string name, T defaultValue, string description)
    {
        _customSettingDefinitions.Add(new CustomSettingDefinition<T>(name, defaultValue, description));
        return this; // Return 'this' to allow chaining
    }

    public GenericScrapItem WithCustomSetting<T>(string name, T defaultValue, string description, AcceptableValueRange<T> range) where T : IComparable
    {
        _customSettingDefinitions.Add(new CustomSettingDefinition<T>(name, defaultValue, description, range));
        return this;
    }

    public void Bind(ConfigFile file, string section)
    {
        Rarity = file.Bind(section, "Rarity", Rarity,
            new ConfigDescription(string.Format(RarityTooltip, ItemName))).Value;

        Weight = file.Bind(section, "Weight", Weight,
            new ConfigDescription(string.Format(WeightTooltip, ItemName), WeightRange)).Value;
        
        MinimumValue = file.Bind(section, "Minimum Value", MinimumValue,
            new ConfigDescription(string.Format(MinimumValueTooltip, ItemName), MinimumValueRange)).Value;
        
        MaximumValue = file.Bind(section, "Maximum Value", MaximumValue,
            new ConfigDescription(string.Format(MaximumValueTooltip, ItemName), MaximumValueRange)).Value;

        for (int i = 0; i < _customSettingDefinitions.Count; i++)
        {
            CustomSettingDefinition definition = _customSettingDefinitions[i];
            
            definition.Bind(file, section);
            _boundCustomSettings[definition.Name] = definition;
        }
    }
    
    public T Get<T>(string name)
    {
        if (_boundCustomSettings.TryGetValue(name, out CustomSettingDefinition setting))
        {
            if (setting is CustomSettingDefinition<T> typedSetting && typedSetting.BoundEntry != null)
            {
                return typedSetting.BoundEntry.Value;
            }
            
            throw new InvalidCastException($"Custom setting '{name}' is not of type {typeof(T).Name}.");
        }
        
        throw new KeyNotFoundException($"Custom setting with the name '{name}' was not found for item '{ItemName}'.");
    }

    private static string CleanConfigString(string str)
    {
        const string pattern = @"[\n\t\\\""'\[\]]";
        return Regex.Replace(str, pattern, "");
    }
}