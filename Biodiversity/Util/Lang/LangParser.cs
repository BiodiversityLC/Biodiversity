using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

namespace Biodiversity.Util.Lang;
internal class LangParser {
    internal static Dictionary<string, string> languages { get; private set; }
    internal static Dictionary<string, object> loadedLanguage { get; private set; }

    internal static void Init() {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Biodiversity.Util.Lang.defs.json");
        using StreamReader reader = new(stream);
        string result = reader.ReadToEnd();

        languages = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
    }

    internal static Dictionary<string, object> LoadLanguage(string id) {
        using Stream stream = File.Open(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lang", id + ".json"), FileMode.Open);
        using StreamReader reader = new StreamReader(stream);
        string result = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
    }

    internal static void SetLanguage(string id) {
        loadedLanguage = LoadLanguage(id);
    }

    internal static string GetTranslation(string translation) {

        if(loadedLanguage.TryGetValue(translation, out var result)) {
            return (string)result;
        }

        if(translation == "lang.missing") {
            // OHNO `lang.missing` is missing!
            BiodiversityPlugin.Logger.LogError("LANG.MISSING IS MISSING!!!!!  THIS IS BAD!! VERY BAD!!");
            return "lang.missing; <translation_id>";
        }

        return GetTranslation("lang.missing").Replace("<translation_id>", translation);
    }

    internal static JArray GetTranslationSet(string translation) {

        if(loadedLanguage.TryGetValue(translation, out var result)) {
            BiodiversityPlugin.Logger.LogInfo(result.GetType());
            return result as JArray;
        }

        return new JArray { GetTranslation("lang.missing").Replace("<translation_id>", translation) };
    }
}
