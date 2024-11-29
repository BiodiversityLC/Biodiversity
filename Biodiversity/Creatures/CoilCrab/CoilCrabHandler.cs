using Biodiversity.Creatures.MicBird;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.CoilCrab
{
    internal class CoilCrabHandler : BiodiverseAIHandler<CoilCrabHandler>
    {
        internal CoilCrabAssets Assets { get; private set; }

        internal CoilCrabConfig Config { get; private set; }

        public CoilCrabHandler()
        {
            Assets = new CoilCrabAssets("biodiversity_coilcrab");

            Config = new CoilCrabConfig(BiodiversityPlugin.Instance.CreateConfig("coilcrab"));
            



            LethalLib.Modules.Items.RegisterScrap(Assets.CoilShellItem, 0, LethalLib.Modules.Levels.LevelTypes.None);

            RegisterEnemyWithConfig(
                Config.EnableCoilCrab,
                "",
                Assets.CoilCrabEnemy,
                Assets.CoilCrabTerminalNode,
                Assets.CoilCrabTerminalKeyword);
        }
    }
}
