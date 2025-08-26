using System;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierNetcodeController : NetworkBehaviour
{
    internal readonly NetworkVariable<ulong> TargetPlayerClientId = new();
    
    internal readonly NetworkVariable<bool> AnimationParamInSalute = new();
    internal readonly NetworkVariable<bool> AnimationParamIsDead = new();

    internal event Action<NetworkObjectReference, int> OnSpawnMusket;
    internal event Action OnDropMusket;
    internal event Action<int> OnSetAnimationTrigger;

    [ClientRpc]
    public void DropMusketClientRpc()
    {
        OnDropMusket?.Invoke();
    }
    
    [ServerRpc(RequireOwnership = true)]
    public void SpawnMusketServerRpc()
    {
        GameObject musketObject = Instantiate(
            WaxSoldierHandler.Instance.Assets.MusketItemData.spawnPrefab,
            transform.position,
            Quaternion.identity,
            RoundManager.Instance.spawnedScrapContainer);

        if (!musketObject)
        {
            BiodiversityPlugin.Logger.LogError("The musket object that was instantiated, is null.");
            return;
        }

        if (!musketObject.TryGetComponent(out Musket musket))
        {
            BiodiversityPlugin.Logger.LogError("Could not get musket component from musket object.");
            return;
        }
        
        int scrapValue = UnityEngine.Random.Range(WaxSoldierHandler.Instance.Config.MusketMinimumValue, WaxSoldierHandler.Instance.Config.MusketMaximumValue + 1);
        RoundManager.Instance.totalScrapValueInLevel += scrapValue;
        
        musket.GetComponent<NetworkObject>().Spawn();
        SpawnMusketClientRpc(musketObject, scrapValue);
    }

    [ClientRpc]
    private void SpawnMusketClientRpc(NetworkObjectReference objectReference, int scrapValue)
    {
        OnSpawnMusket?.Invoke(objectReference, scrapValue);
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
}