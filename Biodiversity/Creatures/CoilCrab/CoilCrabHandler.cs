using Biodiversity.Util;
using System.Collections.Generic;

namespace Biodiversity.Creatures.CoilCrab;

internal class CoilCrabHandler : BiodiverseAIHandler<CoilCrabHandler>
{
    internal CoilCrabAssets Assets { get; private set; }

    internal CoilCrabConfig Config { get; private set; }

    internal Dictionary<string, int> StormyWeights { get; private set; }

    internal Dictionary<string, int> Weights { get; private set; }

    public CoilCrabHandler()
    {
        Assets = new CoilCrabAssets("biodiversity_coilcrab");

        Config = new CoilCrabConfig(BiodiversityPlugin.Instance.CreateConfig("coilcrab"));

        StormyWeights = new Dictionary<string, int>();
        Weights = new Dictionary<string, int>();

        Assets.CoilCrabEnemy.PowerLevel = Config.PowerLevel;
        Assets.CoilCrabEnemy.MaxCount = Config.MaxSpawns;

        ParseWeights();

        LethalLibUtils.RegisterScrapWithRuntimeIconSupport(Assets.CoilShellItem, new Dictionary<LethalLib.Modules.Levels.LevelTypes, int> {{LethalLib.Modules.Levels.LevelTypes.All, 0}}, new Dictionary<string, int> { });

        LethalLibUtils.TranslateTerminalNode(Assets.CoilCrabTerminalNode);
        LethalLibUtils.RegisterEnemyWithConfig(
            Config.EnableCoilCrab,
            Config.CoilCrabRarity,
            Assets.CoilCrabEnemy,
            Assets.CoilCrabTerminalNode,
            Assets.CoilCrabTerminalKeyword);
    }

    private void ParseWeights()
    {
        string[] stormyConfig = Config.CoilCrabRarityStormy.Split(',');
        foreach (string moonConfig in stormyConfig)
        {
            //BiodiversityPlugin.LogVerbose(moonConfig);
            string[] splitMoonConfig = moonConfig.Split(":");

            if (splitMoonConfig.Length != 2)
            {
                BiodiversityPlugin.LogVerbose($"Coil crab parser couldn't parse {moonConfig}");
                continue;
            }

            string name = splitMoonConfig[0];

            //BiodiversityPlugin.LogVerbose(name);

            int weight;

            try
            {
                weight = int.Parse(splitMoonConfig[1]);
            }
            catch
            {
                BiodiversityPlugin.LogVerbose($"Coil crab parser couldn't find a weight for moon: {name}");
                weight = 0;
            }

            StormyWeights.Add(name, weight);
        }


        string[] config = Config.CoilCrabRarity.Split(',');
        foreach (string moonConfig in config)
        {
            string[] splitMoonConfig = moonConfig.Split(":");

            if (splitMoonConfig.Length != 2)
            {
                BiodiversityPlugin.LogVerbose($"Coil crab parser couldn't parse {moonConfig}");
                continue;
            }

            string name = splitMoonConfig[0];

            int weight;
            try
            {
                weight = int.Parse(splitMoonConfig[1]);
            }
            catch
            {
                BiodiversityPlugin.LogVerbose($"Coil crab parser couldn't find a weight for moon: {name}");
                weight = 0;
            }

            Weights.Add(name, weight);
        }

        List<int> wk = new List<int>(Weights.Values);

        List<int> swk = new List<int>(StormyWeights.Values);

        //BiodiversityPlugin.LogVerbose(wk[0]);
        //BiodiversityPlugin.LogVerbose(swk[0]);
    }
}