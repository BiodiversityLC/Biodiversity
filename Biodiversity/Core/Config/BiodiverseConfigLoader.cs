using BepInEx.Configuration;
using System;
using System.Reflection;
using UnityEngine;

namespace Biodiversity.Core.Config;

[Serializable]
public abstract class BiodiverseConfigLoader<T> where T : BiodiverseConfigLoader<T>
{
    protected BiodiverseConfigLoader(ConfigFile configFile)
    {
        string currentHeader = "Misc";
        Type type = typeof(T);
        configFile.SaveOnConfigSet = false;

        for (int i = 0; i < type.GetProperties().Length; i++)
        {
            PropertyInfo property = type.GetProperties()[i];
            try
            {
                FieldInfo backingField = property.DeclaringType.GetField($"<{property.Name}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (backingField.GetCustomAttribute(typeof(NonSerializedAttribute)) != null) continue;

                HeaderAttribute headerAttribute = (HeaderAttribute)backingField.GetCustomAttribute(typeof(HeaderAttribute));
                if (headerAttribute != null)
                {
                    currentHeader = headerAttribute.header.Replace(" ", "");
                }

                string description = "This config option hasn't used [Tooltip] to set a description, so this default one will be here instead.";
                TooltipAttribute tooltipAttribute = (TooltipAttribute)backingField.GetCustomAttribute(typeof(TooltipAttribute));
                if (tooltipAttribute != null)
                {
                    description = tooltipAttribute.tooltip;
                }

                ConfigDescription configDescription;
                RangeAttribute rangeAttribute = (RangeAttribute)backingField.GetCustomAttribute(typeof(RangeAttribute));
                if (rangeAttribute != null)
                {
                    configDescription = property.PropertyType == typeof(int)
                        ? new ConfigDescription(description,
                            new AcceptableValueRange<int>((int)rangeAttribute.min, (int)rangeAttribute.max))
                        : new ConfigDescription(description,
                            new AcceptableValueRange<float>(rangeAttribute.min, rangeAttribute.max));
                }
                else
                {
                    configDescription = new ConfigDescription(description);
                }

                if (property.PropertyType == typeof(float))
                {
                    property.SetValue(this,
                        configFile.Bind(currentHeader, property.Name, (float)property.GetValue(this), configDescription)
                            .Value);
                }

                if (property.PropertyType == typeof(int))
                {
                    property.SetValue(this,
                        configFile.Bind(currentHeader, property.Name, (int)property.GetValue(this), configDescription)
                            .Value);
                }

                if (property.PropertyType == typeof(string))
                {
                    property.SetValue(this,
                        configFile.Bind(currentHeader, property.Name, (string)property.GetValue(this),
                            configDescription).Value);
                }

                if (property.PropertyType == typeof(bool))
                {
                    property.SetValue(this,
                        configFile.Bind(currentHeader, property.Name, (bool)property.GetValue(this), configDescription)
                            .Value);
                }
            }
            catch (Exception ex)
            {
                BiodiversityPlugin.Logger.LogError($"Exception while binding: {property.Name}");
                BiodiversityPlugin.Logger.LogError(ex.ToString());
            }

            if (property.PropertyType == typeof(EnemyRaritiesPerMoon))
            {
                EnemyRaritiesPerMoon rarities = (EnemyRaritiesPerMoon)property.GetValue(this);
                rarities.Bind(configFile, property.Name);
                property.SetValue(this, rarities);
            }
            else if (property.PropertyType == typeof(GenericScrapItem))
            {
                GenericScrapItem scrapSettings = (GenericScrapItem)property.GetValue(this);
                scrapSettings.Bind(configFile, property.Name);
                property.SetValue(this, scrapSettings);
            }
        }

        configFile.SaveOnConfigSet = true;
        configFile.Save();
    }
}