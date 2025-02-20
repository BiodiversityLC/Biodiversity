using System;
using Unity.Netcode;

namespace Biodiversity.Creatures.Aloe;

public class AloeNetcodeController : NetworkBehaviour
{
    internal readonly NetworkVariable<ulong> TargetPlayerClientId = new();
    internal readonly NetworkVariable<bool> HasFinishedSpottedAnimation = new();
    internal readonly NetworkVariable<bool> ShouldHaveDarkSkin = new();

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
    internal event Action<string, ulong> OnCrushPlayerNeck;
    internal event Action<string, float> OnTransitionToRunningForwardsAndCarryingPlayer;
    internal event Action<string, NetworkObjectReference> OnSpawnFakePlayerBodyRagdoll;
    internal event Action<string, ulong, int> OnDamagePlayer;

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
    internal void CrushPlayerClientRpc(string receivedAloeId, ulong playerClientId)
    {
        OnCrushPlayerNeck?.Invoke(receivedAloeId, playerClientId);
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
        OnSyncAloeId?.Invoke(receivedAloeId);
    }
}