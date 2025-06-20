using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierClient : MonoBehaviour
{
    public static readonly int Spawning = Animator.StringToHash("Spawning");
    
    #region Unity Inspector Variables

#pragma warning disable 0649
    [SerializeField] private GameObject unmoltenGameObject;
    [SerializeField] private GameObject moltenGameObject;
    
    [Header("Animation")] [Space(5f)] 
    [SerializeField] private Animator animator;
    
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private WaxSoldierNetcodeController netcodeController;
#pragma warning restore 0649

    #endregion
    
    private CachedNullable<PlayerControllerB> _targetPlayer;

    private bool _networkEventsSubscribed;
    
    private void Awake()
    {
        if (!netcodeController) netcodeController = GetComponent<WaxSoldierNetcodeController>();
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
        animator.SetBool(Spawning, true);
    }
    
    private void HandleTargetPlayerChanged(ulong oldValue, ulong newValue)
    {
        // todo: make similar logging setup like in BiodiverseAI for the client classes
        _targetPlayer.Set(newValue == BiodiverseAI.NullPlayerId ? null : StartOfRound.Instance.allPlayerScripts[newValue]);
        BiodiversityPlugin.LogVerbose(_targetPlayer.HasValue
            ? $"Changed target player to {_targetPlayer.Value?.playerUsername}."
            : "Changed target player to null.");
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;

        netcodeController.TargetPlayerClientId.OnValueChanged += HandleTargetPlayerChanged;
        
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;

        netcodeController.TargetPlayerClientId.OnValueChanged -= HandleTargetPlayerChanged;
        
        _networkEventsSubscribed = false;
    }
}