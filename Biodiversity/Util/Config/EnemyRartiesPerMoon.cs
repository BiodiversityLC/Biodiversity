using BepInEx.Bootstrap;
using BepInEx.Configuration;
using LethalLevelLoader;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Biodiversity.Util.Config;

public class EnemyRaritiesPerMoon(
	int defaultRarity,
	Dictionary<Levels.LevelTypes, int> vanillaRarities = null,
	Dictionary<string, int> moddedRarities = null)
{
	private readonly Dictionary<Levels.LevelTypes, int> _vanillaRarities = vanillaRarities ?? [];
	private readonly Dictionary<string, int> _moddedRarities = moddedRarities ?? [];

	private int DefaultRarity { get; set; } = defaultRarity;


	public void Bind(ConfigFile file, string section) {
		foreach(Levels.LevelTypes vanillaMoon in Enum.GetValues(typeof(Levels.LevelTypes))) {
			if(vanillaMoon is Levels.LevelTypes.All or Levels.LevelTypes.Modded or Levels.LevelTypes.None or Levels.LevelTypes.Vanilla) continue;
			_vanillaRarities[vanillaMoon] = file.Bind(
				section,
				vanillaMoon.ToString(),
				_vanillaRarities.GetValueOrDefault(vanillaMoon, DefaultRarity),
				$"Rarity for '{vanillaMoon.ToString()}' (vanilla)"
			).Value;
		}

		if (Chainloader.PluginInfos.ContainsKey("imabatby.lethallevelloader")) {
			BiodiversityPlugin.Logger.LogDebug("binding modded moons from lethal level loader.");
			BindLLL(file, section);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	private void BindLLL(ConfigFile file, string section) {
		BiodiversityPlugin.Logger.LogDebug($"{PatchedContent.ExtendedMods.Count} mods");
		BiodiversityPlugin.Logger.LogDebug($"{string.Join(", ", PatchedContent.AllLevelSceneNames)}");

		foreach (ExtendedMod mod in PatchedContent.ExtendedMods) {
			if (PatchedContent.VanillaMod == mod) {
				continue;
			}
			
			foreach(ExtendedLevel level in mod.ExtendedLevels) {
				string name = level.NumberlessPlanetName;
				_moddedRarities[name] = file.Bind(
					section,
					name,
					_moddedRarities.GetValueOrDefault(name, DefaultRarity),
					$"Rarity for '{name}' (modded)"
				).Value;
			}
		}
	}
}