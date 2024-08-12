using Biodiversity.Patches;
using System;
using System.Collections.Generic;
using System.Text;
using Biodiversity.Util.Lang;

namespace Biodiversity.General;
internal abstract class BiodiverseAIHandler<T> where T : BiodiverseAIHandler<T> {

    internal static T Instance { get; private set; }

    internal BiodiverseAIHandler() {
        Instance = (T)this;
    }

    protected void AddSpawnRequirement(EnemyType type, Func<bool> callback) {
        RoundManagerPatch.spawnRequirements.Add(type, callback);
    }

    protected void TranslateTerminalNode(TerminalNode node) {
        node.displayText = LangParser.GetTranslation(node.displayText);
    }
}
