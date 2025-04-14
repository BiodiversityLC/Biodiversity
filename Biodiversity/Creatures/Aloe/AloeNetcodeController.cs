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
    
    internal event Action OnInitializeConfigValues;
    internal event Action<int> OnSetAnimationTrigger;
    internal event Action<float, ulong> OnIncreasePlayerFearLevel;
    internal event Action OnMuffleTargetPlayerVoice;
    internal event Action OnUnMuffleTargetPlayerVoice;
    internal event Action<int> OnHealTargetPlayerByAmount;
    internal event Action OnTargetPlayerEscaped;
    internal event Action<bool> OnSetTargetPlayerInCaptivity;
    internal event Action<bool> OnSetTargetPlayerAbleToEscape;
    internal event Action<float> OnPlayHealingVfx;
    internal event Action<ulong> OnCrushPlayerNeck;
    internal event Action<float> OnTransitionToRunningForwardsAndCarryingPlayer;
    internal event Action<NetworkObjectReference> OnSpawnFakePlayerBodyRagdoll;
    internal event Action<ulong, int> OnDamagePlayer;
    
    [ClientRpc]
    internal void DamagePlayerClientRpc(ulong playerId, int damage)
    {
        OnDamagePlayer?.Invoke(playerId, damage);
    }

    [ClientRpc]
    internal void SpawnFakePlayerBodyRagdollClientRpc(NetworkObjectReference fakePlayerBodyRagdollNetworkObjectReference)
    {
        OnSpawnFakePlayerBodyRagdoll?.Invoke(fakePlayerBodyRagdollNetworkObjectReference);
    }

    [ClientRpc]
    internal void TransitionToRunningForwardsAndCarryingPlayerClientRpc(float transitionDuration)
    {
        OnTransitionToRunningForwardsAndCarryingPlayer?.Invoke(transitionDuration);
    }

    [ClientRpc]
    internal void CrushPlayerClientRpc(ulong playerClientId)
    {
        OnCrushPlayerNeck?.Invoke(playerClientId);
    }

    [ClientRpc]
    internal void PlayHealingVfxClientRpc(float totalHealingTime)
    {
        OnPlayHealingVfx?.Invoke(totalHealingTime);
    }

    [ClientRpc]
    internal void SetTargetPlayerAbleToEscapeClientRpc(bool canEscape)
    {
        OnSetTargetPlayerAbleToEscape?.Invoke(canEscape);
    }

    [ClientRpc]
    internal void SetTargetPlayerInCaptivityClientRpc(bool setToInCaptivity)
    {
        OnSetTargetPlayerInCaptivity?.Invoke(setToInCaptivity);
    }

    [ServerRpc(RequireOwnership = false)]
    internal void TargetPlayerEscapedServerRpc()
    {
        OnTargetPlayerEscaped?.Invoke();
    }

    [ClientRpc]
    internal void HealTargetPlayerByAmountClientRpc(int healAmount)
    {
        OnHealTargetPlayerByAmount?.Invoke(healAmount);
    }

    [ClientRpc]
    internal void UnMuffleTargetPlayerVoiceClientRpc()
    {
        OnUnMuffleTargetPlayerVoice?.Invoke();
    }

    [ClientRpc]
    internal void MuffleTargetPlayerVoiceClientRpc()
    {
        OnMuffleTargetPlayerVoice?.Invoke();
    }

    [ClientRpc]
    internal void IncreasePlayerFearLevelClientRpc(float targetInsanity, ulong playerClientId)
    {
        OnIncreasePlayerFearLevel?.Invoke(targetInsanity, playerClientId);
    }

    /// <summary>
    /// Invokes the set animator trigger event
    /// This uses the trigger function on an animator object
    /// </summary>
    /// <param name="animationId">The animation id which is obtained by using the Animator.StringToHash() function</param>
    [ClientRpc]
    internal void SetAnimationTriggerClientRpc(int animationId)
    {
        OnSetAnimationTrigger?.Invoke(animationId);
    }

    /// <summary>
    /// Invokes the initialize config values event
    /// </summary>
    [ClientRpc]
    internal void InitializeConfigValuesClientRpc()
    {
        OnInitializeConfigValues?.Invoke();
    }
}