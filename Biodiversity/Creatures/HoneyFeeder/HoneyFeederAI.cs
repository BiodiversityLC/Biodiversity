using Biodiversity.General;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Biodiversity.Creatures.HoneyFeeder;
public class HoneyFeederAI : BiodiverseAI {
    public enum States {
        WANDERING,
        FOUND_HIVE,

    }
    bool hungry = true;

    List<GrabbableObject> possibleHives;
    GrabbableObject targetHive;

    public override void Start() {
        base.Start();
        possibleHives = FindObjectsOfType<RedLocustBees>().Select(bees => bees.hive).ToList();
    }

    public override void DoAIInterval() { // biodiversity calculates everything host end, so this should always be run on the host.
        if(!ShouldProcessEnemy()) return;
        base.DoAIInterval();
    }
}
