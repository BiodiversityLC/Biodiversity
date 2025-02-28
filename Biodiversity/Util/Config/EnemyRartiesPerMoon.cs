using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using LethalLevelLoader;
using LethalLib.Modules;

namespace Biodiversity.Util.Config;

public class EnemyRaritiesPerMoon(
	int defaultRarity,
	Dictionary<Levels.LevelTypes, int> vanillaRarities = null,
	Dictionary<string, int> moddedRarities = null)
{
	public readonly Dictionary<Levels.LevelTypes, int> VanillaRarities = vanillaRarities ?? [];
	public readonly Dictionary<string, int> ModdedRarities = moddedRarities ?? [];
    
	public int DefaultRarity { get; private set; } = defaultRarity;


	public void Bind(ConfigFile file, string section) 
	{
		foreach (Levels.LevelTypes vanillaMoon in Enum.GetValues(typeof(Levels.LevelTypes))) 
		{
			if (vanillaMoon is Levels.LevelTypes.All or Levels.LevelTypes.Modded or Levels.LevelTypes.None or Levels.LevelTypes.Vanilla) continue;
			VanillaRarities[vanillaMoon] = file.Bind(
				section,
				vanillaMoon.ToString(),
				VanillaRarities.GetValueOrDefault(vanillaMoon, DefaultRarity),
				$"Rarity for '{vanillaMoon.ToString()}' (vanilla)"
			).Value;
		}

		if (Chainloader.PluginInfos.ContainsKey("imabatby.lethallevelloader")) 
		{
			BiodiversityPlugin.LogVerbose("binding modded moons from lethal level loader.");
			BindLLL(file, section);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
	private void BindLLL(ConfigFile file, string section) 
	{
		BiodiversityPlugin.LogVerbose($"{PatchedContent.ExtendedMods.Count} mods");
		BiodiversityPlugin.LogVerbose($"{string.Join(", ", PatchedContent.AllLevelSceneNames)}");

		for (int i = 0; i < PatchedContent.ExtendedMods.Count; i++)
		{
			ExtendedMod mod = PatchedContent.ExtendedMods[i];
			if (PatchedContent.VanillaMod == mod)
				continue;

			for (int j = 0; j < mod.ExtendedLevels.Count; j++)
			{
				ExtendedLevel level = mod.ExtendedLevels[j];
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