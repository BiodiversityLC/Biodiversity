using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Biodiversity.Lang;

internal static class LangParser
{
    internal static Dictionary<string, string> Languages { get; private set; }
    private static Dictionary<string, object> LoadedLanguage { get; set; }

    private const string LangMissing = "lang.missing";

    internal static void Init()
    {
        const string defsJsonFilename = "Biodiversity.Lang.defs.json";
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
    
    internal static string GetTranslation(string translationId)
    {
        if (LoadedLanguage == null)
        {
            if (translationId == LangMissing) return LangMissing;
            BiodiversityPlugin.Logger.LogDebug($"Cannot translate the message {translationId} due to translations not being loaded.");
            return GetTranslation(LangMissing).Replace("<translation_id>", translationId);
        }
         
        if (LoadedLanguage.TryGetValue(translationId, out object result)) return result.ToString();
        if (translationId == LangMissing)
        {
            BiodiversityPlugin.Logger.LogError($"{LangMissing} is missing.");
            return $"{LangMissing}; <translation_id>";
        }

        return GetTranslation($"{LangMissing}").Replace("<translation_id>", translationId);
    }

    internal static JArray GetTranslationSet(string translation) 
    {
        if (LoadedLanguage == null)
        {
            BiodiversityPlugin.Logger.LogDebug($"Cannot translate message due to translations not being loaded: {translation}");
            return [GetTranslation(LangMissing).Replace("<translation_id>", translation)];
        }
        
        if (LoadedLanguage.TryGetValue(translation, out object result)) 
        {
            BiodiversityPlugin.Logger.LogInfo(result.GetType());
            return result as JArray;
        }

        return [GetTranslation(LangMissing).Replace("<translation_id>", translation)];
    }
}
