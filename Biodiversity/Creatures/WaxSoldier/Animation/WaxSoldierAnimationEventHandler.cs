using Biodiversity.Creatures.Core.StateMachine;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Animation;

public class WaxSoldierAnimationEventHandler : NetworkBehaviour
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

    public void OnAnimationEventEndMoltenLunge()
    {
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnAnimationEventEndMoltenLunge));
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
        // Stops the teeth clacking
        ai.creatureVoice.Stop();

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

            int hash = ai.Context.Blackboard.CurrentAttackAction.AnimationTriggerHash;
            WaxSoldierBayonetAttackPhysics.BayonentMode nextBayonetMode =
                WaxSoldierBayonetAttackPhysics.BayonentMode.None;

            // todo: fix this monstrosity
            if (hash == WaxSoldierClient.SpinAttack)
            {
                nextBayonetMode = WaxSoldierBayonetAttackPhysics.BayonentMode.Spin;
            }
            else if (hash == WaxSoldierClient.StabAttack)
            {
                nextBayonetMode = WaxSoldierBayonetAttackPhysics.BayonentMode.Stab;
            }
            else if (hash == WaxSoldierClient.SwingAttack)
            {
                nextBayonetMode = WaxSoldierBayonetAttackPhysics.BayonentMode.Swing;
            }
            else if (hash == WaxSoldierClient.FlailAttack)
            {
                nextBayonetMode = WaxSoldierBayonetAttackPhysics.BayonentMode.Flail;
            }
            else
            {
                ai.LogError($"Unknown bayonet attack mode with hash: {hash}.");
            }

            bayonetAttackPhysics.BeginAttack(nextBayonetMode);
        }
        else
        {
            ai.LogVerbose($"Toggling bayonet off.");
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

    public void OnMeltJitterAnimationFinish()
    {
        ai.LogVerbose("Melt jitter animation complete.");
        if (!IsServer) return;
        ai.TriggerCustomEvent(nameof(OnMeltJitterAnimationFinish));
    }
}