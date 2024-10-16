using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdHandler : BiodiverseAIHandler<MicBirdHandler>
    {
        internal MicBirdAssets Assets { get; private set; }
        internal MicBirdConfig Config { get; private set; }
        public MicBirdHandler()
        {
            Assets = new MicBirdAssets("biodiversity_micbird");
            Config = new MicBirdConfig(BiodiversityPlugin.Instance.CreateConfig("boombird"));

            RegisterEnemyWithConfig(
                Config.EnableBoomBird,
                Config.BoomBirdRarity,
                Assets.MicBirdEnemyType,
                Assets.MicBirdTerminalNode,
                Assets.MicBirdTerminalKeyword);
        }
    }
}
