using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.MicBird
{
    internal class MicBirdHandler : BiodiverseAIHandler<MicBirdHandler>
    {
        internal MicBirdAssets Assets { get; private set; }
        public MicBirdHandler()
        {
            Assets = new MicBirdAssets("biodiversity_micbird");

            RegisterEnemyWithConfig(
                true,
                "All:20",
                Assets.MicBirdEnemyType,
                Assets.MicBirdTerminalNode,
                Assets.MicBirdTerminalKeyword);
        }
    }
}
