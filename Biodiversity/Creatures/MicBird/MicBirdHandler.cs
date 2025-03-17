using Biodiversity.Util.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdHandler : BiodiverseAIHandler<MicBirdHandler>
    {
        internal MicBirdAssets Assets { get; private set; }
        internal MicBirdConfig Config { get; private set; }
        internal List<Pair<string, int>> weights { get; private set; }
        internal int totalweight { get; private set; }

        internal string[] compatGUIDS { get; private set; }
        public MicBirdHandler()
        {
            Assets = new MicBirdAssets("biodiversity_micbird");
            Config = new MicBirdConfig(BiodiversityPlugin.Instance.CreateConfig("boombird"));

            totalweight = Config.WalkieMalfunctionWeight + Config.DoorMalfunctionWeight + Config.RadarMalfunctionWeight + Config.LightsOutMalfunctionWeight;

            weights = new List<Pair<string, int>>()
            {
                new Pair<string, int>("WALKIE", Config.WalkieMalfunctionWeight),
                new Pair<string, int>("SHIPDOORS", Config.DoorMalfunctionWeight),
                new Pair<string, int>("RADARBLINK", Config.RadarMalfunctionWeight),
                new Pair<string, int>("LIGHTSOUT", Config.LightsOutMalfunctionWeight)
            };

            compatGUIDS = Config.CompatabilityModeGuids.Split(',');

            int weightadd = 0;
            foreach (var weight in weights)
            {
                weightadd += weight.Second;
                weight.Second = weightadd;
            }

            Assets.MicBirdEnemyType.PowerLevel = Config.PowerLevel;

            TranslateTerminalNode(Assets.MicBirdTerminalNode);
            RegisterEnemyWithConfig(
                Config.EnableBoomBird,
                Config.BoomBirdRarity,
                Assets.MicBirdEnemyType,
                Assets.MicBirdTerminalNode,
                Assets.MicBirdTerminalKeyword);
        }
    }
}
