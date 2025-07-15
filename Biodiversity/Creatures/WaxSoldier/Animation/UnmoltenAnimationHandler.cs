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
    
    public void OnSpawnAnimationStateExit()
    {
        ai.LogVerbose("Spawn animation complete.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnSpawnAnimationStateExit));
    }

    public void OnAttackAnimationFinish()
    {
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAttackAnimationFinish));
    }
    
    public void OnAnimationEventStabAttackLeap()
    {
        ai.LogVerbose("Stab attack leap.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAnimationEventStabAttackLeap));
    }
    
    public void OnAnimationEventStartTargetLook(string lookTransformName)
    {
        if (!IsServer) return;

        StateData data = new();
        data.Add("lookTransform",
            lookTransformName == "musketMuzzle" ? ai.Context.Blackboard.HeldMusket.muzzleTip : transform);
        
        ai.TriggerCustomEvent(nameof(OnAnimationEventStartTargetLook), data);
    }
    
    public void OnAnimationEventStopTargetLook()
    {
        if (!IsServer) return;
        ai.LogVerbose("Stop target look.");
        ai.TriggerCustomEvent(nameof(OnAnimationEventStopTargetLook));
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
    
    public void OnAnimationEventMusketShoot()
    {
        ai.LogVerbose("Musket fired.");
        if (!IsServer) return;
        ai.Context.Blackboard.HeldMusket.SetupShoot();
    }

    public void OnAnimationEventPlayAudio(string sfxName)
    {
        ai.LogVerbose($"Playing audio {sfxName}.");
        if (!IsServer) return;
        ai.PlayAudioClipTypeClientRpc(sfxName, "creatureVoice", 0, true);
    }
}