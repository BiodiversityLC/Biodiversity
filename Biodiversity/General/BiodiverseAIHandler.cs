using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.General;
internal abstract class BiodiverseAIHandler<T> where T : BiodiverseAIHandler<T> {
    internal static T Instance { get; private set; }

    internal BiodiverseAIHandler() {
        Instance = (T)this;
    }
}
