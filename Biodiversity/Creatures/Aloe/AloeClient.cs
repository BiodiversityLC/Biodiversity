using System;
using System.Collections;
using System.Collections.Generic;
using Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;
using Biodiversity.Util;
using Biodiversity.Util.Lang;
using Biodiversity.Util.DataStructures;
using Unity.Netcode;
using GameNetcodeStuff;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Aloe;

public class AloeClient : MonoBehaviour
{
    private static readonly int Metallic = Shader.PropertyToID("_Metallic");
    private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int Dead = Animator.StringToHash("Dead");
    private static readonly int Spawning = Animator.StringToHash("Spawning");
    public static readonly int Stand = Animator.StringToHash("Stand");
    private static readonly int Crawling = Animator.StringToHash("Crawling");
    private static readonly int Stunned = Animator.StringToHash("Stunned");
    public static readonly int Spotted = Animator.StringToHash("Spotted");
    private static readonly int Healing = Animator.StringToHash("Healing");
    public static readonly int Grab = Animator.StringToHash("Grab");
    public static readonly int KidnapRun = Animator.StringToHash("KidnapRun");
    public static readonly int Slap = Animator.StringToHash("Slap");
    private static readonly int Crush = Animator.StringToHash("Crush");
    
    private static readonly int PlayerCrouching = Animator.StringToHash("crouching");

    public const float SnatchAndGrabAudioLength = 2.019f;

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum AudioClipTypes
    {
        stunSfx,
        chaseSfx,
        crackNeckSfx,
        healingSfx,
        interruptedHealingSfx,
        snatchAndDragSfx,
        stepsSfx,
        hitSfx,
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum AudioSourceTypes
    {
        aloeVoiceSource,
        aloeFootstepsSource
    }

    private enum TargetPlayerOffsetType
    {
        Dragged,
        Carried,
        Cuddled,
    }

    private readonly Dictionary<TargetPlayerOffsetType, Tuple<Vector3, Quaternion>> _targetPlayerOffsetDictionary =
        new()
        {
            {
                TargetPlayerOffsetType.Dragged, new Tuple<Vector3, Quaternion>(
                    new Vector3(0f, 0.5f, 2.7f), new Quaternion(-0.70711f, 0f, 0f, 0.70711f))
            },
            {
                TargetPlayerOffsetType.Carried, new Tuple<Vector3, Quaternion>(
                    new Vector3(0.2f, 0.4f, 0.9f), new Quaternion(0f, 0f, 0f, 1f))
            },
            {
                TargetPlayerOffsetType.Cuddled, new Tuple<Vector3, Quaternion>(
                    new Vector3(-2f, 1.2f, 0.5f), new Quaternion(0.5f, 0.5f, 0.5f, -0.5f))
            }
        };

    private readonly List<Tuple<string, Type>> _playerRendererObjects = 
    [
        new("LOD1", typeof(SkinnedMeshRenderer)),
        new("LOD2", typeof(SkinnedMeshRenderer)),
        new("LOD3", typeof(SkinnedMeshRenderer)),
        new("LevelSticker", typeof(MeshRenderer)),
        new("BetaBadge", typeof(MeshRenderer)),
        // new Tuple<string, Type>("Circle", typeof(SkinnedMeshRenderer)),
        // new Tuple<string, Type>("CopyHeldProp", typeof(MeshRenderer)),
        // new Tuple<string, Type>("PlayerPhysicsBox", typeof(MeshRenderer)),
        // new Tuple<string, Type>("LineOfSightCube", typeof(MeshRenderer)),
        // new Tuple<string, Type>("LineOfSightCubeSmall", typeof(MeshRenderer)),
        // new Tuple<string, Type>("LineOfSight2", typeof(MeshRenderer)),
    ];

#pragma warning disable 0649
    [Header("Audio")] [Space(5f)] 
    [Preserve] [SerializeField] private AudioSource aloeVoiceSource;
    [Preserve] [SerializeField] private AudioSource aloeFootstepsSource;
    [Preserve] [SerializeField] public AudioClip[] stunSfx;
    [Preserve] [SerializeField] public AudioClip[] chaseSfx;
    [Preserve] [SerializeField] public AudioClip[] crackNeckSfx;
    [Preserve] [SerializeField] public AudioClip[] healingSfx;
    [Preserve] [SerializeField] public AudioClip[] interruptedHealingSfx;
    [Preserve] [SerializeField] public AudioClip[] snatchAndDragSfx;
    [Preserve] [SerializeField] public AudioClip[] stepsSfx;
    [Preserve] [SerializeField] public AudioClip[] hitSfx;

    [Header("Renderers and Materials")] [Space(5f)] 
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Renderer petalsRenderer;
    [SerializeField] private MaterialPropertyBlock _propertyBlock;
    [SerializeField] private float skinMetallicTransitionTime = 7.5f;
    [SerializeField] private float skinMetallicValueDark = 0.735f;

    [Header("Visual Effects")] [Space(5f)] 
    [SerializeField] private Light healingLightEffect;
    [SerializeField] private ParticleSystem poofParticleSystem;
    
#pragma warning disable CS0169
    [SerializeField] private VisualEffect healingOrbEffect;
#pragma warning restore CS0169

    [Header("Animation")] [Space(5f)] 
    [SerializeField] private Animator animator;
    [SerializeField] private Rig lookAimRig;
    [SerializeField] private Transform ragdollGrabTarget;

    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private AloeNetcodeController netcodeController;
    public SlapCollisionDetection slapCollisionDetection;

    [Header("Other")] [Space(5f)] 
    [SerializeField] private GameObject scanNode;
#pragma warning restore 0649

    [Header("Settings")] [Space(5f)] 
    [SerializeField] private float escapeChargePerPress = 15f;
    [SerializeField] private float escapeChargeDecayRate = 15f;
    [SerializeField] private float escapeChargeThreshold = 100f;

    private CachedNullable<PlayerControllerB> _targetPlayer;

    private CachedValue<AloeServerAI> _aloeServer;

    private PerKeyCachedDictionary<ulong, List<Tuple<Component, bool>>> _playersCachedRenderers;
    private PerKeyCachedDictionary<ulong, MeshRenderer> _playerVisorRenderers;
    private PerKeyCachedDictionary<ulong, AudioLowPassFilter> _playerAudioLowPassFilters;
    private PerKeyCachedDictionary<ulong, OccludeAudio> _playerOccludeAudios;
    
    private FakePlayerBodyRagdoll _currentFakePlayerBodyRagdoll;

    private Vector3 _agentLastPosition;
    private Vector3 _offsetPosition = Vector3.zero;

    private Quaternion _offsetRotation = Quaternion.identity;

    private Coroutine _changeSkinColourCoroutine;
    private Coroutine _changeTargetPlayerOffsets;

    private bool _targetPlayerCanEscape;
    private bool _targetPlayerInCaptivity;
    private bool _networkEventsSubscribed;

    private float _currentEscapeChargeValue;
    private float _agentCurrentSpeed;
    private float _lastFootstepTime;
    private float _defaultFoostepAudioSourcePitch;

    private void Awake()
    {
        if (netcodeController == null) netcodeController = GetComponent<AloeNetcodeController>();

        _aloeServer = new CachedValue<AloeServerAI>(GetComponent<AloeServerAI>);
        
        _playersCachedRenderers = new PerKeyCachedDictionary<ulong, List<Tuple<Component, bool>>>(playerId =>
        {
            List<Tuple<Component, bool>> rendererComponents = [];
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerId];
            
            if (player == null)
            {
                BiodiversityPlugin.Logger.LogError("Cannot get target player renderers because the target player variable is null.");
                return rendererComponents;
            }

            for (int i = 0; i < _playerRendererObjects.Count; i++)
            {
                Tuple<string, Type> rendererTuple = _playerRendererObjects[i];
                try
                {
                    Transform rendererTransform = player.transform.Find(rendererTuple.Item1);
                    if (rendererTransform == null)
                    {
                        BiodiversityPlugin.Logger.LogWarning($"Transform not found for renderer: {rendererTuple.Item1}");
                        continue;
                    }

                    //BiodiversityPlugin.LogVerbose($"Found transform for renderer: {rendererTuple.Item1}");

                    if (rendererTransform.GetComponent(rendererTuple.Item2) is Renderer rendererComponent)
                    {
                        //BiodiversityPlugin.LogVerbose($"Found {nameof(rendererComponent)} component for renderer: {rendererTuple.Item1}");
                        rendererComponents.Add(new Tuple<Component, bool>(rendererComponent, rendererComponent.enabled));
                    }
                    else
                    {
                        BiodiversityPlugin.Logger.LogWarning(
                            $"Component of type {rendererTuple.Item2} not found or incorrect type for renderer: {rendererTuple.Item1}");
                    }
                }
                catch (Exception ex)
                {
                    BiodiversityPlugin.Logger.LogError(
                        $"Error processing renderer: {rendererTuple.Item1} for player ID {playerId}: {ex.Message}");
                }
            }

            return rendererComponents;
        });
        
        _playerVisorRenderers = new PerKeyCachedDictionary<ulong, MeshRenderer>(playerId =>
            StartOfRound.Instance.allPlayerScripts[playerId].localVisor.gameObject
                .GetComponentsInChildren<MeshRenderer>()[0]);

        _playerAudioLowPassFilters = new PerKeyCachedDictionary<ulong, AudioLowPassFilter>(playerId =>
            StartOfRound.Instance.allPlayerScripts[playerId].currentVoiceChatAudioSource
                .GetComponent<AudioLowPassFilter>());

        _playerOccludeAudios = new PerKeyCachedDictionary<ulong, OccludeAudio>(playerId =>
            StartOfRound.Instance.allPlayerScripts[playerId].currentVoiceChatAudioSource.GetComponent<OccludeAudio>());
    }

    /// <summary>
    /// Subscribe to the needed network events.
    /// </summary>
    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    /// <summary>
    /// Unsubscribe to the network events when the creature is dead.
    /// </summary>
    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();

        if (_changeSkinColourCoroutine != null)
        {
            StopCoroutine(_changeSkinColourCoroutine);
            _changeSkinColourCoroutine = null;
        }

        if (_changeTargetPlayerOffsets != null)
        {
            StopCoroutine(_changeTargetPlayerOffsets);
            _changeTargetPlayerOffsets = null;
        }
        
        StopCoroutine(nameof(DisableHealingLightDelayed));

        healingLightEffect.enabled = false;
        CleanupRagdoll();
    }

    private void Start()
    {
        _propertyBlock = new MaterialPropertyBlock();
        lookAimRig.weight = 0f;

        AddStateMachineBehaviours(animator);

        animator.SetBool(Spawning, true);

        _defaultFoostepAudioSourcePitch = aloeFootstepsSource.pitch;
    }
    
    private void Update()
    {
        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime,
            0.75f);
        _agentLastPosition = position;

        animator.SetFloat(Speed, _agentCurrentSpeed + 0.1f);
        animator.SetBool(Healing, netcodeController.AnimationParamHealing.Value);
        animator.SetBool(Crawling, netcodeController.AnimationParamCrawling.Value);
        animator.SetBool(Stunned, netcodeController.AnimationParamStunned.Value);
        animator.SetBool(Dead, netcodeController.AnimationParamDead.Value);
        
        _lastFootstepTime += Time.deltaTime;
        _timeSinceTargetPlayerNullLog += Time.deltaTime;

        // ManuallyControlPlayerOffsets();

        if (_targetPlayerCanEscape)
        {
            switch (_aloeServer.Value.NetworkCurrentBehaviourStateIndex.Value)
            {
                case (int)AloeServerAI.States.HealingPlayer or (int)AloeServerAI.States.CuddlingPlayer:
                {
                    if (!_targetPlayer.HasValue) break;
                    if (GameNetworkManager.Instance.localPlayerController != _targetPlayer.Value) break;

                    if (Keyboard.current.spaceKey.wasPressedThisFrame)
                    {
                        BiodiversityPlugin.LogVerbose($"Space key was pressed, the new escape charge value is: {_currentEscapeChargeValue}");
                        _currentEscapeChargeValue += escapeChargePerPress;
                    }

                    if (_currentEscapeChargeValue > 0) _currentEscapeChargeValue -= escapeChargeDecayRate * Time.deltaTime;
                    else _currentEscapeChargeValue = 0;

                    if (_currentEscapeChargeValue >= escapeChargeThreshold)
                    {
                        BiodiversityPlugin.LogVerbose("Triggering aloe escape");
                        _currentEscapeChargeValue = 0;
                        _targetPlayerCanEscape = false;
                        netcodeController.TargetPlayerEscapedServerRpc();
                        
                        // healingOrbEffect.Stop();
                        healingLightEffect.enabled = false;
                    }

                    break;
                }
            }
        }
        else
        {
            _currentEscapeChargeValue = 0;  
        }
    }

    private void LateUpdate()
    {
        // Animate the real target player's body
        if (_targetPlayerInCaptivity && _targetPlayer.HasValue)
        {
            if (_aloeServer.Value.NetworkCurrentBehaviourStateIndex.Value is (int)AloeServerAI.States.KidnappingPlayer
                    or (int)AloeServerAI.States.HealingPlayer or (int)AloeServerAI.States.CuddlingPlayer &&
                _targetPlayer.Value.inSpecialInteractAnimation)
            {
                _targetPlayer.Value.transform.position = transform.position + transform.rotation * _offsetPosition;
                _targetPlayer.Value.transform.rotation = transform.rotation * _offsetRotation;
            }
            
            CorrectlySetTargetPlayerLocalRenderers(_targetPlayerInCaptivity, _targetPlayer.Value);
            UpdateRagdollVisibility();
        }
    }

    /// <summary>
    /// Handles what should happen to make the Aloe crush a player's head
    /// </summary>
    /// <param name="playerClientId">The player's client id whose head will be crushed.</param>
    private void HandleCrushPlayerAnimation(ulong playerClientId)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerClientId];
        if (player == null) return;

        StartCoroutine(CrushPlayerAnimation(player));
    }

    private IEnumerator CrushPlayerAnimation(PlayerControllerB player)
    {
        BiodiversityPlugin.LogVerbose($"Killing player: {player.name}");
        if (player.inSpecialInteractAnimation &&
            player.currentTriggerInAnimationWith != null)
            player.currentTriggerInAnimationWith.CancelAnimationExternally();

        player.isCrouching = false;
        player.playerBodyAnimator.SetBool(PlayerCrouching, false);
        yield return null;
        
        player.inSpecialInteractAnimation = true;
        player.inAnimationWithEnemy = _aloeServer.Value;
        player.isInElevator = false;
        player.isInHangarShipRoom = false;
        player.ResetZAndXRotation();
        player.DropAllHeldItemsAndSync();
        player.inSpecialInteractAnimation = true;
        player.transform.LookAt(transform.position);
        animator.SetTrigger(Crush);
        yield return new WaitForSeconds(0.3f);
        
        player.KillPlayer(Vector3.zero, true, CauseOfDeath.Crushing, 1);
    }
    
    private void HandleChangeAloeSkinColour(bool oldValue, bool newValue)
    {
        if (_changeSkinColourCoroutine != null) StopCoroutine(ChangeAloeSkinColour(newValue));
        _propertyBlock ??= new MaterialPropertyBlock();
        bodyRenderer.GetPropertyBlock(_propertyBlock);
        _changeSkinColourCoroutine = StartCoroutine(ChangeAloeSkinColour(newValue));
    }

    /// <summary>
    /// A coroutine for smoothly transitioning the Aloe's skin colour
    /// </summary>
    /// <param name="toDark">Whether her skin colour is going to a dark colour or not.</param>
    /// <returns></returns>
    private IEnumerator ChangeAloeSkinColour(bool toDark)
    {
        BiodiversityPlugin.LogVerbose($"Changing Aloe's skin to a {(toDark ? "dark" : "light")} colour");
        float timeElapsed = 0f;
        float startMetallicValue = toDark ? 0 : skinMetallicValueDark;
        float endMetallicValue = toDark ? skinMetallicValueDark : 0;

        Color currentColour = bodyRenderer.material.GetColor(BaseColour);
        ExtensionMethods.RGBToHSV(currentColour, out float h, out float s, out float v);
        float endV = toDark ? 0.5f : 1f;

        // Early exit if the skin colour is already the desired colour
        if (Mathf.Approximately(v, endV) && Mathf.Approximately(bodyRenderer.material.GetFloat(Metallic), endMetallicValue))
        {
            _changeSkinColourCoroutine = null;
            yield break;
        }

        while (timeElapsed < skinMetallicTransitionTime)
        {
            float t = timeElapsed / skinMetallicTransitionTime;
            float currentMetallicValue = Mathf.Lerp(startMetallicValue, endMetallicValue, t);
            float currentV = Mathf.Lerp(v, endV, t);
            Color newColour = ExtensionMethods.HSVToRGB(h, s, currentV);

            _propertyBlock.SetFloat(Metallic, currentMetallicValue);
            _propertyBlock.SetColor(BaseColour, newColour);
            bodyRenderer.SetPropertyBlock(_propertyBlock);

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure the final value is set exactly at the end
        _propertyBlock.SetFloat(Metallic, endMetallicValue);
        _propertyBlock.SetColor(BaseColour, ExtensionMethods.HSVToRGB(h, s, endV));
        bodyRenderer.SetPropertyBlock(_propertyBlock);
        
        _changeSkinColourCoroutine = null;
    }

    /// <summary>
    /// Heals the target player by the given amount.
    /// It also updates their health bar.
    /// </summary>
    /// <param name="healAmount">The amount to heal the target player by.</param>
    private void HandleHealTargetPlayerByAmount(int healAmount)
    {
        if (!_targetPlayer.HasValue) return;
        
        _targetPlayer.Value.health += healAmount;
        if (HUDManager.Instance.localPlayer == _targetPlayer.Value)
        {
            HUDManager.Instance.UpdateHealthUI(GameNetworkManager.Instance.localPlayerController.health, false);
            _targetPlayer.Value.MakeCriticallyInjured(false);
        }
            
        BiodiversityPlugin.LogVerbose($"Target player health after last heal: {_targetPlayer.Value.health}");
    }

    private IEnumerator ChangeTargetPlayerOffsets(TargetPlayerOffsetType offsetType, float duration = 0.5f)
    {
        (Vector3 newPositionOffset, Quaternion newRotationOffset) = _targetPlayerOffsetDictionary[offsetType];
        Vector3 initialPositionOffset = _offsetPosition;
        Quaternion initialRotationOffset = _offsetRotation;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            _offsetPosition = Vector3.Lerp(initialPositionOffset, newPositionOffset, t);
            _offsetRotation = Quaternion.Slerp(initialRotationOffset, newRotationOffset, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        _offsetPosition = newPositionOffset;
        _offsetRotation = newRotationOffset;
    }

    private void HandleTransitionToRunningForwardsAndCarryingPlayer(float transitionDuration)
    {
        BiodiversityPlugin.LogVerbose($"In {nameof(HandleTransitionToRunningForwardsAndCarryingPlayer)}");

        if (_currentFakePlayerBodyRagdoll == null)
        {
            BiodiversityPlugin.Logger.LogError("The player body ragdoll is null, this should never happen.");
            return;
        }

        if (_changeTargetPlayerOffsets != null) StopCoroutine(_changeTargetPlayerOffsets);
        _changeTargetPlayerOffsets = StartCoroutine(
            ChangeTargetPlayerOffsets(TargetPlayerOffsetType.Carried, transitionDuration));

        _currentFakePlayerBodyRagdoll.DetachLimbFromTransform("Neck");
        _currentFakePlayerBodyRagdoll.AttachLimbToTransform("Root", ragdollGrabTarget);
    }

    private void HandleSpawnFakePlayerBodyRagdoll(NetworkObjectReference fakePlayerBodyRagdollNetworkObjectReference)
    {
        BiodiversityPlugin.LogVerbose($"In {nameof(HandleSpawnFakePlayerBodyRagdoll)}");
        
        CleanupRagdoll();

        if (!fakePlayerBodyRagdollNetworkObjectReference.TryGet(out NetworkObject fakePlayerBodyRagdollNetworkObject))
        {
            BiodiversityPlugin.Logger.LogError("Could not get the network object for the fake player body ragdoll.");
            return; 
        }
        
        _currentFakePlayerBodyRagdoll = fakePlayerBodyRagdollNetworkObject.GetComponent<FakePlayerBodyRagdoll>();
        if (_currentFakePlayerBodyRagdoll == null)
        {
            BiodiversityPlugin.Logger.LogError("FakePlayerBodyRagdoll script is null on the ragdoll gameobject. This should never happen.");
            return;
        }

        _currentFakePlayerBodyRagdoll.AttachLimbToTransform("Neck", ragdollGrabTarget);
        UpdateRagdollVisibility();
    }
    
    private void UpdateRagdollVisibility()
    {
        if (_currentFakePlayerBodyRagdoll == null) return;
    
        bool isLocalPlayer = GameNetworkManager.Instance.localPlayerController == _targetPlayer.Value;
        _currentFakePlayerBodyRagdoll.bodyMeshRenderer.enabled = !isLocalPlayer;
    }

    /// <summary>
    /// Sets the target player up to be in captivity.
    /// It will muffle the player, drop all their items and freeze them.
    /// </summary>
    /// <param name="setToInCaptivity">Whether to make them captive or not.</param>
    private void HandleSetTargetPlayerInCaptivity(bool setToInCaptivity)
    {
        if (!_targetPlayer.HasValue)
        {
            _targetPlayerInCaptivity = false;
            return;
        }
        
        PlayerControllerB targetPlayer = _targetPlayer.Value;

        if (setToInCaptivity) SetupCaptivity(targetPlayer);
        else ReleaseCaptivity(targetPlayer);

        _targetPlayerInCaptivity = setToInCaptivity;
        BiodiversityPlugin.LogVerbose($"Set {_targetPlayer.Value.playerUsername} in captivity: {setToInCaptivity}");
    }

    private void SetupCaptivity(PlayerControllerB targetPlayer)
    {
        if (targetPlayer.inSpecialInteractAnimation && targetPlayer.currentTriggerInAnimationWith != null)
            targetPlayer.currentTriggerInAnimationWith.CancelAnimationExternally();

        targetPlayer.isCrouching = false;
        targetPlayer.playerBodyAnimator.SetBool(PlayerCrouching, false);
        HandleMuffleTargetPlayerVoice();
        
        targetPlayer.inSpecialInteractAnimation = true;
        targetPlayer.inAnimationWithEnemy = _aloeServer.Value;
        targetPlayer.isInElevator = false;
        targetPlayer.isInHangarShipRoom = false;
        
        targetPlayer.ResetZAndXRotation();
        targetPlayer.DropAllHeldItemsAndSync();

        CorrectlySetTargetPlayerLocalRenderers(false, targetPlayer);
    }

    private void ReleaseCaptivity(PlayerControllerB targetPlayer)
    {
        CleanupRagdoll();
        CorrectlySetTargetPlayerLocalRenderers(true, targetPlayer);
        
        // healingOrbEffect.Stop();
        healingLightEffect.enabled = false;
        _targetPlayer.Value.inSpecialInteractAnimation = false;
        _targetPlayer.Value.inAnimationWithEnemy = null;
        _targetPlayer.Value.ResetZAndXRotation();
        
        // Make sure the player is on a navmesh
        Vector3 validPosition =
            RoundManager.Instance.GetNavMeshPosition(_targetPlayer.Value.transform.position, new NavMeshHit(), 10f);
        _targetPlayer.Value.transform.position = new Vector3(validPosition.x, _targetPlayer.Value.transform.position.y, validPosition.z);
            
        HandleUnMuffleTargetPlayerVoice();
    }

    private void CorrectlySetTargetPlayerLocalRenderers(bool enable, PlayerControllerB targetPlayer)
    {
        if (GameNetworkManager.Instance.localPlayerController == targetPlayer)
        {
            GameNetworkManager.Instance.localPlayerController.thisPlayerModelArms.enabled = enable;
            _playerVisorRenderers[targetPlayer.actualClientId].enabled = enable;
        }
        else
        {
            ToggleTargetPlayerLocalRenderers(enable);
        }
    }

    private void CleanupRagdoll()
    {
        if (!_currentFakePlayerBodyRagdoll) return;
        BiodiversityPlugin.LogVerbose("_currentFakePlayerBodyRagdoll is not null. Destroying it.");
        Destroy(_currentFakePlayerBodyRagdoll.gameObject);
        _currentFakePlayerBodyRagdoll = null;
    }

    /// <summary>
    /// Handles when the target player is able to escape by mashing their space bar.
    /// </summary>
    /// <param name="canEscape">Whether to make the target player able to escape or not.</param>
    private void HandleSetTargetPlayerAbleToEscape(bool canEscape)
    {
        if (!_targetPlayer.HasValue) return;
        _targetPlayerCanEscape = canEscape;
        _targetPlayer.Value.inSpecialInteractAnimation = true;

        if (canEscape && HUDManager.Instance.localPlayer == _targetPlayer.Value)
            HUDManager.Instance.DisplayTip(
                LangParser.GetTranslation("tooltip.header.aloe.escape"), 
                LangParser.GetTranslation("tooltip.body.aloe.escape"),
                false,
                true,
                "LC_AloeGrabTip");
        BiodiversityPlugin.LogVerbose($"Set {_targetPlayer.Value.playerUsername} able to escape");
    }

    /// <summary>
    /// Increases the given player's fear level
    /// </summary>
    /// <param name="targetInsanity">.</param>
    /// <param name="playerClientId">.</param>
    private void HandleIncreasePlayerFearLevel(float targetInsanity, ulong playerClientId)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerClientId];
        if (player == null || player != GameNetworkManager.Instance.localPlayerController) return;
        player.JumpToFearLevel(targetInsanity);
        player.IncreaseFearLevelOverTime(0.8f);
    }

    /// <summary>
    /// Muffles the target player's voice.
    /// </summary>
    private void HandleMuffleTargetPlayerVoice()
    {
        if (!_targetPlayer.Value.currentVoiceChatAudioSource) StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
        if (!_targetPlayer.Value.currentVoiceChatAudioSource) return;

        BiodiversityPlugin.LogVerbose($"Muffling {_targetPlayer.Value.playerUsername}");
        _playerAudioLowPassFilters[_targetPlayer.Value.actualClientId].lowpassResonanceQ = 5f;
        _playerOccludeAudios[_targetPlayer.Value.actualClientId].overridingLowPass = true;
        _playerOccludeAudios[_targetPlayer.Value.actualClientId].lowPassOverride = 500f;
        _targetPlayer.Value.voiceMuffledByEnemy = true;
    }

    /// <summary>
    /// Un-muffles the target player's voice.
    /// </summary>
    private void HandleUnMuffleTargetPlayerVoice()
    {
        if (!_targetPlayer.Value.currentVoiceChatAudioSource) StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
        if (!_targetPlayer.Value.currentVoiceChatAudioSource) return;

        BiodiversityPlugin.LogVerbose($"UnMuffling {_targetPlayer.Value.playerUsername}");
        _playerAudioLowPassFilters[_targetPlayer.Value.actualClientId].lowpassResonanceQ = 1f;
        _playerOccludeAudios[_targetPlayer.Value.actualClientId].overridingLowPass = false;
        _playerOccludeAudios[_targetPlayer.Value.actualClientId].lowPassOverride = 20000f;
        _targetPlayer.Value.voiceMuffledByEnemy = false;
    }

    private float _timeSinceTargetPlayerNullLog;
    private int _targetPlayerNullCounter;

    private void ToggleTargetPlayerLocalRenderers(bool setToEnabled)
    {
        if (!_targetPlayer.HasValue)
        {
            _targetPlayerNullCounter++;
            if (_timeSinceTargetPlayerNullLog > 60)
            {
                BiodiversityPlugin.Logger.LogWarning($"Cannot toggle target player renderers because the target player variable is null. Times this has happened since the last message: {_targetPlayerNullCounter}, which was {_timeSinceTargetPlayerNullLog} seconds ago.");
                _targetPlayerNullCounter = 0;
                _timeSinceTargetPlayerNullLog = 0;
            }
            
            return;
        }
        
        ulong targetPlayerId = _targetPlayer.Value.actualClientId;
        List<Tuple<Component, bool>> cachedRenderers = _playersCachedRenderers[targetPlayerId];
        
        if (cachedRenderers == null || cachedRenderers.Count == 0)
        {
            BiodiversityPlugin.Logger.LogWarning($"No renderer components cached for player ID {targetPlayerId}");
            return;
        }

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            (Component component, bool isEnabledByDefault) = cachedRenderers[i];
            if (component is Renderer rendererComponent)
            {
                rendererComponent.enabled = isEnabledByDefault && setToEnabled;
            }
        }
    }

    /// <summary>
    /// Plays the healing effect on the target player.
    /// </summary>
    /// <param name="totalHealingTime">The total time the healing vfx should play for.</param>
    private void HandlePlayHealingVfx(float totalHealingTime)
    {
        healingLightEffect.enabled = true;
        BiodiversityPlugin.LogVerbose("Playing HealingOrbVfx");
        StartCoroutine(DisableHealingLightDelayed(totalHealingTime));
    }

    private IEnumerator DisableHealingLightDelayed(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        healingLightEffect.enabled = false;
    }

    private void HandleDamagePlayer(ulong playerId, int damage)
    {
        if (!netcodeController.IsServer) return;
        PlayerControllerB playerToDamage = StartOfRound.Instance.allPlayerScripts[playerId];
        
        if (PlayerUtil.IsPlayerDead(playerToDamage))
        {
            BiodiversityPlugin.Logger.LogWarning($"Cannot damage player with id {playerId}, because they do not exist.");
            return;
        }
        
        BiodiversityPlugin.LogVerbose($"Damaging player {playerToDamage.playerUsername} for {damage} damage!");
        playerToDamage.DamagePlayer(damage, true, true, CauseOfDeath.Bludgeoning, force: playerToDamage.turnCompass.forward * (-1 * 5));
    }

    /// <summary>
    /// Plays a random footstep sound effect when the Aloe's foot touches the ground in an animation.
    /// </summary>
    public void OnAnimationEventPlayFootstepSfx()
    {
        if (_lastFootstepTime < 0.25f) return;
        _lastFootstepTime = 0;
        
        AudioClip audioClipToPlay = stepsSfx[Random.Range(0, stepsSfx.Length)];
        aloeFootstepsSource.pitch = Random.Range(_defaultFoostepAudioSourcePitch - 0.1f, _defaultFoostepAudioSourcePitch + 0.1f);
        aloeFootstepsSource.PlayOneShot(audioClipToPlay);
        WalkieTalkie.TransmitOneShotAudio(aloeFootstepsSource, audioClipToPlay, aloeFootstepsSource.volume);
    }

    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        _targetPlayer.Set(newValue == BiodiverseAI.NullPlayerId ? null : StartOfRound.Instance.allPlayerScripts[newValue]);
        BiodiversityPlugin.LogVerbose(_targetPlayer.HasValue
            ? $"Changed target player to {_targetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }

    private void HandleBehaviourStateChanged(int oldValue, int newValue)
    {
        petalsRenderer.enabled = newValue is (int)AloeServerAI.States.HealingPlayer
            or (int)AloeServerAI.States.CuddlingPlayer or (int)AloeServerAI.States.ChasingEscapedPlayer
            or (int)AloeServerAI.States.AttackingPlayer;
        
        switch (newValue)
        {
            case (int)AloeServerAI.States.HealingPlayer or (int)AloeServerAI.States.CuddlingPlayer when oldValue is not ((int)AloeServerAI.States.HealingPlayer or (int)AloeServerAI.States.CuddlingPlayer):
            {
                BiodiversityPlugin.LogVerbose("Switching target player offset to cuddled.");
                if (_changeTargetPlayerOffsets != null) StopCoroutine(_changeTargetPlayerOffsets);
                _changeTargetPlayerOffsets = StartCoroutine(ChangeTargetPlayerOffsets(TargetPlayerOffsetType.Cuddled, 0.25f));
                break;
            }
            
            case (int)AloeServerAI.States.KidnappingPlayer when
                oldValue is not (int)AloeServerAI.States.KidnappingPlayer:
            {
                _offsetPosition = Vector3.zero;
                _offsetRotation = Quaternion.identity;
                
                BiodiversityPlugin.LogVerbose("Switching target player offset to dragged.");
                if (_changeTargetPlayerOffsets != null) StopCoroutine(_changeTargetPlayerOffsets);
                _changeTargetPlayerOffsets = StartCoroutine(ChangeTargetPlayerOffsets(TargetPlayerOffsetType.Dragged, 0.25f));
                break;
            }
        }
    }

    /// <summary>
    /// An animation event that gets triggered near the end of the death animation.
    /// The function plays the poof particle effect and makes the Aloe invisible
    /// </summary>
    public void OnAnimationEventPlayPoofParticleEffect()
    {
        poofParticleSystem.Play();

        bodyRenderer.enabled = false;
        petalsRenderer.enabled = false;
        healingLightEffect.enabled = false;
        
        aloeVoiceSource.Stop(true);
        aloeFootstepsSource.Stop(true);

        Destroy(scanNode.gameObject);
        if (netcodeController.IsOwner)
            StartCoroutine(DestroyAloeObjectAfterDuration(poofParticleSystem.main.duration));
    }

    /// <summary>
    /// Destroys the entire Aloe object after a duration
    /// </summary>
    private IEnumerator DestroyAloeObjectAfterDuration(float duration)
    {
        yield return new WaitForSeconds(duration);
        BiodiversityPlugin.LogVerbose($"Destroying AloeClient gameobject.");
        Destroy(gameObject);
    }

    private void AddStateMachineBehaviours(Animator receivedAnimator)
    {
        AloeServerAI aloeServerAI = _aloeServer.Value;
        StateMachineBehaviour[] behaviours = receivedAnimator.GetBehaviours<StateMachineBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            StateMachineBehaviour behaviour = behaviours[i];
            if (behaviour is AloeStateMachineBehaviour baseStateMachineBehaviour)
            {
                baseStateMachineBehaviour.Initialize(netcodeController, aloeServerAI, this);
            }
        }
    }
    
    private void ManuallyControlPlayerOffsets()
    {
        const float moveAmount = 0.1f;
        const float rotationAmount = 5f;
        
        if (Keyboard.current.numpad8Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("moved up");
            _offsetPosition += Vector3.up * moveAmount;
        }
        
        if (Keyboard.current.numpad2Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("moved down");
            _offsetPosition -= Vector3.up * moveAmount;
        }
        
        if (Keyboard.current.numpad4Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("moved left");
            _offsetPosition -= Vector3.right * moveAmount;
        }
        
        if (Keyboard.current.numpad6Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("moved right");
            _offsetPosition += Vector3.right * moveAmount;
        }
        
        if (Keyboard.current.numpad7Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("moved back");
            _offsetPosition -= Vector3.forward * moveAmount;
        }
        
        if (Keyboard.current.numpad9Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("moved forwards");
            _offsetPosition += Vector3.forward * moveAmount;
        }
        
        if (Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("rotated up");
            _offsetRotation *= Quaternion.Euler(Vector3.up * rotationAmount);
        }
        
        if (Keyboard.current.numpad3Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("rotated down");
            _offsetRotation *= Quaternion.Euler(Vector3.down * rotationAmount);
        }
        
        if (Keyboard.current.numpadDivideKey.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("rotated left");
            _offsetRotation *= Quaternion.Euler(Vector3.left * rotationAmount);
        }
        
        if (Keyboard.current.numpadMultiplyKey.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("rotated right");
            _offsetRotation *= Quaternion.Euler(Vector3.right * rotationAmount);
        }
        
        if (Keyboard.current.numpad0Key.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("rotated forward");
            _offsetRotation *= Quaternion.Euler(Vector3.forward * rotationAmount);
        }
        
        if (Keyboard.current.numpadPeriodKey.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("rotated back");
            _offsetRotation *= Quaternion.Euler(Vector3.back * rotationAmount);
        }
        
        // Reset position to original local position
        if (Keyboard.current.homeKey.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("reset all");
            _offsetPosition = Vector3.zero;
            _offsetRotation = Quaternion.identity;
        }
        
        if (Keyboard.current.pageUpKey.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("reset rotation");
            _offsetRotation = Quaternion.identity;
        }
        
        if (Keyboard.current.pageDownKey.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose("reset position");
            _offsetPosition = Vector3.zero;
        }
        
        if (Keyboard.current.numpadPlusKey.wasPressedThisFrame)
        {
            BiodiversityPlugin.LogVerbose(
                $"Offset Position: {_offsetPosition}, Offset Rotation: {_offsetRotation}, current position: {_targetPlayer.Value.transform.position}, bob: {transform.position + _offsetPosition}");
        }
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;
        
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
        netcodeController.OnSetAnimationTrigger += HandleSetAnimationTrigger;
        netcodeController.OnMuffleTargetPlayerVoice += HandleMuffleTargetPlayerVoice;
        netcodeController.OnUnMuffleTargetPlayerVoice += HandleUnMuffleTargetPlayerVoice;
        netcodeController.OnIncreasePlayerFearLevel += HandleIncreasePlayerFearLevel;
        netcodeController.OnHealTargetPlayerByAmount += HandleHealTargetPlayerByAmount;
        netcodeController.OnSetTargetPlayerInCaptivity += HandleSetTargetPlayerInCaptivity;
        netcodeController.OnSetTargetPlayerAbleToEscape += HandleSetTargetPlayerAbleToEscape;
        netcodeController.OnPlayHealingVfx += HandlePlayHealingVfx;
        netcodeController.OnCrushPlayerNeck += HandleCrushPlayerAnimation;
        netcodeController.OnDamagePlayer += HandleDamagePlayer;
        netcodeController.OnSpawnFakePlayerBodyRagdoll += HandleSpawnFakePlayerBodyRagdoll;
        netcodeController.OnTransitionToRunningForwardsAndCarryingPlayer +=
            HandleTransitionToRunningForwardsAndCarryingPlayer;

        netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;
        netcodeController.ShouldHaveDarkSkin.OnValueChanged += HandleChangeAloeSkinColour;
        _aloeServer.Value.NetworkCurrentBehaviourStateIndex.OnValueChanged += HandleBehaviourStateChanged;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;
        
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
        netcodeController.OnSetAnimationTrigger -= HandleSetAnimationTrigger;
        netcodeController.OnMuffleTargetPlayerVoice -= HandleMuffleTargetPlayerVoice;
        netcodeController.OnUnMuffleTargetPlayerVoice -= HandleUnMuffleTargetPlayerVoice;
        netcodeController.OnIncreasePlayerFearLevel -= HandleIncreasePlayerFearLevel;
        netcodeController.OnHealTargetPlayerByAmount -= HandleHealTargetPlayerByAmount;
        netcodeController.OnSetTargetPlayerInCaptivity -= HandleSetTargetPlayerInCaptivity;
        netcodeController.OnSetTargetPlayerAbleToEscape -= HandleSetTargetPlayerAbleToEscape;
        netcodeController.OnPlayHealingVfx -= HandlePlayHealingVfx;
        netcodeController.OnCrushPlayerNeck -= HandleCrushPlayerAnimation;
        netcodeController.OnDamagePlayer -= HandleDamagePlayer;
        netcodeController.OnSpawnFakePlayerBodyRagdoll -= HandleSpawnFakePlayerBodyRagdoll;
        netcodeController.OnTransitionToRunningForwardsAndCarryingPlayer -=
            HandleTransitionToRunningForwardsAndCarryingPlayer;

        netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;
        netcodeController.ShouldHaveDarkSkin.OnValueChanged -= HandleChangeAloeSkinColour;
        _aloeServer.Value.NetworkCurrentBehaviourStateIndex.OnValueChanged -= HandleBehaviourStateChanged;

        _networkEventsSubscribed = false; 
    }

    /// <summary>
    /// Sets a trigger in the animator
    /// </summary>
    /// <param name="parameter">The name of the trigger in the animator.</param>
    private void HandleSetAnimationTrigger(int parameter)
    {
        animator.SetTrigger(parameter);
    }

    /// <summary>
    /// Sets the configurable variables to their value in the player's config
    /// </summary>
    private void HandleInitializeConfigValues()
    {
        escapeChargePerPress = AloeHandler.Instance.Config.EscapeChargePerPress;
        escapeChargeDecayRate = AloeHandler.Instance.Config.EscapeChargeDecayRate;
        escapeChargeThreshold = AloeHandler.Instance.Config.EscapeChargeThreshold;
        skinMetallicTransitionTime = AloeHandler.Instance.Config.DarkSkinTransitionTime;
    }
}