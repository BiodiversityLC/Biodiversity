using Biodiversity.Creatures.Core.StateMachine;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Animation;

public class UnmoltenAnimationHandler : NetworkBehaviour
{
    [SerializeField] private WaxSoldierAI ai;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
        {
            enabled = false;
        }
    }

    #region Animation Events
    public void OnAnimationEventStartStabAttackLunge()
    {
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAnimationEventStartStabAttackLunge));
    }
    
    public void OnAnimationEventEndStabAttackLunge()
    {
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAnimationEventEndStabAttackLunge));
    }
    
    public void OnAnimationEventStartTargetLook(string aimTransformName)
    {
        ai.LogVerbose("Starting to aim with musket.");
        if (!IsServer) return;

        StateData data = new();
        data.Add("aimTransform",
            aimTransformName == "musketMuzzle" ? ai.Context.Blackboard.HeldMusket.muzzleTip : ai.Context.Adapter.Transform);
        
        ai.TriggerCustomEvent(nameof(OnAnimationEventStartTargetLook), data);
    }
    
    public void OnAnimationEventMusketShoot()
    {
        //ai.LogVerbose("Firing musket.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAnimationEventMusketShoot));
    }

    public void OnAnimationEventToggleBayonet()
    {
        if (!IsServer) return;

        WaxSoldierBayonetAttackPhysics bayonetAttackPhysics = ai.Context.Blackboard.HeldMusket.bayonetAttackPhysics;
        if (bayonetAttackPhysics.currentBayonetMode == WaxSoldierBayonetAttackPhysics.BayonentMode.None)
        {
            ai.LogVerbose($"Toggling bayonet on.");
            
            int hash = ai.Context.Blackboard.currentAttackAction.AnimationTriggerHash;
            if (hash == WaxSoldierClient.SpinAttack)
            {
                bayonetAttackPhysics.BeginAttack(WaxSoldierBayonetAttackPhysics.BayonentMode.Spin);
            }
            if (hash == WaxSoldierClient.StabAttack)
            {
                bayonetAttackPhysics.BeginAttack(WaxSoldierBayonetAttackPhysics.BayonentMode.Stab);
            }
        }
        else
        {
            ai.LogVerbose($"Toggling bayonet mode off.");
            bayonetAttackPhysics.EndAttack();
        }
    }

    public void OnAnimationEventPlayAudio(string sfxName)
    {
        ai.LogVerbose($"Playing audio {sfxName}.");
        if (!IsServer) return;
        ai.PlayAudioClipTypeClientRpc(sfxName, "creatureVoice", 0, true);
    }

    public void OnAnimationEventDropMusket()
    {
        if (!IsServer) return;
        ai.DropMusket();
    }

    public void OnAnimationEventSlamIntoGround()
    {
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAnimationEventSlamIntoGround));
    }
    #endregion
    
    public void OnSpawnAnimationFinish()
    {
        ai.LogVerbose("Spawn animation complete.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnSpawnAnimationFinish));
    }

    public void OnAttackAnimationFinish()
    {
        ai.LogVerbose("Attack animation complete.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAttackAnimationFinish));
    }
    
    public void OnReloadAnimationFinish()
    {
        ai.LogVerbose("Reload animation complete.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnReloadAnimationFinish));
    }
}