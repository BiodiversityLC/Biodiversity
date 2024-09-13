using Biodiversity.Util.Types;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Biodiversity.Util.Lang;
internal static class LangParser 
{
    internal static Dictionary<string, string> Languages { get; private set; }
    internal static NullableObject<Dictionary<string, object>> LoadedLanguage { get; private set; } = new();

    internal static void Init() 
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Biodiversity.Util.Lang.defs.json");
        using StreamReader reader = new(stream!);
        string result = reader.ReadToEnd();

        Languages = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
    }

    internal static Dictionary<string, object> LoadLanguage(string id) 
    {
        using Stream stream = File.Open(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "lang", id + ".json"), FileMode.Open);
        using StreamReader reader = new(stream);
        string result = reader.ReadToEnd();
        return JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
    }

    internal static void SetLanguage(string id)
    {
        LoadedLanguage.Value = LoadLanguage(id);
    }

    internal static string GetTranslation(string translation)
    {
        if (!LoadedLanguage.IsNotNull)
        {
            BiodiversityPlugin.Logger.LogWarning("Biodiveristy translations are missing :(");
            return translation;
        }
        
        if (LoadedLanguage.Value.TryGetValue(translation, out object result))  return (string)result;

        if (translation == "lang.missing") 
        {
            // OHNO `lang.missing` is missing!
            BiodiversityPlugin.Logger.LogError("LANG.MISSING IS MISSING!!!!!  THIS IS BAD!! VERY BAD!!");
            return "lang.missing; <translation_id>";
        }

        return GetTranslation("lang.missing").Replace("<translation_id>", translation);
    }

    internal static JArray GetTranslationSet(string translation) 
    {
        if (LoadedLanguage.Value.TryGetValue(translation, out object result)) 
        {
            BiodiversityPlugin.Logger.LogInfo(result.GetType());
            return result as JArray;
        }

        return [GetTranslation("lang.missing").Replace("<translation_id>", translation)];
    }
}
