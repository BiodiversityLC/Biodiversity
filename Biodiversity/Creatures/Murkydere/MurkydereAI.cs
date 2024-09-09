using Biodiversity.General;

namespace Biodiversity.Creatures.Murkydere;
public class MurkydereAI : BiodiverseAI {

    public enum States {

    }

    public override void DoAIInterval() {
        if(!ShouldProcessEnemy()) return;
        base.DoAIInterval();
        

    }
}
