using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.General;
public abstract class BiodiverseAI : EnemyAI {
    public bool ShouldProcessEnemy() {
        return isEnemyDead || StartOfRound.Instance.allPlayersDead;
    }
}
