using System;
using System.Collections;
using BepInEx.Logging;
using Biodiversity.Creatures.Aloe.CustomStateMachineBehaviours;
using Unity.Netcode;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.VFX;
using Logger = BepInEx.Logging.Logger;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Aloe;

public class AloeClient : MonoBehaviour
{
    private ManualLogSource _mls;
    private string _aloeId;
    
    private static readonly int Metallic = Shader.PropertyToID("_Metallic");
    private static readonly int BaseColour = Shader.PropertyToID("_BaseColour");

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Dead = Animator.StringToHash("Dead");
    public static readonly int Crawling = Animator.StringToHash("Crawling");
    public static readonly int Stunned = Animator.StringToHash("Stunned");
    public static readonly int Spotted = Animator.StringToHash("Spotted");
    public static readonly int Healing = Animator.StringToHash("Healing");
    public static readonly int Grab = Animator.StringToHash("Grab");
    public static readonly int KidnapRun = Animator.StringToHash("KidnapRun");

    public const float SnatchAndGrabAudioLength = 2.019f;

    public enum AudioClipTypes
    {
        Stun,
        Chase,
        CrackNeck,
        Healing,
        InterruptedHealing,
        SnatchAndDrag,
        Steps,
        Hit,
    }
    
#if !UNITY_EDITOR
    [field: HideInInspector] [field: SerializeField] public AloeConfig Config { get; private set; } = AloeHandler.Instance.Config;
#endif
    
#pragma warning disable 0649
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioSource aloeVoiceSource;
    [SerializeField] private AudioSource aloeFootstepsSource;
    public AudioClip[] stunSfx;
    public AudioClip[] chaseSfx;
    public AudioClip[] crackNeckSfx;
    public AudioClip[] healingSfx;
    public AudioClip[] interruptedHealingSfx;
    public AudioClip[] snatchAndDragSfx;
    public AudioClip[] stepsSfx;
    public AudioClip[] hitSfx;
    
    [Header("Renderers and Materials")] [Space(5f)]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Renderer petalsRenderer;
    [SerializeField] private MaterialPropertyBlock _propertyBlock;
    [SerializeField] private float skinMetallicTransitionTime = 7.5f;
    [SerializeField] private float skinMetallicPropertyValue = 0.735f;
    
    [Header("Visual Effects")] [Space(5f)]
    [SerializeField] private VisualEffect healingOrbEffect;
    [SerializeField] private ParticleSystem poofParticleSystem;
    
    [Header("Animation")] [Space(5f)]
    [SerializeField] private Animator animator;
    [SerializeField] private Rig lookAimRig;

    [Header("Transforms")] [Space(5f)] 
    [SerializeField] private Transform lookTarget;
    [SerializeField] private Transform grabTarget;
    
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private AloeNetcodeController netcodeController;
    
    [Header("Other")] [Space(5f)]
    [SerializeField] private GameObject scanNode;
#pragma warning restore 0649
    
    [Header("Settings")] [Space(5f)]
    [SerializeField] private float escapeChargePerPress = 15f;
    [SerializeField] private float escapeChargeDecayRate = 15f;
    [SerializeField] private float escapeChargeThreshold = 100f;
    [SerializeField] private float lookBlendDuration = 0.5f;
    
    private PlayerControllerB _targetPlayer;

    private FakePlayerBodyRagdoll _currentFakePlayerBodyRagdoll;
    
    private Vector3 _agentLastPosition;

    private Coroutine _blendLookAimConstraintWeightCoroutine;
    private Coroutine _changeSkinColourCoroutine;
    
    private bool _targetPlayerCanEscape;

    private float _currentEscapeChargeValue;
    private float _agentCurrentSpeed;
    
    private int _currentBehaviourStateIndex;

    /// <summary>
    /// Subscribe to the needed network events.
    /// </summary>
    private void OnEnable()
    {
        if (netcodeController == null) return;
        netcodeController.OnSyncAloeId += HandleSyncAloeId;
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
        netcodeController.OnSetTrigger += HandleSetTrigger;
        netcodeController.OnResetTrigger += HandleResetTrigger;
        netcodeController.OnChangeAnimationParameterBool += HandleSetBool;
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
        netcodeController.OnChangeLookAimConstraintWeight += HandleChangeLookAimConstraintWeight;
        netcodeController.OnSpawnFakePlayerBodyRagdoll += HandleSpawnFakePlayerBodyRagdoll;
        netcodeController.OnTransitionToRunningForwardsAndCarryingPlayer +=
            HandleTransitionToRunningForwardsAndCarryingPlayer;
    }

    /// <summary>
    /// Unsubscribe to the network events when the creature is dead.
    /// </summary>
    private void OnDisable()
    {
        if (netcodeController == null) return;
        netcodeController.OnSyncAloeId -= HandleSyncAloeId;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
        netcodeController.OnSetTrigger -= HandleSetTrigger;
        netcodeController.OnResetTrigger -= HandleResetTrigger;
        netcodeController.OnChangeAnimationParameterBool -= HandleSetBool;
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
        netcodeController.OnChangeLookAimConstraintWeight -= HandleChangeLookAimConstraintWeight;
        netcodeController.OnSpawnFakePlayerBodyRagdoll -= HandleSpawnFakePlayerBodyRagdoll;
        netcodeController.OnTransitionToRunningForwardsAndCarryingPlayer -=
            HandleTransitionToRunningForwardsAndCarryingPlayer;
    }

    private void Awake()
    {
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Client");
        if (netcodeController == null) netcodeController = GetComponent<AloeNetcodeController>();
    }

    private void Start()
    {
        _propertyBlock = new MaterialPropertyBlock();
        lookAimRig.weight = 0f;
        
        AddStateMachineBehaviours(animator);
    }

    /// <summary>
    /// This function is called every frame
    /// </summary>
    private void Update()
    {
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime, 0.75f);
        _agentLastPosition = position;
        animator.SetFloat(Speed, _agentCurrentSpeed);
        
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
                    healingOrbEffect.Stop();
                    netcodeController.TargetPlayerEscapedServerRpc(_aloeId);
                }
                
                break;
            }
        }
    }

    private void LateUpdate()
    {
        // Todo: Limit the amount of times the look target is repositioned
        if (_targetPlayer != null)
        {
            lookTarget.position = _targetPlayer.gameplayCamera.transform.position;
        }
    }

    /// <summary>
    /// Handles what should happen to make the Aloe snap a player's neck
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="playerClientId">The player's client id whose neck will be snapped.</param>
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
    /// Blends the look multi-aim constraint weight parameter over a specific duration
    /// </summary>
    /// <param name="startWeight">The start weight of the blend.</param>
    /// <param name="endWeight">The end weight of the blend.</param>
    /// <param name="duration">The duration of the blend.</param>
    /// <returns></returns>
    private IEnumerator BlendLookAimConstraintWeight(float startWeight, float endWeight, float duration)
    {
        float elapsed = 0f;
        
        // Exit early if the start and end weights are the same
        if (Mathf.Approximately(startWeight, endWeight)) yield break;

        while (elapsed < duration)
        {
            lookAimRig.weight = Mathf.Lerp(startWeight, endWeight, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        lookAimRig.weight = endWeight;
        _blendLookAimConstraintWeightCoroutine = null;
    }
    
    /// <summary>
    /// Plays an audio clip with the given type and index
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="audioClipType">The audio clip type to play.</param>
    /// <param name="clipIndex">The index of the clip in their respective AudioClip array to play.</param>
    /// <param name="interrupt">Whether to interrupt any previously playing sound before playing the new audio.</param>
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
            AudioClipTypes.Hit => hitSfx[clipIndex],
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
    /// Handles what should happen to make the Aloe change her skin colour
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="toDark">Whether the skin colour is going to a dark colour or not.</param>
    private void HandleChangeAloeSkinColour(string receivedAloeId, bool toDark)
    {
        if (_aloeId != receivedAloeId) return;
        if (bodyRenderer == null)
        {
            LogDebug("Body renderer is null, cannot change aloe skin colour");
            return;
        }
        
        if (_changeSkinColourCoroutine != null) StopCoroutine(ChangeAloeSkinColour(toDark));
        _propertyBlock ??= new MaterialPropertyBlock();
        bodyRenderer.GetPropertyBlock(_propertyBlock);
        _changeSkinColourCoroutine = StartCoroutine(ChangeAloeSkinColour(toDark));
    }

    /// <summary>
    /// A coroutine for smoothly transitioning the Aloe's skin colour
    /// </summary>
    /// <param name="toDark">Whether her skin colour is going to a dark colour or not.</param>
    /// <returns></returns>
    private IEnumerator ChangeAloeSkinColour(bool toDark)
    {
        LogDebug($"Changing Aloe's skin to a {(toDark ? "dark" : "light")} colour");
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
            
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        
        // Ensure the final value is set exactly at the end
        _propertyBlock.SetFloat(Metallic, endMetallicValue);
        _propertyBlock.SetColor(BaseColour, HSVToRGB(h, s, endV));
        bodyRenderer.SetPropertyBlock(_propertyBlock);
        _changeSkinColourCoroutine = null;
    }

    /// <summary>
    /// Heals the target player by the given amount.
    /// It also updates their health bar.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="healAmount">The amount to heal the target player by.</param>
    private void HandleHealTargetPlayerByAmount(string receivedAloeId, int healAmount)
    {
        if (_aloeId != receivedAloeId) return;
        _targetPlayer.health += healAmount;
        if (HUDManager.Instance.localPlayer == _targetPlayer) HUDManager.Instance.UpdateHealthUI(GameNetworkManager.Instance.localPlayerController.health, false);
        LogDebug($"Target player health after last heal: {_targetPlayer.health}");
    }

    private void HandleTransitionToRunningForwardsAndCarryingPlayer(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        LogDebug($"In {nameof(HandleTransitionToRunningForwardsAndCarryingPlayer)}");

        if (_currentFakePlayerBodyRagdoll == null)
        {
            _mls.LogError("The player body ragdoll is null, this should never happen.");
            return;
        }
        
        _currentFakePlayerBodyRagdoll.DetachLimbFromTransform("Neck");
        _currentFakePlayerBodyRagdoll.AttachLimbToTransform("Root", grabTarget);
    }

    private void HandleSpawnFakePlayerBodyRagdoll(string receivedAloeId,
        NetworkObjectReference fakePlayerBodyRagdollNetworkObjectReference)
    {
        if (_aloeId != receivedAloeId) return;
        if (_currentFakePlayerBodyRagdoll != null) Destroy(_currentFakePlayerBodyRagdoll.gameObject);
        if (!fakePlayerBodyRagdollNetworkObjectReference.TryGet(out NetworkObject fakePlayerBodyRagdollNetworkObject))
            return;

        LogDebug($"In {nameof(HandleSpawnFakePlayerBodyRagdoll)}");
        _currentFakePlayerBodyRagdoll = fakePlayerBodyRagdollNetworkObject.GetComponent<FakePlayerBodyRagdoll>();
        if (_currentFakePlayerBodyRagdoll == null)
        {
            _mls.LogError("FakePlayerBodyRagdoll script is null on the ragdoll gameobject. This should never happen.");
            return;
        }

        _currentFakePlayerBodyRagdoll.AttachLimbToTransform("Neck", grabTarget);
    }

    /// <summary>
    /// Sets the target player up to be in captivity.
    /// It will muffle the player, drop all their items and freeze them.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="setToInCaptivity">Whether to make them captive or not.</param>
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
            if (_currentFakePlayerBodyRagdoll != null)
            {
                Destroy(_currentFakePlayerBodyRagdoll.gameObject);
                _currentFakePlayerBodyRagdoll = null;
            }
        }
        
        LogDebug($"Set {_targetPlayer.playerUsername} in captivity: {setToInCaptivity}");
    }

    /// <summary>
    /// Handles when the target player is able to escape by mashing their space bar.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="canEscape">Whether to make the target player able to escape or not.</param>
    private void HandleSetTargetPlayerAbleToEscape(string receivedAloeId, bool canEscape)
    {
        if (_aloeId != receivedAloeId) return;
        if (_targetPlayer == null) return;
        _targetPlayerCanEscape = canEscape;
        _targetPlayer.inSpecialInteractAnimation = true;
        
        if (canEscape && HUDManager.Instance.localPlayer == _targetPlayer) HUDManager.Instance.DisplayTip("You can escape!", "Mash the spacebar to escape from The Aloe!");
        LogDebug($"Set {_targetPlayer.playerUsername} able to escape");
    }

    /// <summary>
    /// Increases the given player's fear level
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="targetInsanity">.</param>
    /// <param name="playerClientId">.</param>
    private void HandleIncreasePlayerFearLevel(string receivedAloeId, float targetInsanity, ulong playerClientId)
    {
        if (_aloeId != receivedAloeId) return;
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerClientId];
        player.JumpToFearLevel(targetInsanity);
        player.IncreaseFearLevelOverTime(0.8f);
    }

    /// <summary>
    /// Muffles the target player's voice.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleMuffleTargetPlayerVoice(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        
        if (_targetPlayer.currentVoiceChatAudioSource == null) StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
        if (_targetPlayer.currentVoiceChatAudioSource == null) return;
        
        LogDebug($"Muffling {_targetPlayer.playerUsername}");
        _targetPlayer.currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 5f;
        OccludeAudio component = _targetPlayer.currentVoiceChatAudioSource.GetComponent<OccludeAudio>();
        component.overridingLowPass = true;
        component.lowPassOverride = 500f;
        _targetPlayer.voiceMuffledByEnemy = true;
    }
    
    /// <summary>
    /// Un-muffles the target player's voice.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleUnMuffleTargetPlayerVoice(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        
        if (_targetPlayer.currentVoiceChatAudioSource == null) StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
        if (_targetPlayer.currentVoiceChatAudioSource == null) return;
        
        LogDebug($"UnMuffling {_targetPlayer.playerUsername}");
        _targetPlayer.currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 1f;
        OccludeAudio component = _targetPlayer.currentVoiceChatAudioSource.GetComponent<OccludeAudio>();
        component.overridingLowPass = false;
        component.lowPassOverride = 20000f;
        _targetPlayer.voiceMuffledByEnemy = false;
    }

    /// <summary>
    /// Plays the healing effect on the target player.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="totalHealingTime">The total time the healing vfx should play for.</param>
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
    /// Grabs the target player at the end of the grab player animation
    /// </summary>
    public void OnAnimationEventGrabPlayer()
    {
        if (!NetworkManager.Singleton.IsServer || !netcodeController.IsOwner) return;
        netcodeController.GrabTargetPlayerServerRpc(_aloeId);
    }

    /// <summary>
    /// Tells the server that the spotted animation is complete
    /// </summary>
    public void OnAnimationEventSpottedAnimationComplete()
    {
        if (!NetworkManager.Singleton.IsServer || !netcodeController.IsOwner) return;
        LogDebug($"In {nameof(OnAnimationEventSpottedAnimationComplete)}");
        netcodeController.SpottedAnimationCompleteServerRpc(_aloeId);
    }

    /// <summary>
    /// Plays a random footstep sound effect when the Aloe's foot touches the ground in an animation.
    /// </summary>
    public void OnAnimationEventPlayFootstepSfx()
    {
        AudioClip audioClipToPlay = stepsSfx[Random.Range(0, stepsSfx.Length)];
        aloeFootstepsSource.PlayOneShot(audioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(aloeFootstepsSource, audioClipToPlay, aloeFootstepsSource.volume);
    }
    
    /// <summary>
    /// Changes the look aim constraint weight to the specified value.
    /// The look aim constraint is used for making the Aloe look at something with her head bone.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="endWeight">The weight to blend to.</param>
    /// <param name="duration">The duration of the blend.</param>
    private void HandleChangeLookAimConstraintWeight(string receivedAloeId, float endWeight, float duration = -1f)
    {
        if (_aloeId != receivedAloeId) return;
        if (duration < 0f) duration = lookBlendDuration;
        LogDebug($"Changing look aim constraint weight from {lookAimRig.weight} to {endWeight} in {duration} seconds blend time.");
        if (_blendLookAimConstraintWeightCoroutine != null) StopCoroutine(_blendLookAimConstraintWeightCoroutine);
        _blendLookAimConstraintWeightCoroutine = 
            StartCoroutine(BlendLookAimConstraintWeight(lookAimRig.weight, endWeight, duration));
    }
    
    /// <summary>
    /// Changes the target player to the player with the given playerObjectId.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="targetPlayerObjectId">The target player's object ID.</param>
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
        LogDebug($"Changed target player to {_targetPlayer.playerUsername}");
    }

    /// <summary>
    /// Handles what happens when the aloe is dead.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleEnterDeathState(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        LogDebug("Entering death state");
        animator.SetBool(Dead, true);
    }

    /// <summary>
    /// An animation event that gets triggered near the end of the death animation.
    /// The function plays the poof particle effect and makes the Aloe invisible
    /// </summary>
    public void OnAnimationEventPlayPoofParticleEffect()
    {
        LogDebug($"In {nameof(OnAnimationEventPlayPoofParticleEffect)}");
        poofParticleSystem.Play();
        
        bodyRenderer.enabled = false;
        petalsRenderer.enabled = false;
        
        aloeVoiceSource.Stop(true);
        aloeFootstepsSource.Stop(true);
        
        Destroy(scanNode.gameObject);
        StartCoroutine(DestroyAloeObjectAfterDuration(poofParticleSystem.main.duration));
    }
    
    /// <summary>
    /// Destroys the entire Aloe object after a duration
    /// </summary>
    private IEnumerator DestroyAloeObjectAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        LogDebug("Destroying gameobject");
        Destroy(gameObject);
    }
    
    private void AddStateMachineBehaviours(Animator receivedAnimator)
    {
        StateMachineBehaviour[] behaviours = receivedAnimator.GetBehaviours<StateMachineBehaviour>();
        foreach (StateMachineBehaviour behaviour in behaviours)
        {
            if (behaviour is BaseStateMachineBehaviour baseStateMachineBehaviour)
            {
                baseStateMachineBehaviour.Initialize(netcodeController);
            }
        }
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
    /// Sets a bool animation parameter to the given value
    /// </summary>
    /// <param name="receivedAloeId">.</param>
    /// <param name="parameter">The name of the parameter in the animator.</param>
    /// <param name="value">The bool value to set it to.</param>
    private void HandleSetBool(string receivedAloeId, int parameter, bool value)
    {
        if (_aloeId != receivedAloeId) return;
        animator.SetBool(parameter, value);
    }

    /// <summary>
    /// Sets a trigger in the animator
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="parameter">The name of the trigger in the animator.</param>
    private void HandleSetTrigger(string receivedAloeId, int parameter)
    {
        if (_aloeId != receivedAloeId) return;
        animator.SetTrigger(parameter);
    }

    /// <summary>
    /// Resets a trigger in the animator
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="parameter">The name of the trigger in the animator.</param>
    private void HandleResetTrigger(string receivedAloeId, int parameter)
    {
        if (_aloeId != receivedAloeId) return;
        animator.ResetTrigger(parameter);
    }

    /// <summary>
    /// Sets the configurable variables to their value in the player's config
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
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
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="newBehaviourStateIndex">The behaviour state to change to.</param>
    private void HandleChangeBehaviourStateIndex(string receivedAloeId, int newBehaviourStateIndex)
    {
        if (_aloeId != receivedAloeId) return;
        _currentBehaviourStateIndex = newBehaviourStateIndex;
    }

    /// <summary>
    /// Syncs The Aloe ID with the server
    /// </summary>
    /// <param name="id">The Aloe ID.</param>
    private void HandleSyncAloeId(string id)
    {
        _aloeId = id;
        _mls?.Dispose();
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Client {_aloeId}");
        
        LogDebug("Successfully synced aloe id");
    }
    
    /// <summary>
    /// Only logs the given message if the assembly version is in debug, not release
    /// </summary>
    /// <param name="msg">The debug message to log.</param>
    private void LogDebug(string msg)
    {
        #if DEBUG
        _mls?.LogInfo(msg);
        #endif
    }
}