using Biodiversity.Creatures.Core.StateMachine;
using System;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.WaxSoldier.Animation;

public class WaxSoldierAnimationEventHandler : NetworkBehaviour
{
    [SerializeField] private WaxSoldierAI _serverAI;
    [SerializeField] private WaxSoldierClient _clientAI;

    #region Animation Events
    public void OnAnimationEventPlayFootstepSfx()
    {
        _clientAI.PlayFoostepSfx();
    }

    public void OnAnimationEventStartStabAttackLunge()
    {
        if (!IsServer) return;
        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventStartStabAttackLunge));
    }

    public void OnAnimationEventEndStabAttackLunge()
    {
        if (!IsServer) return;
        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventEndStabAttackLunge));
    }

    public void OnAnimationEventEndMoltenLunge()
    {
        if (!IsServer) return;
        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventEndMoltenLunge));
    }

    public void OnAnimationEventStartTargetLook(string aimTransformName)
    {
        if (!IsServer) return;
        _serverAI.LogVerbose("Starting to aim with musket.");

        StateData data = new();
        data.Add("aimTransform",
            aimTransformName == "musketMuzzle" ? _serverAI.Context.Blackboard.HeldMusket.muzzleTip : _serverAI.Context.Adapter.Transform);

        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventStartTargetLook), data);
    }

    public void OnAnimationEventMusketShoot()
    {
        // Stops the teeth clacking
        _clientAI.creatureVoice.Stop(true);

        if (!IsServer) return;
        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventMusketShoot));
    }

    public void OnAnimationEventToggleBayonet()
    {
        if (!IsServer) return;

        WaxSoldierBayonetAttackPhysics bayonetAttackPhysics = _serverAI.Context.Blackboard.HeldMusket.bayonetAttackPhysics;
        if (bayonetAttackPhysics.currentBayonetMode == WaxSoldierBayonetAttackPhysics.BayonentMode.None)
        {
            _serverAI.LogVerbose($"Toggling bayonet on.");

            int hash = _serverAI.Context.Blackboard.CurrentAttackAction.AnimationTriggerHash;
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
                _serverAI.LogError($"Unknown bayonet attack mode with hash: {hash}.");
            }

            bayonetAttackPhysics.BeginAttack(nextBayonetMode);
        }
        else
        {
            _serverAI.LogVerbose($"Toggling bayonet off.");
            bayonetAttackPhysics.EndAttack();
        }
    }

    public void OnAnimationEventPlayAudio(string sfxName)
    {
        if (!IsServer) return;
        _serverAI.LogVerbose($"Playing audio {sfxName}.");

        string audioSourceType = "creatureVoice";
        if (sfxName.EndsWith("Music", System.StringComparison.OrdinalIgnoreCase)) audioSourceType = "musicSource";
        _serverAI.PlayAudioClipTypeClientRpc(sfxName, audioSourceType, 0, true);
    }

    public void OnAnimationEventDropMusket()
    {
        if (!IsServer) return;
        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventDropMusket));
    }

    public void OnAnimationEventUntoggleStartMeltParam()
    {
        if (!IsServer) return;
        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventUntoggleStartMeltParam));
    }

    public void OnAnimationEventMeltJitterFinish()
    {
        if (!IsServer) return;
        _serverAI.LogVerbose("Melt jitter animation complete.");
        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventMeltJitterFinish));
    }

    public void OnAnimationEventSlamIntoGround()
    {
        if (!IsServer) return;
        _serverAI.TriggerCustomEvent(nameof(OnAnimationEventSlamIntoGround));
    }

    public void OnAnimationEventDeathComplete()
    {
        if (IsServer) _serverAI.LogVerbose($"{nameof(OnAnimationEventDeathComplete)}.");

        // todo: use events instead of adding code here
        _clientAI.musicSource.Stop(true);

        // todo: remove enemyai collider
    }
    #endregion

    public void OnSpawnAnimationFinish()
    {
        if (!IsServer) return;
        _serverAI.LogVerbose("Spawn animation complete.");
        _serverAI.TriggerCustomEvent(nameof(OnSpawnAnimationFinish));
    }

    public void OnAttackAnimationFinish()
    {
        if (!IsServer) return;
        _serverAI.LogVerbose("Attack animation complete.");
        _serverAI.TriggerCustomEvent(nameof(OnAttackAnimationFinish));
    }

    public void OnReloadAnimationFinish()
    {
        if (!IsServer) return;
        _serverAI.LogVerbose("Reload animation complete.");
        _serverAI.TriggerCustomEvent(nameof(OnReloadAnimationFinish));
    }
}