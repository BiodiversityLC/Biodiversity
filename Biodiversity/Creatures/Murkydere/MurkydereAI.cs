using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.Murkydere;
public class MurkydereAI : BiodiverseAI {

    public enum States {

    }

    public override void DoAIInterval() {
        if(!ShouldProcessEnemy()) return;
        base.DoAIInterval();
        

    }
}
