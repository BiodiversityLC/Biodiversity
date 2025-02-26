using Biodiversity.Creatures.MicBird;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using UnityEngine.ProBuilder;

namespace Biodiversity.Creatures.CoilCrab
{
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

            ParseWeights();

            LethalLib.Modules.Items.RegisterScrap(Assets.CoilShellItem, 0, LethalLib.Modules.Levels.LevelTypes.None);

            TranslateTerminalNode(Assets.CoilCrabTerminalNode);
            RegisterEnemyWithConfig(
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
                BiodiversityPlugin.Logger.LogInfo(moonConfig);
                string[] splitMoonConfig = moonConfig.Split(":");

                if (splitMoonConfig.Length != 2)
                {
                    BiodiversityPlugin.Logger.LogInfo($"Coil crab parser couldn't parse {moonConfig}");
                    continue;
                }

                string name = splitMoonConfig[0];

                BiodiversityPlugin.Logger.LogInfo(name);

                int weight;

                try
                {
                    weight = int.Parse(splitMoonConfig[1]);
                }
                catch
                {
                    BiodiversityPlugin.Logger.LogInfo($"Coil crab parser couldn't find a weight for moon: {name}");
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
                    BiodiversityPlugin.Logger.LogInfo($"Coil crab parser couldn't parse {moonConfig}");
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
                    BiodiversityPlugin.Logger.LogInfo($"Coil crab parser couldn't find a weight for moon: {name}");
                    weight = 0;
                }

                Weights.Add(name, weight);
            }

            List<int> wk = new List<int>(Weights.Values);

            List<int> swk = new List<int>(StormyWeights.Values);

            BiodiversityPlugin.Logger.LogInfo(wk[0]);
            BiodiversityPlugin.Logger.LogInfo(swk[0]);
        }
    }
}
