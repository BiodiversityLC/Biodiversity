using Biodiversity.Creatures.Murkydere.States;
using Biodiversity.General;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.Murkydere;
public class MurkydereAI : BiodiverseAI<MurkydereAI> {
    internal EnemyState<MurkydereAI> wanderingState, agroState;

    public MurkydereAI() {
        wanderingState = new WanderingState(this);
        agroState = new AgroState(this);

        InitAI(wanderingState);

        CurrentState = agroState;
        CurrentState = wanderingState;
    }

    public override string GetName() {
        return "Murkydere";
    }
}
