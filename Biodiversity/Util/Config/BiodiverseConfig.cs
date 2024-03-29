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
public abstract class BiodiverseConfig<T> where T : BiodiverseConfig<T> {
    public BiodiverseConfig(ConfigFile configFile) {
        string CurrentHeader = "Misc";
        Type type = typeof(T);
        foreach(FieldInfo field in type.GetFields()) {
            HeaderAttribute headerAttribute = (HeaderAttribute)field.GetCustomAttribute(typeof(HeaderAttribute));
            if(headerAttribute != null) {
                CurrentHeader = headerAttribute.header.Replace(" ","");
            }
            string description = "This config option hasn't used [Tooltip] to set a description, so this default one will be here instead.";
            TooltipAttribute tooltipAttribute = (TooltipAttribute)field.GetCustomAttribute(typeof(TooltipAttribute));
            if(tooltipAttribute != null) {
                description = tooltipAttribute.tooltip;
            }

            ConfigDescription configDescription;
            RangeAttribute rangeAttribute = (RangeAttribute)field.GetCustomAttribute(typeof(RangeAttribute));
            if(rangeAttribute != null) {
                if(field.FieldType == typeof(int)) {
                    configDescription = new ConfigDescription(description, new AcceptableValueRange<int>((int)rangeAttribute.min, (int)rangeAttribute.max));
                } else {
                    configDescription = new ConfigDescription(description, new AcceptableValueRange<float>(rangeAttribute.min, rangeAttribute.max));
                }
            } else {
                configDescription = new ConfigDescription(description);
            }

            if(field.FieldType == typeof(float)) {
                field.SetValue(this, configFile.Bind(CurrentHeader, field.Name, (float)field.GetValue(this), configDescription).Value);
            }
            if(field.FieldType == typeof(int)) {
                field.SetValue(this, configFile.Bind(CurrentHeader, field.Name, (int)field.GetValue(this), configDescription).Value);
            }
            if(field.FieldType == typeof(string)) {
                field.SetValue(this, configFile.Bind(CurrentHeader, field.Name, (string)field.GetValue(this), configDescription).Value);
            }
        }

    }
}
