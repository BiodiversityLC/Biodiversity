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
    public void OnAnimationEventStabAttackLeap()
    {
        ai.LogVerbose("Stab attack leap.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAnimationEventStabAttackLeap));
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
        ai.LogVerbose("Firing musket.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAnimationEventMusketShoot));
    }

    public void OnAnimationEventToggleBayonet()
    {
        if (!IsServer) return;

        MusketBayonetHitbox bayonetHitbox = ai.Context.Blackboard.HeldMusket.bayonetHitbox;
        if (bayonetHitbox.currentBayonetMode == MusketBayonetHitbox.BayonentMode.None)
        {
            int hash = ai.Context.Blackboard.currentAttackAction.AnimationTriggerHash;
            if (hash == WaxSoldierClient.SpinAttack)
            {
                bayonetHitbox.BeginAttack(MusketBayonetHitbox.BayonentMode.Spin);
            }
            if (hash == WaxSoldierClient.StabAttack)
            {
                bayonetHitbox.BeginAttack(MusketBayonetHitbox.BayonentMode.Stab);
            }
            
            ai.LogVerbose($"Toggling bayonet on.");
        }
        else
        {
            ai.LogVerbose($"Toggling bayonet mode off.");
            bayonetHitbox.EndAttack();
        }
    }

    public void OnAnimationEventPlayAudio(string sfxName)
    {
        ai.LogVerbose($"Playing audio {sfxName}.");
        if (!IsServer) return;
        ai.PlayAudioClipTypeClientRpc(sfxName, "creatureVoice", 0, true);
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