using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierClient : MonoBehaviour
{
#pragma warning disable 0649
    [Header("Controllers")] [Space(5f)] 
    [SerializeField] private WaxSoldierNetcodeController netcodeController;
    
    [Header("Other")] [Space(5f)] 
    [SerializeField] private GameObject scanNode;
#pragma warning restore 0649
    
    private void Awake()
    {
        if (netcodeController == null) netcodeController = GetComponent<WaxSoldierNetcodeController>();
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    private void SubscribeToNetworkEvents()
    {
        
    }

    private void UnsubscribeFromNetworkEvents()
    {
        
    }
}