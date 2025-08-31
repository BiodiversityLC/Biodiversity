using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierClient : MonoBehaviour
{
    #region Animator Hashes
    private static readonly int InSalute = Animator.StringToHash("InSalute");
    public static readonly int Spawning = Animator.StringToHash("Spawning");
    public static readonly int SpinAttack = Animator.StringToHash("SpinAttack");
    public static readonly int StabAttack = Animator.StringToHash("StabAttack");
    public static readonly int AimMusket = Animator.StringToHash("AimMusket");
    public static readonly int ShootMusket = Animator.StringToHash("ShootMusket");
    public static readonly int ReloadMusket = Animator.StringToHash("ReloadMusket");
    public static readonly int ForceWalk = Animator.StringToHash("ForceWalk");
    public static readonly int Melt = Animator.StringToHash("Melt");
    public static readonly int Dead = Animator.StringToHash("Dead");
    
    public static readonly int VelocityX = Animator.StringToHash("VelocityX");
    public static readonly int VelocityZ = Animator.StringToHash("VelocityZ");
    #endregion
    
    #region Unity Inspector Variables
#pragma warning disable 0649
#pragma warning disable CS0169
    [SerializeField] private NavMeshAgent agent;
    
    [SerializeField] private GameObject unmoltenGameObject;
    [SerializeField] private GameObject moltenGameObject;
    
    [Header("Animation")] [Space(5f)] 
    [SerializeField] private Animator unmoltenAnimator;
    [SerializeField] private Animator moltenAnimator;
    [SerializeField] private Transform musketContainer;
    
    [Header("Audio")] [Space(5f)]
    [SerializeField] private AudioSource creatureVoice;
    [SerializeField] private AudioSource footstepsSource;
    
    [Space(2f)]
    
    [SerializeField] private AudioClip[] activateSfx;
    [SerializeField] private AudioClip[] aimSfx;
    [SerializeField] private AudioClip[] reloadSfx;
    [SerializeField] private AudioClip[] spinSfx;
    [SerializeField] private AudioClip[] lightFootstepSfx;
    [SerializeField] private AudioClip[] heavyFootstepSfx;
    
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private WaxSoldierNetcodeController netcodeController;
#pragma warning restore 0649
#pragma warning restore CS0169
    #endregion

    private Animator _currentAnimator;
    
    private CachedUnityObject<PlayerControllerB> _targetPlayer;
    private CachedValue<EnemyAI> _enemyAIReference;
    private Musket _musket;
    
    private Vector3 _smoothedVelocity;

    private float _previousAnimatorSpeedBeforeFreeze = 1f;

    private bool _networkEventsSubscribed;
    
    private void Awake()
    {
        if (!netcodeController) netcodeController = GetComponent<WaxSoldierNetcodeController>();
        _enemyAIReference = new CachedValue<EnemyAI>(GetComponent<EnemyAI>);
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void Start()
    {
        unmoltenAnimator.SetBool(Spawning, true);
        _currentAnimator = unmoltenAnimator;
    }

    private void Update()
    {
        _currentAnimator.SetBool(InSalute, netcodeController.AnimationParamInSalute.Value);
        _currentAnimator.SetBool(Dead, netcodeController.AnimationParamIsDead.Value);
    }

    #region Animation
    public void SetWalkLocomotionAnimationParams()
    {
        Vector3 worldVelocity = agent.velocity;
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);

        float maxSpeed = agent.speed;
        if (maxSpeed > 0.01f)
        {
            localVelocity.x /= maxSpeed;
            localVelocity.z /= maxSpeed;
        }

        _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, localVelocity, Time.deltaTime / 0.1f);

        Animator animator = _currentAnimator;
        animator.SetFloat(VelocityX, _smoothedVelocity.x);
        animator.SetFloat(VelocityZ, _smoothedVelocity.z);
    }
    #endregion

    #region Network Events
    private void HandleSpawnMusket(NetworkObjectReference objectReference, int scrapValue)
    {
        if (!objectReference.TryGet(out NetworkObject networkObject))
        {
            BiodiversityPlugin.Logger.LogError("[WaxSoldierClient] Received null network object for the musket.");
            return;
        }

        if (!networkObject.TryGetComponent(out Musket receivedMusket))
        {
            BiodiversityPlugin.Logger.LogError("[WaxSoldierClient] The musket component on the musket network object is null.");
            return;
        }
        
        _musket = receivedMusket;
        _musket.SetScrapValue(scrapValue);
        _musket.parentObject = musketContainer;
        _musket.OnGrabbedByWaxSoldier(_enemyAIReference.Value);
    }

    private void HandleDropMusket()
    {
        if (!_musket) return;
        BiodiversityPlugin.LogVerbose("[WaxSoldierClient] Dropping musket...");
        
        _musket.OnDroppedByWaxSoldier();
        _musket.parentObject = null;
        _musket.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        _musket.EnablePhysics(true);
        _musket.fallTime = 0f;

        Transform parent;
        _musket.startFallingPosition =
            (parent = _musket.transform.parent).InverseTransformPoint(_musket.transform.position);
        _musket.targetFloorPosition = parent.InverseTransformPoint(transform.position);
        _musket = null;
    }
    
    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        // todo: make similar logging setup like in BiodiverseAI for the client classes
        _targetPlayer.Set(newValue == BiodiverseAI.NullPlayerId ? null : PlayerUtil.GetPlayerFromClientId(newValue));
        BiodiversityPlugin.LogVerbose(_targetPlayer.HasValue
            ? $"Changed target player to {_targetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }
    
    /// <summary>
    /// Sets a trigger in the <see cref="_currentAnimator"/>.
    /// </summary>
    /// <param name="parameter">The name of the trigger in the <see cref="_currentAnimator"/>.</param>
    private void HandleSetAnimationTrigger(int parameter)
    {
        _currentAnimator.SetTrigger(parameter);
    }

    private void HandleSetAnimationControllerToFrozen(bool setToFrozen)
    {
        if (setToFrozen)
        {
            if (_currentAnimator.speed != 0)
            {
                _previousAnimatorSpeedBeforeFreeze = _currentAnimator.speed;
            }
            
            _currentAnimator.speed = 0;
        }
        else
        {
            if (_previousAnimatorSpeedBeforeFreeze != 0)
            {
                _currentAnimator.speed = _previousAnimatorSpeedBeforeFreeze;
            }
            else
            {
                _currentAnimator.speed = 1;
            }
        }
    }

    private void HandleSlamIntoGround()
    {
        float localPlayerDistanceToBody = Vector3.Distance(transform.position, HUDManager.Instance.localPlayer.transform.position);
        
        if (localPlayerDistanceToBody <= 10f) 
        {
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
        }
        else if (localPlayerDistanceToBody <= 5f)
        {
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
        }
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;
        
        netcodeController.OnSpawnMusket += HandleSpawnMusket;
        netcodeController.OnDropMusket += HandleDropMusket;
        netcodeController.OnSetAnimationTrigger += HandleSetAnimationTrigger;
        netcodeController.OnSetAnimationControllerToFrozen += HandleSetAnimationControllerToFrozen;
        netcodeController.OnSlamIntoGround += HandleSlamIntoGround;

        //netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;
        
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;
        
        netcodeController.OnSpawnMusket -= HandleSpawnMusket;
        netcodeController.OnDropMusket -= HandleDropMusket;
        netcodeController.OnSetAnimationTrigger -= HandleSetAnimationTrigger;
        netcodeController.OnSetAnimationControllerToFrozen -= HandleSetAnimationControllerToFrozen;
        netcodeController.OnSlamIntoGround -= HandleSlamIntoGround;

        //netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;
        
        _networkEventsSubscribed = false;
    }
    #endregion
}