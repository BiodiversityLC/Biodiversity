using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;
using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.Util.Lang;
using Unity.Netcode;
using GameNetcodeStuff;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
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
        new Tuple<string, Type>("LOD1", typeof(SkinnedMeshRenderer)),
        new Tuple<string, Type>("LOD2", typeof(SkinnedMeshRenderer)),
        new Tuple<string, Type>("LOD3", typeof(SkinnedMeshRenderer)),
        new Tuple<string, Type>("LevelSticker", typeof(MeshRenderer)),
        new Tuple<string, Type>("BetaBadge", typeof(MeshRenderer)),
        // new Tuple<string, Type>("Circle", typeof(SkinnedMeshRenderer)),
        // new Tuple<string, Type>("CopyHeldProp", typeof(MeshRenderer)),
        // new Tuple<string, Type>("PlayerPhysicsBox", typeof(MeshRenderer)),
        // new Tuple<string, Type>("LineOfSightCube", typeof(MeshRenderer)),
        // new Tuple<string, Type>("LineOfSightCubeSmall", typeof(MeshRenderer)),
        // new Tuple<string, Type>("LineOfSight2", typeof(MeshRenderer)),
    ];

#if !UNITY_EDITOR
    [field: HideInInspector]
    [field: SerializeField]
    public AloeConfig Config { get; private set; } = AloeHandler.Instance.Config;
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
    [SerializeField] private float skinMetallicValueDark = 0.735f;

    [Header("Visual Effects")] [Space(5f)] 
    [SerializeField] private Light healingLightEffect;
    [SerializeField] private VisualEffect healingOrbEffect;
    [SerializeField] private ParticleSystem poofParticleSystem;

    [Header("Animation")] [Space(5f)] 
    [SerializeField] private Animator animator;
    [SerializeField] private Rig lookAimRig;
    [SerializeField] private Transform lookTarget;
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
    [SerializeField] private float lookBlendDuration = 0.5f;
    [SerializeField] private float smoothLookTargetPositionTime = 0.3f;
    
    private readonly Dictionary<ulong, List<Component>> _targetPlayersCachedRenderers = new();

    private readonly NullableObject<PlayerControllerB> _targetPlayer = new();

    private FakePlayerBodyRagdoll _currentFakePlayerBodyRagdoll;

    private Vector3 _agentLastPosition;
    private Vector3 _lookTargetVelocity;
    private Vector3 _lastReceivedLookTargetPosition;
    private Vector3 _offsetPosition = Vector3.zero;

    private Quaternion _offsetRotation = Quaternion.identity;

    private Coroutine _changeLookAimConstraintWeightCoroutine;
    private Coroutine _changeSkinColourCoroutine;
    private Coroutine _changeTargetPlayerOffsets;

    private bool _targetPlayerCanEscape;
    private bool _targetPlayerInCaptivity;
    private bool _networkEventsSubscribed;

    private float _currentEscapeChargeValue;
    private float _agentCurrentSpeed;
    private float _lastReceivedNewLookTargetPositionTime;
    private const float LookTargetPositionLerpTime = 0.1f;

    private int _currentBehaviourStateIndex;

    private void Awake()
    {
        _mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Client");
        if (netcodeController == null) netcodeController = GetComponent<AloeNetcodeController>();
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
    }

    private void Start()
    {
        _propertyBlock = new MaterialPropertyBlock();
        lookAimRig.weight = 0f;

        AddStateMachineBehaviours(animator);

        animator.SetBool(Spawning, true);
    }
    
    private void Update()
    {
        _currentBehaviourStateIndex = netcodeController.CurrentBehaviourStateIndex.Value;

        Vector3 position = transform.position;
        _agentCurrentSpeed = Mathf.Lerp(_agentCurrentSpeed, (position - _agentLastPosition).magnitude / Time.deltaTime,
            0.75f);
        _agentLastPosition = position;

        animator.SetFloat(Speed, _agentCurrentSpeed);
        animator.SetBool(Healing, netcodeController.AnimationParamHealing.Value);
        animator.SetBool(Crawling, netcodeController.AnimationParamCrawling.Value);
        animator.SetBool(Stunned, netcodeController.AnimationParamStunned.Value);
        animator.SetBool(Dead, netcodeController.AnimationParamDead.Value);

        /*
        const float moveAmount = 0.1f;
        const float rotationAmount = 5f;
        
        if (Keyboard.current.numpad8Key.wasPressedThisFrame)
        {
            LogDebug("moved up");
            _offsetPosition += Vector3.up * moveAmount;
        }
        
        if (Keyboard.current.numpad2Key.wasPressedThisFrame)
        {
            LogDebug("moved down");
            _offsetPosition -= Vector3.up * moveAmount;
        }
        
        if (Keyboard.current.numpad4Key.wasPressedThisFrame)
        {
            LogDebug("moved left");
            _offsetPosition -= Vector3.right * moveAmount;
        }
        
        if (Keyboard.current.numpad6Key.wasPressedThisFrame)
        {
            LogDebug("moved right");
            _offsetPosition += Vector3.right * moveAmount;
        }
        
        if (Keyboard.current.numpad7Key.wasPressedThisFrame)
        {
            LogDebug("moved back");
            _offsetPosition -= Vector3.forward * moveAmount;
        }
        
        if (Keyboard.current.numpad9Key.wasPressedThisFrame)
        {
            LogDebug("moved forwards");
            _offsetPosition += Vector3.forward * moveAmount;
        }
        
        if (Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            LogDebug("rotated up");
            _offsetRotation *= Quaternion.Euler(Vector3.up * rotationAmount);
        }
        
        if (Keyboard.current.numpad3Key.wasPressedThisFrame)
        {
            LogDebug("rotated down");
            _offsetRotation *= Quaternion.Euler(Vector3.down * rotationAmount);
        }
        
        if (Keyboard.current.numpadDivideKey.wasPressedThisFrame)
        {
            LogDebug("rotated left");
            _offsetRotation *= Quaternion.Euler(Vector3.left * rotationAmount);
        }
        
        if (Keyboard.current.numpadMultiplyKey.wasPressedThisFrame)
        {
            LogDebug("rotated right");
            _offsetRotation *= Quaternion.Euler(Vector3.right * rotationAmount);
        }
        
        if (Keyboard.current.numpad0Key.wasPressedThisFrame)
        {
            LogDebug("rotated forward");
            _offsetRotation *= Quaternion.Euler(Vector3.forward * rotationAmount);
        }
        
        if (Keyboard.current.numpadPeriodKey.wasPressedThisFrame)
        {
            LogDebug("rotated back");
            _offsetRotation *= Quaternion.Euler(Vector3.back * rotationAmount);
        }
        
        // Reset position to original local position
        if (Keyboard.current.homeKey.wasPressedThisFrame)
        {
            LogDebug("reset all");
            _offsetPosition = Vector3.zero;
            _offsetRotation = Quaternion.identity;
        }
        
        if (Keyboard.current.pageUpKey.wasPressedThisFrame)
        {
            LogDebug("reset rotation");
            _offsetRotation = Quaternion.identity;
        }
        
        if (Keyboard.current.pageDownKey.wasPressedThisFrame)
        {
            LogDebug("reset position");
            _offsetPosition = Vector3.zero;
        }
        
        if (Keyboard.current.numpadPlusKey.wasPressedThisFrame)
        {
            LogDebug(
                $"Offset Position: {_offsetPosition}, Offset Rotation: {_offsetRotation}, current position: {_targetPlayer.Value.transform.position}, bob: {transform.position + _offsetPosition}");
        }
        */

        if (_targetPlayerCanEscape)
        {
            switch (_currentBehaviourStateIndex)
            {
                case (int)AloeServer.States.HealingPlayer or (int)AloeServer.States.CuddlingPlayer:
                {
                    if (!_targetPlayer.IsNotNull) break;
                    if (GameNetworkManager.Instance.localPlayerController != _targetPlayer.Value) break;

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
                        
                        // healingOrbEffect.Stop();
                        healingLightEffect.enabled = false;
                    }

                    break;
                }
            }
        }
        else 
            _currentEscapeChargeValue = 0;
        
    }

    private void LateUpdate()
    {
        // Make the look target aim at a player
        {
            Vector3 newPosition;
            if (netcodeController.IsOwner)
            {
                newPosition = netcodeController.LookTargetPosition.Value;
            }
            else
            {
                float timeSinceLastUpdate = Time.time - _lastReceivedNewLookTargetPositionTime;
                float t = timeSinceLastUpdate / LookTargetPositionLerpTime;

                newPosition = Vector3.Lerp(transform.position, _lastReceivedLookTargetPosition, t);
            }

            AloeUtils.SmoothMoveTransformTo(lookTarget, newPosition, smoothLookTargetPositionTime,
                ref _lookTargetVelocity);
        }

        // Animate the real target player's body
        if (netcodeController.CurrentBehaviourStateIndex.Value is (int)AloeServer.States.KidnappingPlayer
                or (int)AloeServer.States.HealingPlayer or (int)AloeServer.States.CuddlingPlayer &&
            _targetPlayer.IsNotNull && _targetPlayerInCaptivity && _targetPlayer.Value.inSpecialInteractAnimation)
        {
            _targetPlayer.Value.transform.position = transform.position + transform.rotation * _offsetPosition;
            _targetPlayer.Value.transform.rotation = transform.rotation * _offsetRotation;
        }
    }

    /// <summary>
    /// Handles what should happen to make the Aloe crush a player's head
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="playerClientId">The player's client id whose head will be crushed.</param>
    private void HandleCrushPlayerAnimation(string receivedAloeId, ulong playerClientId)
    {
        if (_aloeId != receivedAloeId) return;

        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerClientId];
        if (player == null) return;

        StartCoroutine(CrushPlayerAnimation(player));
    }

    private IEnumerator CrushPlayerAnimation(PlayerControllerB player)
    {
        LogDebug($"Killing player: {player.name}");
        if (player.inSpecialInteractAnimation &&
            player.currentTriggerInAnimationWith != null)
            player.currentTriggerInAnimationWith.CancelAnimationExternally();
        
        player.inSpecialInteractAnimation = true;
        player.inAnimationWithEnemy = GetComponent<AloeServer>();
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

    /// <summary>
    /// Plays an audio clip with the given type and index
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="audioClipType">The audio clip type to play.</param>
    /// <param name="clipIndex">The index of the clip in their respective AudioClip array to play.</param>
    /// <param name="interrupt">Whether to interrupt any previously playing sound before playing the new audio.</param>
    private void HandlePlayAudioClipType(string receivedAloeId, AudioClipTypes audioClipType, int clipIndex,
        bool interrupt = false)
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
        LogDebug($"Changing Aloe's skin to a {(toDark ? "dark" : "light")} colour");
        float timeElapsed = 0f;
        float startMetallicValue = toDark ? 0 : skinMetallicValueDark;
        float endMetallicValue = toDark ? skinMetallicValueDark : 0;

        Color currentColour = bodyRenderer.material.GetColor(BaseColour);
        RGBToHSV(currentColour, out float h, out float s, out float v);
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
        _changeLookAimConstraintWeightCoroutine = null;
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
        if (!_targetPlayer.IsNotNull) return;
        _targetPlayer.Value.health += healAmount;
        if (HUDManager.Instance.localPlayer == _targetPlayer.Value)
            HUDManager.Instance.UpdateHealthUI(GameNetworkManager.Instance.localPlayerController.health, false);
        LogDebug($"Target player health after last heal: {_targetPlayer.Value.health}");
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

    private void HandleTransitionToRunningForwardsAndCarryingPlayer(string receivedAloeId, float transitionDuration)
    {
        if (_aloeId != receivedAloeId) return;
        LogDebug($"In {nameof(HandleTransitionToRunningForwardsAndCarryingPlayer)}");

        if (_currentFakePlayerBodyRagdoll == null)
        {
            _mls.LogError("The player body ragdoll is null, this should never happen.");
            return;
        }

        if (_changeTargetPlayerOffsets != null) StopCoroutine(_changeTargetPlayerOffsets);
        _changeTargetPlayerOffsets = StartCoroutine(
            ChangeTargetPlayerOffsets(TargetPlayerOffsetType.Carried, transitionDuration));

        _currentFakePlayerBodyRagdoll.DetachLimbFromTransform("Neck");
        _currentFakePlayerBodyRagdoll.AttachLimbToTransform("Root", ragdollGrabTarget);
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

        _currentFakePlayerBodyRagdoll.AttachLimbToTransform("Neck", ragdollGrabTarget);

        if (GameNetworkManager.Instance.localPlayerController == _targetPlayer.Value)
        {
            _currentFakePlayerBodyRagdoll.bodyMeshRenderer.enabled = false;
        }
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
        if (!_targetPlayer.IsNotNull)
        {
            _targetPlayerInCaptivity = false;
            return;
        }

        if (setToInCaptivity)
        {
            if (_targetPlayer.Value.inSpecialInteractAnimation && _targetPlayer.Value.currentTriggerInAnimationWith != null)
                _targetPlayer.Value.currentTriggerInAnimationWith.CancelAnimationExternally();

            HandleMuffleTargetPlayerVoice(_aloeId);
            _targetPlayer.Value.inSpecialInteractAnimation = true;
            _targetPlayer.Value.inAnimationWithEnemy = GetComponent<AloeServer>();
            _targetPlayer.Value.isInElevator = false;
            _targetPlayer.Value.isInHangarShipRoom = false;
            _targetPlayer.Value.ResetZAndXRotation();
            _targetPlayer.Value.DropAllHeldItemsAndSync();

            if (GameNetworkManager.Instance.localPlayerController != _targetPlayer.Value)
            {
                ToggleTargetPlayerRenderers(false);
            }
            else
            {
                GameNetworkManager.Instance.localPlayerController.thisPlayerModelArms.enabled = false;
                GameNetworkManager.Instance.localPlayerController.localVisor.gameObject.GetComponentsInChildren<MeshRenderer>()[0].enabled = false;
            }
        }
        else
        {
            if (_currentFakePlayerBodyRagdoll != null)
            {
                Destroy(_currentFakePlayerBodyRagdoll.gameObject);
                _currentFakePlayerBodyRagdoll = null;
            }

            if (GameNetworkManager.Instance.localPlayerController != _targetPlayer.Value)
            {
                ToggleTargetPlayerRenderers(true);
            }
            else
            {
                GameNetworkManager.Instance.localPlayerController.thisPlayerModelArms.enabled = true;
                GameNetworkManager.Instance.localPlayerController.localVisor.gameObject.GetComponentsInChildren<MeshRenderer>()[0].enabled = true;
            }

            // healingOrbEffect.Stop();
            healingLightEffect.enabled = false;
            _targetPlayer.Value.inSpecialInteractAnimation = false;
            _targetPlayer.Value.inAnimationWithEnemy = null;
            _targetPlayer.Value.ResetZAndXRotation();
            
            // Make sure the player is on a navmesh
            Vector3 validPosition =
                RoundManager.Instance.GetNavMeshPosition(_targetPlayer.Value.transform.position, new NavMeshHit(), 1.5f);
            _targetPlayer.Value.transform.position = new Vector3(validPosition.x, _targetPlayer.Value.transform.position.y, validPosition.z);
            
            HandleUnMuffleTargetPlayerVoice(_aloeId);
        }

        _targetPlayerInCaptivity = setToInCaptivity;
        LogDebug($"Set {_targetPlayer.Value.playerUsername} in captivity: {setToInCaptivity}");
    }

    /// <summary>
    /// Handles when the target player is able to escape by mashing their space bar.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="canEscape">Whether to make the target player able to escape or not.</param>
    private void HandleSetTargetPlayerAbleToEscape(string receivedAloeId, bool canEscape)
    {
        if (_aloeId != receivedAloeId) return;
        if (!_targetPlayer.IsNotNull) return;
        _targetPlayerCanEscape = canEscape;
        _targetPlayer.Value.inSpecialInteractAnimation = true;

        if (canEscape && HUDManager.Instance.localPlayer == _targetPlayer.Value)
            HUDManager.Instance.DisplayTip(
                LangParser.GetTranslation("You can escape!"), 
                LangParser.GetTranslation("Mash the spacebar to escape from The Aloe!"),
                false,
                true,
                "LC_AloeGrabTip");
        LogDebug($"Set {_targetPlayer.Value.playerUsername} able to escape");
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
        if (player == null) return;
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

        if (_targetPlayer.Value.currentVoiceChatAudioSource == null)
            StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
        if (_targetPlayer.Value.currentVoiceChatAudioSource == null) return;

        LogDebug($"Muffling {_targetPlayer.Value.playerUsername}");
        _targetPlayer.Value.currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 5f;
        OccludeAudio component = _targetPlayer.Value.currentVoiceChatAudioSource.GetComponent<OccludeAudio>();
        component.overridingLowPass = true;
        component.lowPassOverride = 500f;
        _targetPlayer.Value.voiceMuffledByEnemy = true;
    }

    /// <summary>
    /// Un-muffles the target player's voice.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    private void HandleUnMuffleTargetPlayerVoice(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;

        if (_targetPlayer.Value.currentVoiceChatAudioSource == null)
            StartOfRound.Instance.RefreshPlayerVoicePlaybackObjects();
        if (_targetPlayer.Value.currentVoiceChatAudioSource == null) return;

        LogDebug($"UnMuffling {_targetPlayer.Value.playerUsername}");
        _targetPlayer.Value.currentVoiceChatAudioSource.GetComponent<AudioLowPassFilter>().lowpassResonanceQ = 1f;
        OccludeAudio component = _targetPlayer.Value.currentVoiceChatAudioSource.GetComponent<OccludeAudio>();
        component.overridingLowPass = false;
        component.lowPassOverride = 20000f;
        _targetPlayer.Value.voiceMuffledByEnemy = false;
    }

    private void ToggleTargetPlayerRenderers(bool setToEnabled)
    {
        if (!_targetPlayer.IsNotNull)
        {
            _mls.LogError("Cannot toggle target player renderers because the target player variable is null.");
            return;
        }
        
        ulong targetPlayerId = _targetPlayer.Value.actualClientId;
        if (_targetPlayersCachedRenderers.TryGetValue(targetPlayerId, out List<Component> rendererComponents))
        {
            foreach (Renderer rendererComponent in rendererComponents.Cast<Renderer>())
            {
                rendererComponent.enabled = setToEnabled;
            }
        }
        else
        {
            List<Component> newRendererComponents = [];
            foreach (Tuple<string, Type> rendererTuple in _playerRendererObjects)
            {
                try
                {
                    Transform rendererTransform = _targetPlayer.Value.transform.Find(rendererTuple.Item1);
                    if (rendererTransform == null)
                    {
                        _mls.LogWarning($"Transform not found for renderer: {rendererTuple.Item1}");
                        continue;
                    }
                    LogDebug($"Found transform for renderer: {rendererTuple.Item1}");

                    if (rendererTransform.GetComponent(rendererTuple.Item2) is Renderer rendererComponent)
                    {
                        LogDebug($"Found {nameof(rendererComponent)} component for renderer: {rendererTuple.Item1}");
                        rendererComponent.enabled = setToEnabled;
                        newRendererComponents.Add(rendererComponent);
                    }
                    else
                    {
                        _mls.LogWarning($"Component of type {rendererTuple.Item2} not found or incorrect type for renderer: {rendererTuple.Item1}");
                    }
                }
                catch (Exception ex)
                {
                    _mls.LogError($"Error processing renderer: {rendererTuple.Item1} for player ID {targetPlayerId}: {ex.Message}");
                }
            }

            if (newRendererComponents.Count > 0)
                _targetPlayersCachedRenderers[targetPlayerId] = newRendererComponents;
        }
    }

    /// <summary>
    /// Plays the healing effect on the target player.
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="totalHealingTime">The total time the healing vfx should play for.</param>
    private void HandlePlayHealingVfx(string receivedAloeId, float totalHealingTime)
    {
        if (_aloeId != receivedAloeId) return;
        
        // healingOrbEffect.Stop();
        // healingOrbEffect.SetFloat("Duration", totalHealingTime);
        // healingOrbEffect.SendEvent("OnShowHealingOrb");
        
        healingLightEffect.enabled = true;
        LogDebug("Playing HealingOrbVfx");
        StartCoroutine(DisableHealingLightDelayed(totalHealingTime));
        HandlePlayAudioClipType(receivedAloeId, AudioClipTypes.Healing, 0, true);
    }

    private IEnumerator DisableHealingLightDelayed(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        healingLightEffect.enabled = false;
    }

    private void HandleDamagePlayer(string receivedAloeId, ulong playerId, int damage)
    {
        if (_aloeId != receivedAloeId || !netcodeController.IsServer) return;
        NullableObject<PlayerControllerB> playerToDamage = new(StartOfRound.Instance.allPlayerScripts[playerId]);
        if (!playerToDamage.IsNotNull)
        {
            _mls.LogError($"Cannot damage player with id {playerId}, because they do not exist.");
            return;
        }
        LogDebug($"Damaging player {playerToDamage.Value.playerUsername} for {damage} damage!");
        playerToDamage.Value.DamagePlayer(damage, true, true, CauseOfDeath.Bludgeoning, force: playerToDamage.Value.turnCompass.forward * (-1 * 5));
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
        LogDebug(
            $"Changing look aim constraint weight from {lookAimRig.weight} to {endWeight} in {duration} seconds blend time.");
        if (_changeLookAimConstraintWeightCoroutine != null) StopCoroutine(_changeLookAimConstraintWeightCoroutine);
        _changeLookAimConstraintWeightCoroutine =
            StartCoroutine(BlendLookAimConstraintWeight(lookAimRig.weight, endWeight, duration));
    }

    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        _targetPlayer.Value = newValue == AloeServer.NullPlayerId ? null : StartOfRound.Instance.allPlayerScripts[newValue];
        LogDebug(_targetPlayer.IsNotNull
            ? $"Changed target player to {_targetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }

    private void HandleLookTargetPositionChanged(Vector3 oldValue, Vector3 newValue)
    {
        _lastReceivedLookTargetPosition = newValue;
        _lastReceivedNewLookTargetPositionTime = Time.time;
    }

    private void HandleBehaviourStateChanged(int oldValue, int newValue)
    {
        petalsRenderer.enabled = newValue is (int)AloeServer.States.HealingPlayer
            or (int)AloeServer.States.CuddlingPlayer or (int)AloeServer.States.ChasingEscapedPlayer
            or (int)AloeServer.States.AttackingPlayer;
        
        switch (newValue)
        {
            case (int)AloeServer.States.HealingPlayer or (int)AloeServer.States.CuddlingPlayer when oldValue is not ((int)AloeServer.States.HealingPlayer or (int)AloeServer.States.CuddlingPlayer):
            {
                LogDebug("Switching target player offset to cuddled.");
                if (_changeTargetPlayerOffsets != null) StopCoroutine(_changeTargetPlayerOffsets);
                _changeTargetPlayerOffsets = StartCoroutine(ChangeTargetPlayerOffsets(TargetPlayerOffsetType.Cuddled, 0.25f));
                break;
            }
            
            case (int)AloeServer.States.KidnappingPlayer when
                oldValue is not (int)AloeServer.States.KidnappingPlayer:
            {
                _offsetPosition = Vector3.zero;
                _offsetRotation = Quaternion.identity;
                
                LogDebug("Switching target player offset to dragged.");
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
        LogDebug($"In {nameof(OnAnimationEventPlayPoofParticleEffect)}");
        poofParticleSystem.Play();

        bodyRenderer.enabled = false;
        petalsRenderer.enabled = false;

        // healingOrbEffect.Stop();
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
        LogDebug("Destroying gameobject.");
        Destroy(gameObject);
    }

    private void AddStateMachineBehaviours(Animator receivedAnimator)
    {
        AloeServer aloeServer = GetComponent<AloeServer>();
        StateMachineBehaviour[] behaviours = receivedAnimator.GetBehaviours<StateMachineBehaviour>();
        foreach (StateMachineBehaviour behaviour in behaviours)
        {
            if (behaviour is BaseStateMachineBehaviour baseStateMachineBehaviour)
            {
                baseStateMachineBehaviour.Initialize(netcodeController, aloeServer, this);
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

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;

        netcodeController.OnSyncAloeId += HandleSyncAloeId;
        netcodeController.OnInitializeConfigValues += HandleInitializeConfigValues;
        netcodeController.OnSetAnimationTrigger += HandleSetAnimationTrigger;
        netcodeController.OnMuffleTargetPlayerVoice += HandleMuffleTargetPlayerVoice;
        netcodeController.OnUnMuffleTargetPlayerVoice += HandleUnMuffleTargetPlayerVoice;
        netcodeController.OnIncreasePlayerFearLevel += HandleIncreasePlayerFearLevel;
        netcodeController.OnHealTargetPlayerByAmount += HandleHealTargetPlayerByAmount;
        netcodeController.OnSetTargetPlayerInCaptivity += HandleSetTargetPlayerInCaptivity;
        netcodeController.OnSetTargetPlayerAbleToEscape += HandleSetTargetPlayerAbleToEscape;
        netcodeController.OnPlayHealingVfx += HandlePlayHealingVfx;
        netcodeController.OnPlayAudioClipType += HandlePlayAudioClipType;
        netcodeController.OnCrushPlayerNeck += HandleCrushPlayerAnimation;
        netcodeController.OnDamagePlayer += HandleDamagePlayer;
        netcodeController.OnChangeLookAimConstraintWeight += HandleChangeLookAimConstraintWeight;
        netcodeController.OnSpawnFakePlayerBodyRagdoll += HandleSpawnFakePlayerBodyRagdoll;
        netcodeController.OnTransitionToRunningForwardsAndCarryingPlayer +=
            HandleTransitionToRunningForwardsAndCarryingPlayer;

        netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;
        netcodeController.ShouldHaveDarkSkin.OnValueChanged += HandleChangeAloeSkinColour;
        netcodeController.CurrentBehaviourStateIndex.OnValueChanged += HandleBehaviourStateChanged;
        
        if (!netcodeController.IsOwner)
            netcodeController.LookTargetPosition.OnValueChanged += HandleLookTargetPositionChanged;

        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;

        netcodeController.OnSyncAloeId -= HandleSyncAloeId;
        netcodeController.OnInitializeConfigValues -= HandleInitializeConfigValues;
        netcodeController.OnSetAnimationTrigger -= HandleSetAnimationTrigger;
        netcodeController.OnMuffleTargetPlayerVoice -= HandleMuffleTargetPlayerVoice;
        netcodeController.OnUnMuffleTargetPlayerVoice -= HandleUnMuffleTargetPlayerVoice;
        netcodeController.OnIncreasePlayerFearLevel -= HandleIncreasePlayerFearLevel;
        netcodeController.OnHealTargetPlayerByAmount -= HandleHealTargetPlayerByAmount;
        netcodeController.OnSetTargetPlayerInCaptivity -= HandleSetTargetPlayerInCaptivity;
        netcodeController.OnSetTargetPlayerAbleToEscape -= HandleSetTargetPlayerAbleToEscape;
        netcodeController.OnPlayHealingVfx -= HandlePlayHealingVfx;
        netcodeController.OnPlayAudioClipType -= HandlePlayAudioClipType;
        netcodeController.OnCrushPlayerNeck -= HandleCrushPlayerAnimation;
        netcodeController.OnDamagePlayer -= HandleDamagePlayer;
        netcodeController.OnChangeLookAimConstraintWeight -= HandleChangeLookAimConstraintWeight;
        netcodeController.OnSpawnFakePlayerBodyRagdoll -= HandleSpawnFakePlayerBodyRagdoll;
        netcodeController.OnTransitionToRunningForwardsAndCarryingPlayer -=
            HandleTransitionToRunningForwardsAndCarryingPlayer;

        netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;
        netcodeController.ShouldHaveDarkSkin.OnValueChanged -= HandleChangeAloeSkinColour;
        netcodeController.CurrentBehaviourStateIndex.OnValueChanged -= HandleBehaviourStateChanged;
        
        if (!netcodeController.IsOwner)
            netcodeController.LookTargetPosition.OnValueChanged -= HandleLookTargetPositionChanged;

        _networkEventsSubscribed = false;
    }

    /// <summary>
    /// Sets a trigger in the animator
    /// </summary>
    /// <param name="receivedAloeId">The Aloe ID.</param>
    /// <param name="parameter">The name of the trigger in the animator.</param>
    private void HandleSetAnimationTrigger(string receivedAloeId, int parameter)
    {
        if (_aloeId != receivedAloeId) return;
        animator.SetTrigger(parameter);
    }

    /// <summary>
    /// Sets the configurable variables to their value in the player's config
    /// </summary>
    private void HandleInitializeConfigValues(string receivedAloeId)
    {
        if (_aloeId != receivedAloeId) return;
        
        escapeChargePerPress = Config.EscapeChargePerPress;
        escapeChargeDecayRate = Config.EscapeChargeDecayRate;
        escapeChargeThreshold = Config.EscapeChargeThreshold;
        skinMetallicTransitionTime = Config.DarkSkinTransitionTime;
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

        LogDebug("Successfully synced aloe id.");
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