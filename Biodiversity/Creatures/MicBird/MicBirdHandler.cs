using Biodiversity.Util.DataStructures;
using System.Collections.Generic;

namespace Biodiversity.Creatures.MicBird;

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

        weights = new List<Pair<string, int>>
        {
            new("WALKIE", Config.WalkieMalfunctionWeight),
            new("SHIPDOORS", Config.DoorMalfunctionWeight),
            new("RADARBLINK", Config.RadarMalfunctionWeight),
            new("LIGHTSOUT", Config.LightsOutMalfunctionWeight)
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