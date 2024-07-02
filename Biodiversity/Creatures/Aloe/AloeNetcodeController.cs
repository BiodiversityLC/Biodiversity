using System;
using BepInEx.Logging;
using Unity.Netcode;

namespace Biodiversity.Creatures.Aloe;

public class AloeNetcodeController : NetworkBehaviour
{
    private ManualLogSource _mls;
    private string _aloeId;

    public event Action<string> OnSyncAloeId;
    public event Action<string> OnInitializeConfigValues;
    public event Action<string, int, bool> OnChangeAnimationParameterBool;
    public event Action<string, int> OnSetTrigger;
    public event Action<string, int> OnResetTrigger;
    public event Action<string, float, ulong> OnIncreasePlayerFearLevel;
    public event Action<string> OnMuffleTargetPlayerVoice;
    public event Action<string> OnUnMuffleTargetPlayerVoice;
    public event Action<string, ulong> OnChangeTargetPlayer;
    public event Action<string, int> OnHealTargetPlayerByAmount;
    public event Action<string> OnTargetPlayerEscaped;
    public event Action<string> OnEnterDeathState;
    public event Action<string, bool> OnSetTargetPlayerInCaptivity;
    public event Action<string, bool> OnSetTargetPlayerAbleToEscape;
    public event Action<string, float> OnPlayHealingVfx;
    public event Action<string, bool> OnChangeAloeSkinColour;
    public event Action<string, AloeClient.AudioClipTypes, int, bool> OnPlayAudioClipType;
    public event Action<string, int> OnChangeBehaviourState;
    public event Action<string, ulong> OnSnapPlayerNeck;
    public event Action<string> OnGrabTargetPlayer;
    public event Action<string, float, float> OnChangeLookAimConstraintWeight;

    private void Start()
    {
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Netcode Controller");
    }

    [ClientRpc]
    public void ChangeLookAimConstraintWeightClientRpc(string receivedAloeId, float endWeight, float duration = -1f)
    {
        OnChangeLookAimConstraintWeight?.Invoke(receivedAloeId, endWeight, duration);
    }

    [ServerRpc]
    public void GrabTargetPlayerServerRpc(string receivedAloeId)
    {
        OnGrabTargetPlayer?.Invoke(receivedAloeId);
    }

    [ClientRpc]
    public void SnapPlayerNeckClientRpc(string receivedAloeId, ulong playerClientId)
    {
        OnSnapPlayerNeck?.Invoke(receivedAloeId, playerClientId);
    }

    [ClientRpc]
    public void ChangeBehaviourStateClientRpc(string receivedAloeId, int newBehaviourStateIndex)
    {
        OnChangeBehaviourState?.Invoke(receivedAloeId, newBehaviourStateIndex);
    }
    
    [ServerRpc]
    public void PlayAudioClipTypeServerRpc(string receivedAloeId, AloeClient.AudioClipTypes audioClipType, bool interrupt = false)
    {
        AloeClient aloeClient = GetComponent<AloeClient>();
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
                _mls.LogError($"Audio Clip Type was not listed, cannot play audio clip. Number of audio clips: {numberOfAudioClips}.");
                return;
            
            default:
            {
                int clipIndex = UnityEngine.Random.Range(0, numberOfAudioClips);
                PlayAudioClipTypeClientRpc(receivedAloeId, audioClipType, clipIndex, interrupt);
                break;
            }
        }
    }

    [ClientRpc]
    private void PlayAudioClipTypeClientRpc(string receivedAloeId, AloeClient.AudioClipTypes audioClipType, int clipIndex, bool interrupt = false)
    {
        OnPlayAudioClipType?.Invoke(receivedAloeId, audioClipType, clipIndex, interrupt);
    }

    [ClientRpc]
    public void ChangeAloeSkinColourClientRpc(string receivedAloeId, bool toDark)
    {
        OnChangeAloeSkinColour?.Invoke(receivedAloeId, toDark);
    }

    [ClientRpc]
    public void PlayHealingVfxClientRpc(string receivedAloeId, float totalHealingTime)
    {
        OnPlayHealingVfx?.Invoke(receivedAloeId, totalHealingTime);
    }
    
    [ClientRpc]
    public void SetTargetPlayerAbleToEscapeClientRpc(string receivedAloeId, bool canEscape)
    {
        OnSetTargetPlayerAbleToEscape?.Invoke(receivedAloeId, canEscape);
    }

    [ClientRpc]
    public void SetTargetPlayerInCaptivityClientRpc(string receivedAloeId, bool setToInCaptivity)
    {
        OnSetTargetPlayerInCaptivity?.Invoke(receivedAloeId, setToInCaptivity);
    }

    [ClientRpc]
    public void EnterDeathStateClientRpc(string receivedAloeId)
    {
        OnEnterDeathState?.Invoke(receivedAloeId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void TargetPlayerEscapedServerRpc(string receivedAloeId)
    {
       OnTargetPlayerEscaped?.Invoke(receivedAloeId); 
    }

    [ClientRpc]
    public void HealTargetPlayerByAmountClientRpc(string receivedAloeId, int healAmount)
    {
        OnHealTargetPlayerByAmount?.Invoke(receivedAloeId, healAmount);
    }

    [ClientRpc]
    public void ChangeTargetPlayerClientRpc(string receivedAloeId, ulong targetPlayerObjectId)
    {
        OnChangeTargetPlayer?.Invoke(receivedAloeId, targetPlayerObjectId);
    }

    [ClientRpc]
    public void UnMuffleTargetPlayerVoiceClientRpc(string receivedAloeId)
    {
        OnUnMuffleTargetPlayerVoice?.Invoke(receivedAloeId);
    }

    [ClientRpc]
    public void MuffleTargetPlayerVoiceClientRpc(string receivedAloeId)
    {
       OnMuffleTargetPlayerVoice?.Invoke(receivedAloeId); 
    }

    [ClientRpc]
    public void IncreasePlayerFearLevelClientRpc(string receivedAloeId, float targetInsanity, ulong playerClientId)
    {
        OnIncreasePlayerFearLevel?.Invoke(receivedAloeId, targetInsanity, playerClientId);
    }
    
    /// <summary>
    /// Invokes the reset animator trigger event
    /// This uses the trigger function on an animator object
    /// </summary>
    /// <param name="receivedAloeId">The Aloe Id</param>
    /// <param name="animationId">The animation id which is obtained by using the Animator.StringToHash() function</param>
    [ClientRpc]
    public void ResetTriggerClientRpc(string receivedAloeId, int animationId)
    {
        OnResetTrigger?.Invoke(receivedAloeId, animationId);
    }

    /// <summary>
    /// Invokes the set animator trigger event
    /// This uses the trigger function on an animator object
    /// </summary>
    /// <param name="receivedAloeId">The Aloe Id</param>
    /// <param name="animationId">The animation id which is obtained by using the Animator.StringToHash() function</param>
    [ClientRpc]
    public void SetTriggerClientRpc(string receivedAloeId, int animationId)
    {
        OnSetTrigger?.Invoke(receivedAloeId, animationId);
    }

    /// <summary>
    /// Invokes the change animation parameter event
    /// </summary>
    /// <param name="receivedAloeId">The Aloe Id</param>
    /// <param name="animationId">The animation id which is obtained by using the Animator.StringToHash() function</param>
    /// <param name="value">The bool value to change to</param>
    [ClientRpc]
    public void ChangeAnimationParameterBoolClientRpc(string receivedAloeId, int animationId, bool value)
    {
        OnChangeAnimationParameterBool?.Invoke(receivedAloeId, animationId, value);
    }

    /// <summary>
    /// Invokes the initialize config values event
    /// </summary>
    /// <param name="receivedAloeId">The Aloe Id</param>
    [ClientRpc]
    public void InitializeConfigValuesClientRpc(string receivedAloeId)
    {
        OnInitializeConfigValues?.Invoke(receivedAloeId);
    }

    /// <summary>
    /// Invokes the update aloe id event
    /// </summary>
    /// <param name="receivedAloeId">The Aloe Id</param>
    [ClientRpc]
    public void SyncAloeIdClientRpc(string receivedAloeId)
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