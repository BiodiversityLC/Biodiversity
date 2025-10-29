using Biodiversity.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Creatures.ClockworkAngel;

[HideHandler]
internal class ClockworkAngelHandler : BiodiverseAIHandler<ClockworkAngelHandler>
{
    internal ClockworkAngelAssets Assets { get; private set; }

    public ClockworkAngelHandler()
    {
        // Assets = new ClockworkAngelAssets("biodiversity_clockworkangel");
    }
}