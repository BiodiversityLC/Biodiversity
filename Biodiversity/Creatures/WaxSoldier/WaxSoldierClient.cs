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
    public static readonly int Spawning = Animator.StringToHash("Spawning");
    public static readonly int SpinAttack = Animator.StringToHash("SpinAttack");
    public static readonly int StabAttack = Animator.StringToHash("StabAttack");
    
    public static readonly int VelocityX = Animator.StringToHash("VelocityX");
    public static readonly int VelocityZ = Animator.StringToHash("VelocityZ");
    #endregion
    
    #region Unity Inspector Variables
#pragma warning disable 0649
    [SerializeField] private NavMeshAgent agent;
    
    [SerializeField] private GameObject unmoltenGameObject;
    [SerializeField] private GameObject moltenGameObject;
    
    [Header("Animation")] [Space(5f)] 
    [SerializeField] private Animator unmoltenAnimator;
    [SerializeField] private Animator moltenAnimator;
    [SerializeField] private Transform musketContainer;
    
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private WaxSoldierNetcodeController netcodeController;
#pragma warning restore 0649
    #endregion

    private Animator currentAnimator;
    
    private CachedNullable<PlayerControllerB> _targetPlayer;
    private CachedValue<EnemyAI> enemyAIReference;
    private Musket musket;
    
    private Vector3 smoothedVelocity;

    private bool _networkEventsSubscribed;
    
    private void Awake()
    {
        if (!netcodeController) netcodeController = GetComponent<WaxSoldierNetcodeController>();
        enemyAIReference = new CachedValue<EnemyAI>(GetComponent<EnemyAI>);
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
        currentAnimator = unmoltenAnimator;
    }
    
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

        smoothedVelocity = Vector3.Lerp(smoothedVelocity, localVelocity, Time.deltaTime / 0.1f);

        Animator animator = currentAnimator;
        animator.SetFloat(VelocityX, smoothedVelocity.x);
        animator.SetFloat(VelocityZ, smoothedVelocity.z);
    }

    private void HandleSpawnMusket(NetworkObjectReference objectReference, int scrapValue)
    {
        if (!objectReference.TryGet(out NetworkObject networkObject))
        {
            BiodiversityPlugin.Logger.LogError("Received null network object for the musket.");
            return;
        }

        if (!networkObject.TryGetComponent(out Musket receivedMusket))
        {
            BiodiversityPlugin.Logger.LogError("The musket component on the musket network object is null.");
            return;
        }

        musket = receivedMusket;
        musket.SetScrapValue(scrapValue);
        musket.parentObject = musketContainer;
        musket.GrabItemFromEnemy(enemyAIReference.Value);
        musket.OnGrabbedByWaxSoldier();
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
    /// Sets a trigger in the <see cref="currentAnimator"/>.
    /// </summary>
    /// <param name="parameter">The name of the trigger in the <see cref="currentAnimator"/>.</param>
    private void HandleSetAnimationTrigger(int parameter)
    {
        currentAnimator.SetTrigger(parameter);
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;
        
        netcodeController.OnSpawnMusket += HandleSpawnMusket;
        netcodeController.OnSetAnimationTrigger += HandleSetAnimationTrigger;

        netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;
        
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;
        
        netcodeController.OnSpawnMusket -= HandleSpawnMusket;
        netcodeController.OnSetAnimationTrigger -= HandleSetAnimationTrigger;

        netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;
        
        _networkEventsSubscribed = false;
    }
}