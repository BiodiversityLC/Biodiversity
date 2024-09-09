using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using JetBrains.Annotations;
using LethalLevelLoader;
using LethalLib.Modules;

namespace Biodiversity.Util.Config;

public class EnemyRaritiesPerMoon {
	public Dictionary<Levels.LevelTypes, int> VanillaRarities;
	public Dictionary<string, int> ModdedRarities;
    
	public int DefaultRarity { get; private set; }
	
	public EnemyRaritiesPerMoon(int defaultRarity, Dictionary<Levels.LevelTypes, int> VanillaRarities = null, Dictionary<string, int> ModdedRarities = null) {
		this.DefaultRarity = defaultRarity;
		if (VanillaRarities != null) this.VanillaRarities = VanillaRarities;
		else this.VanillaRarities = [];
		if (ModdedRarities != null) this.ModdedRarities = ModdedRarities;
		else this.ModdedRarities = [];
	}

    
	public void Bind(ConfigFile file, string section) {
		foreach(Levels.LevelTypes vanillaMoon in Enum.GetValues(typeof(Levels.LevelTypes))) {
			if(vanillaMoon is Levels.LevelTypes.All or Levels.LevelTypes.Modded or Levels.LevelTypes.None or Levels.LevelTypes.Vanilla) continue;
			VanillaRarities[vanillaMoon] = file.Bind(
				section,
				vanillaMoon.ToString(),
				VanillaRarities.GetValueOrDefault(vanillaMoon, DefaultRarity),
				$"Rarity for '{vanillaMoon.ToString()}' (vanilla)"
			).Value;
		}

		if (Chainloader.PluginInfos.ContainsKey("imabatby.lethallevelloader")) {
			BiodiversityPlugin.Logger.LogDebug("binding modded moons from lethal level loader.");
			BindLLL(file, section);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	void BindLLL(ConfigFile file, string section) {
		BiodiversityPlugin.Logger.LogDebug($"{PatchedContent.ExtendedMods.Count} mods");
		BiodiversityPlugin.Logger.LogDebug($"{string.Join(", ", PatchedContent.AllLevelSceneNames)}");

		foreach (ExtendedMod mod in PatchedContent.ExtendedMods) {
			if (PatchedContent.VanillaMod == mod) {
				continue;
			}
			
			foreach(ExtendedLevel level in mod.ExtendedLevels) {
				string name = level.NumberlessPlanetName;
				ModdedRarities[name] = file.Bind(
					section,
					name,
					ModdedRarities.GetValueOrDefault(name, DefaultRarity),
					$"Rarity for '{name}' (modded)"
				).Value;
			}
		}
	}
}