using Biodiversity.Util.Types;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biodiversity.Items.DeveloperItems;

public class RubberDuckBehaviour : PhysicsProp
{
#pragma warning disable 0649
    [SerializeField] private Material[] materialVariants;
    [SerializeField] private AudioClip[] audioClipVariants;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animator animator;
#pragma warning restore 0649
    
    private bool _networkEventsSubscribed;
    private bool _loadedVariantFromSave;
    
    private CachedValue<NetworkObject> _networkObject;
    
    private readonly NetworkVariable<int> _variantIndex = new(-1);

    private void Awake()
    {
        _networkObject = new CachedValue<NetworkObject>(GetComponent<NetworkObject>, true);
    }
    
    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
    }

    public override void Start()
    {
        base.Start();
        SubscribeToNetworkEvents();

        if (IsServer)
        {
            Random.InitState(StartOfRound.Instance.randomMapSeed + Guid.NewGuid().GetHashCode());
            if (!_loadedVariantFromSave) _variantIndex.Value = Random.Range(0, materialVariants.Length);
        }
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        PlayQuackServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayQuackServerRpc()
    {
        int clipIndex = Random.Range(0, audioClipVariants.Length);
        PlayQuackClientRpc(clipIndex);
    }

    [ClientRpc]
    private void PlayQuackClientRpc(int clipIndex)
    {
        AudioClip clip = audioClipVariants[clipIndex];
        
        audioSource.Stop(true);
        animator.Play("Squeeze", -1, 0);
        audioSource.PlayOneShot(clip);
        WalkieTalkie.TransmitOneShotAudio(audioSource, clip, audioSource.volume);
        RoundManager.Instance.PlayAudibleNoise(transform.position, (audioSource.minDistance + audioSource.maxDistance) / 2);
    }
    
    public override int GetItemDataToSave()
    {
        return _variantIndex.Value;
    }

    public override void LoadItemSaveData(int saveData)
    {
        _loadedVariantFromSave = true;
        StartCoroutine(ApplyItemSaveData(saveData));
    }

    private IEnumerator ApplyItemSaveData(int loadedVariantIndex)
    {
        while (!_networkObject.Value.IsSpawned)
        {
            yield return null;
        }

        _variantIndex.Value = loadedVariantIndex;
    }
    
    private void OnVariantIndexChanged(int oldValue, int newValue)
    {
        ApplyVariant(newValue);
    }
    
    private void ApplyVariant(int chosenVariantIndex)
    {
        if (materialVariants.Length > 0) mainObjectRenderer.material = materialVariants[chosenVariantIndex];
        else BiodiversityPlugin.Logger.LogError($"Nethersome's rubber duck item: No material variants available with index: {chosenVariantIndex}.");
    }
    
    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed) return;
        _variantIndex.OnValueChanged += OnVariantIndexChanged;
        _networkEventsSubscribed = true;
    }
    
    private void UnsubscribeFromNetworkEvents()
    {
        if (!_networkEventsSubscribed) return;
        _variantIndex.OnValueChanged -= OnVariantIndexChanged;
        _networkEventsSubscribed = false;
    }
}