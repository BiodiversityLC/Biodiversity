using Biodiversity.Util;
using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

namespace Biodiversity.Creatures.Ogopogo;

[UsedImplicitly]
internal class OgopogoHandler : BiodiverseAIHandler<OgopogoHandler> {
	internal OgopogoAssets Assets { get; private set; }

	internal OgopogoConfig Config { get; private set; }

	public static Dictionary<string, List<Vector3>> LevelsParsedStaticSpawns { get; private set; } = new Dictionary<string, List<Vector3>>();


    public OgopogoHandler() {
		Assets = new OgopogoAssets("biodiversity_ogopogo");
		Config = new OgopogoConfig(BiodiversityPlugin.Instance.CreateConfig("ogopogo"));
		
		if (Config.DetectionRange >= Config.LoseRange)
		{
			BiodiversityPlugin.Logger.LogInfo("Ogopogo detection range lower than lose range. Disabling Ogopogo spawning until this is fixed.");
		}
		else if (Config.AttackDistance > Config.DetectionRange)
		{
			BiodiversityPlugin.Logger.LogInfo("Ogopogo attack distance is not in the detection distance. Disabling Ogopogo spawning until this is fixed.");
		} 
		else 
		{
			parseStaticSpawns(Config.OgopogoStaticSpawns);
            LethalLibUtils.TranslateTerminalNode(Assets.OgopogoTerminalNode);
			LethalLibUtils.RegisterEnemyWithConfig(
				Config.OgopogoEnabled,
				Config.OgopogoRarity,
				Assets.OgopogoEnemyType,
				Assets.OgopogoTerminalNode,
				Assets.OgopogoTerminalKeyword);
			
		}
		
		LethalLibUtils.TranslateTerminalNode(Assets.VerminTerminalNode);
		LethalLibUtils.RegisterEnemyWithConfig(
			Config.EnableVermin,
			Config.VerminRarity,
			Assets.VerminEnemyType,
			Assets.VerminTerminalNode,
			Assets.VerminTerminalKeyword);
	}

	private void parseStaticSpawns(string spawns)
	{
		string[] levels = spawns.Split(';');

		foreach (string level in levels)
		{
			string[] parts = level.Split(':');
			if (parts.Length != 2)
			{
				BiodiversityPlugin.Logger.LogError("Level definitions for Ogopogo static spawns are not defined correctly.");
				LevelsParsedStaticSpawns.Clear();
				return;
            }
			string levelName = parts[0];
			string[] positions = parts[1].Split('/');

			BiodiversityPlugin.LogVerbose($"{level}");


            foreach (string position in positions)
			{
				string[] coords = position.Replace("(", "").Replace(")", "").Split(",");
				if (coords.Length != 3)
                {
                    BiodiversityPlugin.Logger.LogError($"Coordinate definitions for Ogopogo static spawns on level {levelName} are not defined correctly.");
                    LevelsParsedStaticSpawns.Clear();
                    return;
                }

                BiodiversityPlugin.LogVerbose($"{position}");

                if (float.TryParse(coords[0], out float x) && float.TryParse(coords[1], out float y) && float.TryParse(coords[2], out float z))
				{
                    if (!LevelsParsedStaticSpawns.ContainsKey(levelName))
                    {
                        LevelsParsedStaticSpawns.Add(levelName, new List<Vector3>());
                    }
                    LevelsParsedStaticSpawns[levelName].Add(new Vector3(x, y, z));
				}
			}
        }
    }
}