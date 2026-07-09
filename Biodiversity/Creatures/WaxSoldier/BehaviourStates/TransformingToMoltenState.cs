using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.Animation;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.WaxSoldier.BehaviourStates;

[Preserve]
[State(WaxSoldierAI.States.TransformingToMolten)]
internal class TransformingToMoltenState : BehaviourState<WaxSoldierAI.States, WaxSoldierAI>
{
    private bool hasTriggeredAnimation;

    public TransformingToMoltenState(WaxSoldierAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions = [];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.Context.Blackboard.MoltenState = WaxSoldierAI.MoltenState.Molten;
        EnemyAIInstance.Context.Blackboard.HuntingLingerTime = 800f;

        EnemyAIInstance.Context.Adapter.StopAllPathing();
        EnemyAIInstance.Context.Adapter.BeginGracefulStop();
        EnemyAIInstance.Context.Adapter.SetNetworkFidelityProfile(EnemyAIInstance.Context.Adapter.PatrolFidelityProfile);

        hasTriggeredAnimation = false;
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();

        if (!hasTriggeredAnimation && EnemyAIInstance.Context.Adapter.Agent.velocity.sqrMagnitude <= 0.5f)
        {
            EnemyAIInstance.LogVerbose("Starting melt animation...");
            EnemyAIInstance.Context.Blackboard.NetcodeController.AnimationParamStartMelting.Value = true;

            hasTriggeredAnimation = true;
            EnemyAIInstance.Context.Adapter.KillAllSpeed();
        }
    }

    internal override bool OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        return true; // Makes nothing happen
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
            case nameof(WaxSoldierAnimationEventHandler.OnAnimationEventUntoggleStartMeltParam):
                EnemyAIInstance.Context.Blackboard.NetcodeController.AnimationParamStartMelting.Value = false;
                break;

            case nameof(WaxSoldierAnimationEventHandler.OnAnimationEventMeltJitterFinish):
                EnemyAIInstance.Context.Blackboard.NetcodeController.CompleteMoltenTransitionClientRpc();

                EnemyAIInstance.Context.Adapter.Animator =
                    EnemyAIInstance.GetComponent<WaxSoldierClient>().moltenAnimator;

                EnemyAIInstance.UpdateBehaviourStateFromPerception();
                break;
        }
    }
}