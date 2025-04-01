using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Biodiversity.Util.Lang;
internal static class LangParser
{
    internal static Dictionary<string, string> Languages { get; private set; }
    private static Dictionary<string, object> LoadedLanguage { get; set; }

    internal static void Init()
    {
        const string defsJsonFilename = "Biodiversity.Util.Lang.defs.json";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(defsJsonFilename);
        if (stream == null)
        {
            BiodiversityPlugin.Logger.LogWarning($"Could not find {defsJsonFilename}, and therefore cannot do translations.");
            return;
        }
        using StreamReader reader = new(stream);
        string result = reader.ReadToEnd();

        Languages = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
    }

    private static Dictionary<string, object> LoadLanguage(string id)
    {
        string directoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (directoryPath == null)
        {
            BiodiversityPlugin.Logger.LogError("Cannot determine the assembly directory path, and therefore cannot do translations.");
            return null;
        }
        
        using Stream stream = File.Open(Path.Combine(directoryPath, "lang", id + ".json"), FileMode.Open);
        using StreamReader reader = new(stream);
        string result = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
    }

    internal static void SetLanguage(string id)
    {
        LoadedLanguage = LoadLanguage(id);
    }

    internal static string GetTranslation(string translation)
    {
        if (LoadedLanguage == null)
        {
            BiodiversityPlugin.Logger.LogDebug($"Cannot translate message due to translations not being loaded: {translation}");
            return GetTranslation("lang.missing").Replace("<translation_id>", translation);
        }
        
        if (LoadedLanguage.TryGetValue(translation, out object result)) return (string)result;
        if (translation == "lang.missing") 
        {
            BiodiversityPlugin.Logger.LogError("LANG.MISSING IS MISSING!!!!!  THIS IS BAD!! VERY BAD!!");
            return "lang.missing; <translation_id>";
        }

        return GetTranslation("lang.missing").Replace("<translation_id>", translation);
    }

    internal static JArray GetTranslationSet(string translation) 
    {
        if (LoadedLanguage == null)
        {
            BiodiversityPlugin.Logger.LogDebug($"Cannot translate message due to translations not being loaded: {translation}");
            return [GetTranslation("lang.missing").Replace("<translation_id>", translation)];
        }
        
        if (LoadedLanguage.TryGetValue(translation, out object result)) 
        {
            BiodiversityPlugin.Logger.LogInfo(result.GetType());
            return result as JArray;
        }

        return [GetTranslation("lang.missing").Replace("<translation_id>", translation)];
    }
}
