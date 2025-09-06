using Biodiversity.Util.DataStructures;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Biodiversity.Core.Lang;

internal class LangParser
{
    private const string MISSING_TRANSLATION_KEY = "lang.missing";
    private const string FALLBACK_LANGUAGE_ID = "en";

    private readonly string _missingTranslationFormat;
    private readonly Assembly _assembly;
    private readonly JObject _defaultLanguageData;
    private JObject _currentLanguageData;

    private readonly BulkPopulateDictionary<string, string> _availableLanguages;

    /// <summary>
    /// Initializes the lang parser (localization service).
    /// This constructor immediately loads the embedded fallback language (en.json)
    /// to ensure the service is always in a valid and usable state.
    /// </summary>
    /// <param name="assembly">The assembly containing the embedded language resources.</param>
    /// <exception cref="FileNotFoundException">Thrown if the essential fallback language ('en.json') is not embedded in the assembly.</exception>
    public LangParser(Assembly assembly)
    {
        _assembly = assembly;

        BiodiversityPlugin.Logger.LogInfo("--- Dumping All Embedded Resource Names ---");
        var allResourceNames = _assembly.GetManifestResourceNames();
        if (allResourceNames.Length == 0)
        {
            BiodiversityPlugin.Logger.LogWarning("Assembly contains NO embedded resources. Check file properties in your IDE.");
        }
        else
        {
            foreach (var name in allResourceNames)
            {
                BiodiversityPlugin.Logger.LogInfo($"Found resource: {name}");
            }
        }
        BiodiversityPlugin.Logger.LogInfo("-------------------------------------------");
        // =======================================================================

        _defaultLanguageData = LoadEmbeddedLanguage(FALLBACK_LANGUAGE_ID);
        if (_defaultLanguageData == null)
        {
            throw new FileNotFoundException(
                $"The fallback language resource '{GetResourceName(FALLBACK_LANGUAGE_ID)}' was not found in the assembly. Translations will not work.");
        }

        _currentLanguageData = _defaultLanguageData;
        _missingTranslationFormat = (string)_defaultLanguageData[MISSING_TRANSLATION_KEY] ?? "MISSING TRANSLATION: <translation_id>";

        _availableLanguages = new BulkPopulateDictionary<string, string>(DiscoverLanguages);
    }

    private JObject LoadEmbeddedLanguage(string languageId)
    {
        // Assumes id.json is marked as an "Embedded Resource" in the project
        string resourceName = GetResourceName(languageId);
        using Stream stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using StreamReader reader = new(stream);
        return JObject.Parse(reader.ReadToEnd());
    }

    private JObject LoadExternalLanguage(string languageId)
    {
        string directoryPath = Path.GetDirectoryName(_assembly.Location);
        if (directoryPath == null) return null;

        string filePath = Path.Combine(directoryPath, "lang", $"{languageId}.json");
        if (!File.Exists(filePath)) return null;

        string json = File.ReadAllText(filePath);
        return JObject.Parse(json);
    }

    /// <summary>
    /// Sets the active language. It will attempt to load the language from an external file.
    /// If loading fails, it logs a warning and safely falls back to the default language (English).
    /// </summary>
    /// <param name="languageId">The two-letter language code (e.g., "de", "fr").</param>
    internal void SetLanguage(string languageId)
    {
        if (string.IsNullOrWhiteSpace(languageId) || languageId.ToLower() == FALLBACK_LANGUAGE_ID)
        {
            _currentLanguageData = _defaultLanguageData;
            BiodiversityPlugin.Logger.LogInfo("Language set to English (default).");
            return;
        }

        try
        {
            JObject loadedLang = LoadExternalLanguage(languageId);
            if (loadedLang != null)
            {
                _currentLanguageData = loadedLang;
                BiodiversityPlugin.Logger.LogInfo($"Successfully loaded language: {languageId}");
            }
            else
            {
                _currentLanguageData = _defaultLanguageData;
                BiodiversityPlugin.Logger.LogWarning(
                    $"Language file for '{languageId}' not found or failed to load. Using default.");
            }
        }
        catch (Exception ex)
        {
            _currentLanguageData = _defaultLanguageData;
            BiodiversityPlugin.Logger.LogError($"An error occurred while loading language '{languageId}'. Using default. Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves a translated string for a given key.
    /// It first checks the current language, then the fallback language.
    /// If the key is not found in either, it returns a formatted "missing translation" string.
    /// </summary>
    /// <param name="key">The translation key (e.g., "item.rubberduck.name").</param>
    /// <returns>The translated string, or a formatted error string if not found.</returns>
    internal string GetTranslation(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        JToken token = _currentLanguageData[key] ?? _defaultLanguageData[key];
        if (token != null && token.Type == JTokenType.String)
        {
            return (string)token;
        }

        BiodiversityPlugin.Logger.LogWarning($"Translation key '{key}' not found or has incorrect type.");
        return _missingTranslationFormat.Replace("<translation_id>", key);
    }

    /// <summary>
    /// Retrieves a translated JSON array for a given key.
    /// Useful for lists of tips, descriptions, etc.
    /// </summary>
    /// <param name="key">The translation key.</param>
    /// <returns>A JArray if found and is of the correct type; otherwise, null.</returns>
    public JArray GetTranslationSet(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        JToken token = _currentLanguageData[key] ?? _defaultLanguageData[key];
        return token as JArray;
    }

    /// <summary>
    /// Discovers all available languages.
    /// </summary>
    /// <returns>A dictionary mapping language IDs (e.g., "en") to their native names (e.g., "English").</returns>
    private Dictionary<string, string> DiscoverLanguages()
    {
        Dictionary<string, string> discoveredLanguages = new();
        Regex langIdRegex = new(@"(\w+)\.json$");

        // 1). Scan embedded resources
        foreach (string resourceName in _assembly.GetManifestResourceNames())
        {
            if (resourceName.Contains(".Core.Lang.languages."))
            {
                Match match = langIdRegex.Match(resourceName);
                if (match.Success)
                {
                    string langId = match.Groups[1].Value;
                    if (langId != "defs") // Make sure to skip the old file if it's still there
                    {
                        JObject langData = LoadEmbeddedLanguage(langId);
                        string nativeName = (string)langData?["lang.name"] ?? langId;
                        discoveredLanguages[langId] = nativeName;
                    }
                }
            }
        }

        // 2). Scan external 'lang' folder
        string directoryPath = Path.GetDirectoryName(_assembly.Location);
        if (directoryPath != null)
        {
            string langFolderPath = Path.Combine(directoryPath, "lang");
            if (Directory.Exists(langFolderPath))
            {
                foreach (string filePath in Directory.GetFiles(langFolderPath, "*.json"))
                {
                    string langId = Path.GetFileNameWithoutExtension(filePath);
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        JObject langData = JObject.Parse(json);
                        string nativeName = (string)langData?["lang.name"] ?? langId;

                        // External files override embedded ones
                        discoveredLanguages[langId] = nativeName;
                    }
                    catch (Exception ex)
                    {
                        BiodiversityPlugin.Logger.LogWarning($"Could not parse external language file '{filePath}'. Skipping. Error: {ex.Message}");
                    }
                }
            }
        }

        return discoveredLanguages;
    }

    /// <summary>
    /// Discovers all available languages. The result is cached after the first call.
    /// </summary>
    public Dictionary<string, string> GetAvailableLanguages()
    {
        return _availableLanguages.Value;
    }

    /// <summary>
    /// Forces a re-scan of language files on the next request.
    /// </summary>
    public void ForceLanguageRediscovery()
    {
        _availableLanguages.Invalidate();
    }

    private string GetResourceName(string languageId)
    {
        return $"Biodiversity.Core.Lang.languages.{languageId}.json";
    }
}