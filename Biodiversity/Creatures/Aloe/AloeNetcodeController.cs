using BepInEx.Logging;
using System;
using Unity.Netcode;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Aloe;

public class AloeNetcodeController : NetworkBehaviour
{
    private ManualLogSource _mls;
    private string _aloeId;

#pragma warning disable 0649
    [SerializeField] private AloeClient aloeClient;
#pragma warning restore 0649

    internal readonly NetworkVariable<ulong> TargetPlayerClientId = new();
    internal readonly NetworkVariable<int> CurrentBehaviourStateIndex = new();
    internal readonly NetworkVariable<bool> HasFinishedSpottedAnimation = new();
    internal readonly NetworkVariable<bool> ShouldHaveDarkSkin = new();
    internal readonly NetworkVariable<Vector3> LookTargetPosition = new();

    internal readonly NetworkVariable<bool> AnimationParamCrawling = new();
    internal readonly NetworkVariable<bool> AnimationParamHealing = new();
    internal readonly NetworkVariable<bool> AnimationParamStunned = new();
    internal readonly NetworkVariable<bool> AnimationParamDead = new();

    internal event Action<string> OnSyncAloeId;
    internal event Action<string> OnInitializeConfigValues;
    internal event Action<string, int> OnSetAnimationTrigger;
    internal event Action<string, float, ulong> OnIncreasePlayerFearLevel;
    internal event Action<string> OnMuffleTargetPlayerVoice;
    internal event Action<string> OnUnMuffleTargetPlayerVoice;
    internal event Action<string, int> OnHealTargetPlayerByAmount;
    internal event Action<string> OnTargetPlayerEscaped;
    internal event Action<string, bool> OnSetTargetPlayerInCaptivity;
    internal event Action<string, bool> OnSetTargetPlayerAbleToEscape;
    internal event Action<string, float> OnPlayHealingVfx;
    internal event Action<string, AloeClient.AudioClipTypes, int, bool> OnPlayAudioClipType;
    internal event Action<string, ulong> OnCrushPlayerNeck;
    internal event Action<string, float, float> OnChangeLookAimConstraintWeight;
    internal event Action<string, float> OnTransitionToRunningForwardsAndCarryingPlayer;
    internal event Action<string, NetworkObjectReference> OnSpawnFakePlayerBodyRagdoll;
    internal event Action<string, ulong, int> OnDamagePlayer;

    private void Awake()
    {
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Netcode Controller");
    }

    [ClientRpc]
    internal void DamagePlayerClientRpc(string receivedAloeId, ulong playerId, int damage)
    {
        OnDamagePlayer?.Invoke(receivedAloeId, playerId, damage);
    }

    [ClientRpc]
    internal void SpawnFakePlayerBodyRagdollClientRpc(string receivedAloeId,
        NetworkObjectReference fakePlayerBodyRagdollNetworkObjectReference)
    {
        OnSpawnFakePlayerBodyRagdoll?.Invoke(receivedAloeId, fakePlayerBodyRagdollNetworkObjectReference);
    }

    [ClientRpc]
    internal void TransitionToRunningForwardsAndCarryingPlayerClientRpc(string receivedAloeId, float transitionDuration)
    {
        OnTransitionToRunningForwardsAndCarryingPlayer?.Invoke(receivedAloeId, transitionDuration);
    }

    [ClientRpc]
    internal void ChangeLookAimConstraintWeightClientRpc(string receivedAloeId, float endWeight, float duration = -1f)
    {
        OnChangeLookAimConstraintWeight?.Invoke(receivedAloeId, endWeight, duration);
    }

    [ClientRpc]
    internal void CrushPlayerClientRpc(string receivedAloeId, ulong playerClientId)
    {
        OnCrushPlayerNeck?.Invoke(receivedAloeId, playerClientId);
    }

    [ServerRpc]
    internal void PlayAudioClipTypeServerRpc(string receivedAloeId, AloeClient.AudioClipTypes audioClipType,
        bool interrupt = false)
    {
        if (aloeClient == null)
        {
            _mls.LogError("Aloe client was null, cannot play audio clip");
            return;
        }

        int numberOfAudioClips = audioClipType switch
        {
            AloeClient.AudioClipTypes.Stun => aloeClient.stunSfx.Length,
            AloeClient.AudioClipTypes.Chase => aloeClient.chaseSfx.Length,
            AloeClient.AudioClipTypes.CrackNeck => aloeClient.crackNeckSfx.Length,
            AloeClient.AudioClipTypes.Healing => aloeClient.healingSfx.Length,
            AloeClient.AudioClipTypes.InterruptedHealing => aloeClient.interruptedHealingSfx.Length,
            AloeClient.AudioClipTypes.SnatchAndDrag => aloeClient.snatchAndDragSfx.Length,
            AloeClient.AudioClipTypes.Steps => aloeClient.stepsSfx.Length,
            AloeClient.AudioClipTypes.Hit => aloeClient.hitSfx.Length,
            _ => -1
        };

        switch (numberOfAudioClips)
        {
            case 0:
                _mls.LogError($"There are no audio clips for audio clip type {audioClipType}.");
                return;

            case -1:
                _mls.LogError(
                    $"Audio Clip Type was not listed, cannot play audio clip. Number of audio clips: {numberOfAudioClips}.");
                return;

            default:
            {
                int clipIndex = Random.Range(0, numberOfAudioClips);
                PlayAudioClipTypeClientRpc(receivedAloeId, audioClipType, clipIndex, interrupt);
                break;
            }
        }
    }

    [ClientRpc]
    private void PlayAudioClipTypeClientRpc(string receivedAloeId, AloeClient.AudioClipTypes audioClipType,
        int clipIndex, bool interrupt = false)
    {
        OnPlayAudioClipType?.Invoke(receivedAloeId, audioClipType, clipIndex, interrupt);
    }

    [ClientRpc]
    internal void PlayHealingVfxClientRpc(string receivedAloeId, float totalHealingTime)
    {
        OnPlayHealingVfx?.Invoke(receivedAloeId, totalHealingTime);
    }

    [ClientRpc]
    internal void SetTargetPlayerAbleToEscapeClientRpc(string receivedAloeId, bool canEscape)
    {
        OnSetTargetPlayerAbleToEscape?.Invoke(receivedAloeId, canEscape);
    }

    [ClientRpc]
    internal void SetTargetPlayerInCaptivityClientRpc(string receivedAloeId, bool setToInCaptivity)
    {
        OnSetTargetPlayerInCaptivity?.Invoke(receivedAloeId, setToInCaptivity);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void TargetPlayerEscapedServerRpc(string receivedAloeId)
    {
        OnTargetPlayerEscaped?.Invoke(receivedAloeId);
    }

    [ClientRpc]
    internal void HealTargetPlayerByAmountClientRpc(string receivedAloeId, int healAmount)
    {
        OnHealTargetPlayerByAmount?.Invoke(receivedAloeId, healAmount);
    }

    [ClientRpc]
    internal void UnMuffleTargetPlayerVoiceClientRpc(string receivedAloeId)
    {
        OnUnMuffleTargetPlayerVoice?.Invoke(receivedAloeId);
    }

    [ClientRpc]
    internal void MuffleTargetPlayerVoiceClientRpc(string receivedAloeId)
    {
        OnMuffleTargetPlayerVoice?.Invoke(receivedAloeId);
    }

    [ClientRpc]
    internal void IncreasePlayerFearLevelClientRpc(string receivedAloeId, float targetInsanity, ulong playerClientId)
    {
        OnIncreasePlayerFearLevel?.Invoke(receivedAloeId, targetInsanity, playerClientId);
    }

    /// <summary>
    /// Invokes the set animator trigger event
    /// This uses the trigger function on an animator object
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="animationId">The animation id which is obtained by using the Animator.StringToHash() function</param>
    [ClientRpc]
    internal void SetAnimationTriggerClientRpc(string receivedAloeId, int animationId)
    {
        OnSetAnimationTrigger?.Invoke(receivedAloeId, animationId);
    }

    /// <summary>
    /// Invokes the initialize config values event
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    [ClientRpc]
    internal void InitializeConfigValuesClientRpc(string receivedAloeId)
    {
        OnInitializeConfigValues?.Invoke(receivedAloeId);
    }

    /// <summary>
    /// Invokes the update aloe id event
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    [ClientRpc]
    internal void SyncAloeIdClientRpc(string receivedAloeId)
    {
        _aloeId = receivedAloeId;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Netcode Controller {_aloeId}");

        OnSyncAloeId?.Invoke(receivedAloeId);
    }

    /// <summary>
    /// Only logs the given message if the assembly version is in debug, not release
    /// </summary>
    /// <param name="msg">The debug message to log</param>
    private void LogDebug(string msg)
    {
#if DEBUG
        _mls?.LogInfo(msg);
#endif
    }
}