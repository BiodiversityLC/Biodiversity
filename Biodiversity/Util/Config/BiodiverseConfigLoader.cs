using BepInEx.Configuration;
using Biodiversity.Patches;
using Biodiversity.Util.Assetloading;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Timeline;

namespace Biodiversity.Util.Config;
[Serializable]
public abstract class BiodiverseConfigLoader<T> where T : BiodiverseConfigLoader<T> {
    public BiodiverseConfigLoader(ConfigFile configFile) {
        string CurrentHeader = "Misc";
        Type type = typeof(T);
        configFile.SaveOnConfigSet = false;
        foreach(PropertyInfo property in type.GetProperties()) {
            try {
                FieldInfo backingField = property.DeclaringType.GetField($"<{property.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

                if(backingField.GetCustomAttribute(typeof(NonSerializedAttribute)) != null) continue;
                
                HeaderAttribute headerAttribute = (HeaderAttribute)backingField.GetCustomAttribute(typeof(HeaderAttribute));
                if (headerAttribute != null) {
                    CurrentHeader = headerAttribute.header.Replace(" ", "");
                }

                string description = "This config option hasn't used [Tooltip] to set a description, so this default one will be here instead.";
                TooltipAttribute tooltipAttribute = (TooltipAttribute)backingField.GetCustomAttribute(typeof(TooltipAttribute));
                if (tooltipAttribute != null) {
                    description = tooltipAttribute.tooltip;
                }


                ConfigDescription configDescription = new ConfigDescription(description);
                RangeAttribute rangeAttribute = (RangeAttribute)backingField.GetCustomAttribute(typeof(RangeAttribute));
                if (rangeAttribute != null) {
                    if (property.PropertyType == typeof(int)) {
                        configDescription = new ConfigDescription(description, new AcceptableValueRange<int>((int)rangeAttribute.min, (int)rangeAttribute.max));
                    } else {
                        configDescription = new ConfigDescription(description, new AcceptableValueRange<float>(rangeAttribute.min, rangeAttribute.max));
                    }
                } else {
                    configDescription = new ConfigDescription(description);
                }

                // this is icky
                if (property.PropertyType == typeof(float)) {
                    property.SetValue(this, configFile.Bind(CurrentHeader, property.Name, (float)property.GetValue(this), configDescription).Value);
                }

                if (property.PropertyType == typeof(int)) {
                    property.SetValue(this, configFile.Bind(CurrentHeader, property.Name, (int)property.GetValue(this), configDescription).Value);
                }

                if (property.PropertyType == typeof(string)) {
                    property.SetValue(this, configFile.Bind(CurrentHeader, property.Name, (string)property.GetValue(this), configDescription).Value);
                }

                if (property.PropertyType == typeof(bool)) {
                    property.SetValue(this, configFile.Bind(CurrentHeader, property.Name, (bool)property.GetValue(this), configDescription).Value);
                }
            } catch (Exception ex) {
                BiodiversityPlugin.Logger.LogError($"Exception while binding: {property.Name}");
                BiodiversityPlugin.Logger.LogError(ex.ToString());
            }

            if (property.PropertyType == typeof(EnemyRaritiesPerMoon)) {
                EnemyRaritiesPerMoon rarities = (EnemyRaritiesPerMoon) property.GetValue(this);
                rarities.Bind(configFile, property.Name);
                property.SetValue(this, rarities);
            }
        }

        configFile.SaveOnConfigSet = true;
        configFile.Save();
    }
}
