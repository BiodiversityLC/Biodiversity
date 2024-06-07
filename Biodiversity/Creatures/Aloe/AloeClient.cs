using System.Collections;
using BepInEx.Logging;
using Dissonance;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;
using Logger = BepInEx.Logging.Logger;

namespace Biodiversity.Creatures.Aloe;

public class AloeClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _aloeId;
    
    private static readonly int Metallic = Shader.PropertyToID("_Metallic");
    private static readonly int BaseColour = Shader.PropertyToID("BaseColour");

    public enum AudioClipTypes
    {
        Stun,
        Chase,
        CrackNeck,
        Healing,
        InterruptedHealing,
        SnatchAndDrag,
        Steps,
    }
    
    [field: SerializeField] public AloeConfig Config { get; private set; } = AloeHandler.Instance.Config;
    
#pragma warning disable 0649
    [Header("Audio")] [Space(5f)] 
    public AudioClip[] stunSfx;
    public AudioClip[] chaseSfx;
    public AudioClip[] crackNeckSfx;
    public AudioClip[] healingSfx;
    public AudioClip[] interruptedHealingSfx;
    public AudioClip[] snatchAndDragSfx;
    public AudioClip[] stepsSfx;
    [SerializeField] private AudioSource aloeVoiceSource;
    [SerializeField] private AudioSource aloeFootstepsSource;
    
    [Header("Renderers and Materials")] [Space(5f)]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Renderer leavesRenderer;
    [SerializeField] private Renderer petalsRenderer;
    [SerializeField] private MaterialPropertyBlock _propertyBlock;
    [SerializeField] private float skinMetallicTransitionTime = 7.5f;
    [SerializeField] private float skinMetallicPropertyValue = 0.735f;
    
    [Header("Visual Effects")] [Space(5f)]
    [SerializeField] private VisualEffect healingOrbEffect;
    [SerializeField] private ParticleSystem poofParticleSystem;
    
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private Animator animator;
    [SerializeField] private Collider collider;
    [SerializeField] private GameObject scanNode;
    [SerializeField] private AloeNetcodeController netcodeController;
#pragma warning restore 0649
    
    [SerializeField] private float escapeChargePerPress = 15f;
    [SerializeField] private float escapeChargeDecayRate = 15f;
    [SerializeField] private float escapeChargeThreshold = 100f;
    
    private PlayerControllerB _targetPlayer;
    
    private bool _targetPlayerCanEscape;

    private float _currentEscapeChargeValue;
    private float _agentCurrentSpeed;
    
    private Vector3 _agentLastPosition;
    
    private int _currentBehaviourStateIndex;

    /// <summary>
    /// Subscribe to the needed network events.
    /// </summary>
    private void OnEnable()
    {
        if (netcodeController == null) return;
        netcodeController.OnSyncAloeId += HandleSyncAloeId;
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
        netcodeController.OnDoAnimation += SetTrigger;
        netcodeController.OnChangeAnimationParameterBool += SetBool;
        netcodeController.OnChangeTargetPlayer += HandleChangeTargetPlayer;
        netcodeController.OnMuffleTargetPlayerVoice += HandleMuffleTargetPlayerVoice;
        netcodeController.OnUnMuffleTargetPlayerVoice += HandleUnMuffleTargetPlayerVoice;
        netcodeController.OnIncreasePlayerFearLevel += HandleIncreasePlayerFearLevel;
        netcodeController.OnHealTargetPlayerByAmount += HandleHealTargetPlayerByAmount;
        netcodeController.OnSetTargetPlayerInCaptivity += HandleSetTargetPlayerInCaptivity;
        netcodeController.OnSetTargetPlayerAbleToEscape += HandleSetTargetPlayerAbleToEscape;
        netcodeController.OnEnterDeathState += HandleEnterDeathState;
        netcodeController.OnPlayHealingVfx += HandlePlayHealingVfx;
        netcodeController.OnChangeAloeSkinColour += HandleChangeAloeSkinColour;
        netcodeController.OnPlayAudioClipType += HandlePlayAudioClipType;
        netcodeController.OnChangeBehaviourState += HandleChangeBehaviourStateIndex;
        netcodeController.OnSnapPlayerNeck += HandleSnapPlayerNeck;

        StartCoroutine(PlayStepsSfx());
    }

    /// <summary>
    /// Unsubscribe to the network events when the creature is dead.
    /// </summary>
    private void OnDisable()
    {
        if (netcodeController == null) return;
        netcodeController.OnSyncAloeId -= HandleSyncAloeId;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
        netcodeController.OnDoAnimation -= SetTrigger;
        netcodeController.OnChangeAnimationParameterBool -= SetBool;
        netcodeController.OnChangeTargetPlayer -= HandleChangeTargetPlayer;
        netcodeController.OnMuffleTargetPlayerVoice -= HandleMuffleTargetPlayerVoice;
        netcodeController.OnUnMuffleTargetPlayerVoice -= HandleUnMuffleTargetPlayerVoice;
        netcodeController.OnIncreasePlayerFearLevel -= HandleIncreasePlayerFearLevel;
        netcodeController.OnHealTargetPlayerByAmount -= HandleHealTargetPlayerByAmount;
        netcodeController.OnSetTargetPlayerInCaptivity -= HandleSetTargetPlayerInCaptivity;
        netcodeController.OnSetTargetPlayerAbleToEscape -= HandleSetTargetPlayerAbleToEscape;
        netcodeController.OnEnterDeathState -= HandleEnterDeathState;
        netcodeController.OnPlayHealingVfx -= HandlePlayHealingVfx;
        netcodeController.OnChangeAloeSkinColour -= HandleChangeAloeSkinColour;
        netcodeController.OnPlayAudioClipType -= HandlePlayAudioClipType;
        netcodeController.OnChangeBehaviourState -= HandleChangeBehaviourStateIndex;
        netcodeController.OnSnapPlayerNeck -= HandleSnapPlayerNeck;
        
        StopCoroutine(PlayStepsSfx());
    }
    
    private void Start()
    {
        _mls = Logger.CreateLogSource($"Biodiversity | Aloe Client {_aloeId}");
        _propertyBlock = new MaterialPropertyBlock();
    }
    
    private void FixedUpdate()
    {
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
    }

    /// <summary>
    /// This function is called every frame
    /// </summary>
    private void Update()
    {
        if (!_targetPlayerCanEscape)
        {
            _currentEscapeChargeValue = 0;
            return;
        }

        switch (_currentBehaviourStateIndex)
        {
            case (int)AloeServer.States.HealingPlayer or (int)AloeServer.States.CuddlingPlayer:
            {
                if (_targetPlayer == null) break;
                if (GameNetworkManager.Instance.localPlayerController.playerClientId != _targetPlayer.playerClientId) break;
        
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    LogDebug($"Space key was pressed, the new escape charge value is: {_currentEscapeChargeValue}");
                    _currentEscapeChargeValue += escapeChargePerPress;
                }

                if (_currentEscapeChargeValue > 0) _currentEscapeChargeValue -= escapeChargeDecayRate * Time.deltaTime;
                else _currentEscapeChargeValue = 0;

                if (_currentEscapeChargeValue >= escapeChargeThreshold)
                {
                    LogDebug("Triggering aloe escape");
                    _currentEscapeChargeValue = 0;
                    _targetPlayerCanEscape = false;
                    netcodeController.TargetPlayerEscapedServerRpc(_aloeId);
                }
                
                break;
            }
        }
    }

    /// <summary>
    /// Handles what should happen to make the Aloe snap a player's neck
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="playerClientId">The player's client id whose neck will be snapped</param>
    private void HandleSnapPlayerNeck(string receivedAloeId, ulong playerClientId)
    {
        if (_aloeId != receivedAloeId) return;
        
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerClientId];
        if (player == null) return;
        LogDebug($"Killing player: {player.name}");
        //player.inSpecialInteractAnimation = true;
        
        player.KillPlayer(Vector3.zero, true, CauseOfDeath.Strangulation);
    }
    
    /// <summary>
    /// Plays an audio clip with the given type and index
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="audioClipType">The audio clip type to play</param>
    /// <param name="clipIndex">The index of the clip in their respective AudioClip array to play</param>
    /// <param name="interrupt">Whether to interrupt any previously playing sound before playing the new audio</param>
    private void HandlePlayAudioClipType(string receivedAloeId, AudioClipTypes audioClipType, int clipIndex, bool interrupt = false)
    {
        if (_aloeId != receivedAloeId) return;

        AudioClip audioClipToPlay = audioClipType switch
        {
            AudioClipTypes.Stun => stunSfx[clipIndex],
            AudioClipTypes.Chase => chaseSfx[clipIndex],
            AudioClipTypes.CrackNeck => crackNeckSfx[clipIndex],
            AudioClipTypes.Healing => healingSfx[clipIndex],
            AudioClipTypes.InterruptedHealing => interruptedHealingSfx[clipIndex],
            AudioClipTypes.SnatchAndDrag => snatchAndDragSfx[clipIndex],
            AudioClipTypes.Steps => stepsSfx[clipIndex],
            _ => null
        };

        if (audioClipToPlay == null)
        {
            _mls.LogError($"Invalid audio clip with type: {audioClipType} and index: {clipIndex}");
            return;
        }
        
        LogDebug($"Playing audio clip: {audioClipToPlay.name}");
        if (interrupt) aloeVoiceSource.Stop(true);
        aloeVoiceSource.PlayOneShot(audioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(aloeVoiceSource, audioClipToPlay, aloeVoiceSource.volume);
    }

    /// <summary>
    /// Handles what should happen to make the aloe change her skin colour
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="toDark">Whether the skin colour is going to a dark colour or not</param>
    private void HandleChangeAloeSkinColour(string receivedAloeId, bool toDark)
    {
        if (_aloeId != receivedAloeId) return;

        _propertyBlock ??= new MaterialPropertyBlock();
        if (bodyRenderer == null)
        {
            LogDebug("body renderer is null");
            return;
        }

        if (_propertyBlock == null)
        {
            LogDebug("Property block is null");
            return;
        }
        
        bodyRenderer.GetPropertyBlock(_propertyBlock);
        leavesRenderer.GetPropertyBlock(_propertyBlock);
        StartCoroutine(ChangeAloeSkinColour(toDark));
    }

    /// <summary>
    /// Plays the steps sound effect in a random interval if the aloe is moving
    /// Replace in the future with animation events
    /// </summary>
    /// <returns></returns>
    private IEnumerator PlayStepsSfx()
    {
        // Would not work if the aloe was revived at a later date
        if (_currentBehaviourStateIndex == (int)AloeServer.States.Dead) yield break;

        if (_agentCurrentSpeed >= 2)
        {
            AudioClip audioClipToPlay = stepsSfx[Random.Range(0, stepsSfx.Length)];
            LogDebug($"Playing audio clip: {audioClipToPlay.name}");
            aloeFootstepsSource.PlayOneShot(audioClipToPlay);
            WalkieTalkie.TransmitOneShotAudio(aloeFootstepsSource, audioClipToPlay, aloeFootstepsSource.volume);
        }

        yield return new WaitForSeconds(Random.Range(1.9f, 2.5f));
    }

    /// <summary>
    /// A coroutine for smoothly transitioning the aloe's skin colour
    /// </summary>
    /// <param name="toDark">Whether her skin colour is going to a dark colour or not</param>
    /// <returns></returns>
    private IEnumerator ChangeAloeSkinColour(bool toDark)
    {
        LogDebug($"Changing aloe skin to {(toDark ? "dark" : "light")} colour");
        float timeElapsed = 0f;
        float startMetallicValue = toDark ? 0 : skinMetallicPropertyValue;
        float endMetallicValue = toDark ? skinMetallicPropertyValue : 0;

        Color currentColour = bodyRenderer.material.GetColor(BaseColour);
        RGBToHSV(currentColour, out float h, out float s, out float v);
        float endV = toDark ? 0.5f : 1f;

        while (timeElapsed < skinMetallicTransitionTime)
        {
            float currentMetallicValue = Mathf.Lerp(startMetallicValue, endMetallicValue, timeElapsed / skinMetallicTransitionTime);
            float currentV = Mathf.Lerp(v, endV, timeElapsed / skinMetallicTransitionTime);
            Color newColour = HSVToRGB(h, s, currentV);
            
            _propertyBlock.SetFloat(Metallic, currentMetallicValue);
            _propertyBlock.SetColor(BaseColour, newColour);
            
            bodyRenderer.SetPropertyBlock(_propertyBlock);
            leavesRenderer.SetPropertyBlock(_propertyBlock);
            //LogDebug($"current metallic value: {currentMetallicValue}, current v value: {currentV}");
            
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        
        // Ensure the final value is set exactly at the end
        _propertyBlock.SetFloat(Metallic, endMetallicValue);
        _propertyBlock.SetColor(BaseColour, HSVToRGB(h, s, endV));
        bodyRenderer.SetPropertyBlock(_propertyBlock);
        leavesRenderer.SetPropertyBlock(_propertyBlock);
    }

    /// <summary>
    /// Converts an RGB color to HSV and returns the original RGB color.
    /// </summary>
    /// <param name="rgb">The RGB color to convert.</param>
    /// <param name="h">The hue component of the HSV color.</param>
    /// <param name="s">The saturation component of the HSV color.</param>
    /// <param name="v">The value component of the HSV color.</param>
    /// <returns>The original RGB color.</returns>
    private static Color RGBToHSV(Color rgb, out float h, out float s, out float v)
    {
        Color.RGBToHSV(rgb, out h, out s, out v);
        return rgb;
    }

    /// <summary>
    /// Converts HSV color components to an RGB color.
    /// </summary>
    /// <param name="h">The hue component of the HSV color.</param>
    /// <param name="s">The saturation component of the HSV color.</param>
    /// <param name="v">The value component of the HSV color.</param>
    /// <returns>The RGB color corresponding to the given HSV components.</returns>
    private static Color HSVToRGB(float h, float s, float v)
    {
        return Color.HSVToRGB(h, s, v);
    }

    /// <summary>
    /// Heals the target player by the given amount.
    /// It also updates their health bar.
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="healAmount">The amount to heal the target player by</param>
    private void HandleHealTargetPlayerByAmount(string receivedAloeId, int healAmount)
    {
        if (_aloeId != receivedAloeId) return;
        _targetPlayer.health += healAmount;
        if (HUDManager.Instance.localPlayer == _targetPlayer) HUDManager.Instance.UpdateHealthUI(GameNetworkManager.Instance.localPlayerController.health, false);
        LogDebug($"Target player health after last heal: {_targetPlayer.health}");
    }

    /// <summary>
    /// Sets the target player up to be in captivity.
    /// It will muffle the player, drop all their items and freeze them.
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="setToInCaptivity">Whether to make them captive or not</param>
    private void HandleSetTargetPlayerInCaptivity(string receivedAloeId, bool setToInCaptivity)
    {
        if (_aloeId != receivedAloeId) return;
        if (_targetPlayer == null) return;

        if (setToInCaptivity)
        {
            _targetPlayer.inSpecialInteractAnimation = true;
            _targetPlayer.DropAllHeldItemsAndSync();
            HandleMuffleTargetPlayerVoice(_aloeId);
        }
        else
        {
            _targetPlayer.inSpecialInteractAnimation = false;
            HandleUnMuffleTargetPlayerVoice(_aloeId);
        }
        
        LogDebug($"Set target player in captivity: {setToInCaptivity}");
    }

    /// <summary>
    /// Handles when the target player is able to escape by mashing their space bar.
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="canEscape">Whether to make the target player able to escape or not</param>
    private void HandleSetTargetPlayerAbleToEscape(string receivedAloeId, bool canEscape)
    {
        if (_aloeId != receivedAloeId) return;
        _targetPlayerCanEscape = canEscape;
        _targetPlayer.inSpecialInteractAnimation = true;
        
        if (canEscape && HUDManager.Instance.localPlayer == _targetPlayer) HUDManager.Instance.DisplayTip("You can escape!", "Mash the spacebar to escape from the aloe!");
        LogDebug("Set target player able to escape");
    }

    /// <summary>
    /// Increases the given player's fear level
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="targetInsanity"></param>
    /// <param name="playerClientId"></param>
    private void HandleIncreasePlayerFearLevel(string receivedAloeId, float targetInsanity, ulong playerClientId)
    {
        if (_aloeId != receivedAloeId) return;
        if (GameNetworkManager.Instance.localPlayerController != StartOfRound.Instance.allPlayerScripts[playerClientId])
            return;
        
        GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(targetInsanity);
        GameNetworkManager.Instance.localPlayerController.IncreaseFearLevelOverTime(0.8f);
    }

    /// <summary>
    /// Muffles the target player's voice.
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    private void HandleMuffleTargetPlayerVoice(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        
        if (_targetPlayer.currentVoiceChatAudioSource == null) StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
        if (_targetPlayer.currentVoiceChatAudioSource == null) return;
        
        LogDebug($"Muffling {_targetPlayer.name}");
        _targetPlayer.currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 5f;
        OccludeAudio component = _targetPlayer.currentVoiceChatAudioSource.GetComponent<OccludeAudio>();
        component.overridingLowPass = true;
        component.lowPassOverride = 500f;
        _targetPlayer.voiceMuffledByEnemy = true;
    }
    
    /// <summary>
    /// Un-muffles the target player's voice.
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    private void HandleUnMuffleTargetPlayerVoice(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        
        if (_targetPlayer.currentVoiceChatAudioSource == null) StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
        if (_targetPlayer.currentVoiceChatAudioSource == null) return;
        
        LogDebug($"UnMuffling {_targetPlayer.name}");
        _targetPlayer.currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 1f;
        OccludeAudio component = _targetPlayer.currentVoiceChatAudioSource.GetComponent<OccludeAudio>();
        component.overridingLowPass = false;
        component.lowPassOverride = 20000f;
        _targetPlayer.voiceMuffledByEnemy = false;
    }

    /// <summary>
    /// Plays the healing effect on the target player.
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="totalHealingTime">The total time the healing vfx should play for</param>
    private void HandlePlayHealingVfx(string receivedAloeId, float totalHealingTime)
    {
        if (_aloeId != receivedAloeId) return;
        
        // Set the duration of the vfx
        healingOrbEffect.Stop();
        healingOrbEffect.SetFloat("Duration", totalHealingTime);
        
        // Make the vfx go to the target player
        healingOrbEffect.gameObject.transform.position = _targetPlayer.lowerSpine.transform.position;
        
        // Play the vfx and audio
        LogDebug("Playing HealingOrbVfx");
        healingOrbEffect.SendEvent("OnShowHealingOrb");
        HandlePlayAudioClipType(receivedAloeId, AudioClipTypes.Healing, 0, true);
    }
    
    /// <summary>
    /// Changes the target player to the player with the given playerObjectId.
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    /// <param name="targetPlayerObjectId">The target player's object ID</param>
    private void HandleChangeTargetPlayer(string receivedAloeId, ulong targetPlayerObjectId)
    {
        if (_aloeId != receivedAloeId) return;
        if (targetPlayerObjectId == 69420)
        {
            _targetPlayer = null;
            return;
        }
        
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[targetPlayerObjectId];
        _targetPlayer = player;
    }

    /// <summary>
    /// Handles what happens when the aloe is dead.
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    private void HandleEnterDeathState(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        
        poofParticleSystem.Play();
        bodyRenderer.enabled = false;
        leavesRenderer.enabled = false;
        petalsRenderer.enabled = false;
        
        aloeVoiceSource.Stop(true);
        aloeFootstepsSource.Stop(true);
        
        Destroy(collider.gameObject);
        Destroy(scanNode.gameObject);
    }

    /// <summary>
    /// Sets a bool animation parameter to the given value
    /// </summary>
    /// <param name="receivedAloeId"></param>
    /// <param name="parameter">The name of the parameter in the animator</param>
    /// <param name="value">The bool value to set it to</param>
    private void SetBool(string receivedAloeId, int parameter, bool value)
    {
        if (_aloeId != receivedAloeId) return;
        animator.SetBool(parameter, value);
    }

    /// <summary>
    /// Sets a trigger in the animator
    /// </summary>
    /// <param name="receivedAloeId"></param>
    /// <param name="parameter">The name of the trigger in the animator</param>
    private void SetTrigger(string receivedAloeId, int parameter)
    {
        if (_aloeId != receivedAloeId) return;
        animator.SetTrigger(parameter);
    }

    /// <summary>
    /// Sets the configurable variables to their value in the player's config
    /// </summary>
    /// <param name="receivedAloeId">The aloe id</param>
    private void HandleInitializeConfigValues(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;

        escapeChargePerPress = Config.EscapeChargePerPress;
        escapeChargeDecayRate = Config.EscapeChargeDecayRate;
        escapeChargeThreshold = Config.EscapeChargeThreshold;
        skinMetallicTransitionTime = Config.DarkSkinTransitionTime;
    }

    /// <summary>
    /// Changes the behaviour state to the given state
    /// </summary>
    /// <param name="receivedAloeId">The aloe Id</param>
    /// <param name="newBehaviourStateIndex">The behaviour state to change to</param>
    private void HandleChangeBehaviourStateIndex(string receivedAloeId, int newBehaviourStateIndex)
    {
        if (_aloeId != receivedAloeId) return;
        _currentBehaviourStateIndex = newBehaviourStateIndex;
    }

    /// <summary>
    /// Syncs the Aloe id with the server
    /// </summary>
    /// <param name="id">The aloe id</param>
    private void HandleSyncAloeId(string id)
    {
        _aloeId = id;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource($"Biodiversity | Aloe Client {_aloeId}");
        
        LogDebug("Successfully synced aloe id");
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