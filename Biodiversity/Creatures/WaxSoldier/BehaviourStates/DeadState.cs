using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using Biodiversity.Util;
using GameNetcodeStuff;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.Dead)]
internal class DeadState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    public DeadState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Adapter.KillAllSpeed();
        EnemyAIInstance.Context.Adapter.SetNetworkFidelityProfile(EnemyAIInstance.Context.Adapter.PatrolFidelityProfile);

        EnemyAIInstance.Context.Blackboard.NetcodeController.AnimationParamIsDead.Value = true;
    }

    internal override bool OnSetEnemyStunned(bool setToStunned, float setToStunTime = 1, PlayerControllerB setStunnedByPlayer = null)
    {
        base.OnSetEnemyStunned(setToStunned, setToStunTime, setStunnedByPlayer);
        return true; // Makes nothing happen
    }

    internal override bool OnHitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, int hitId = -1)
    {
        base.OnHitEnemy(force, playerWhoHit, hitId);
        return true; // Makes nothing happen
    }

    internal override void OnCustomEvent(string eventName, StateData eventData)
    {
        base.OnCustomEvent(eventName, eventData);

        switch (eventName)
        {
            case nameof(UnmoltenAnimationHandler.OnAnimationEventSlamIntoGround):
            {
                EnemyAIInstance.Context.Blackboard.NetcodeController.SlamIntoGroundClientRpc();
                EnemyAIInstance.KillEnemyServerRpc(false);

                break;
            }
        }
    }
}