using Biodiversity;
using Biodiversity.Util;
using GameNetcodeStuff;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// This component is used for bayonet collision detection when wielded by the Wax Soldier.
/// The collisions for when it is wielded by the player, is handled in the main Musket script (it is meant to mimic how the shovel works (monty wanted it this way)).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class MusketBayonetHitbox : NetworkBehaviour
{
    [Header("Configuration")]
    [Tooltip("The layers that the bayonet can hit.")]
    [SerializeField] private LayerMask hitLayers;
    [Tooltip("How long (in seconds) a player is immune after being hit.")]
    [SerializeField] private float hitCooldown = 1f;
    
    [Header("Attack Properties")]
    [SerializeField] private int spinDamage = 40;
    [SerializeField] private float spinKnockback = 16f;
    [SerializeField] private int stabDamage = 50;
    [SerializeField] private float stabKnockback = 8f;
    
    [Header("References")]
    [SerializeField] private Transform bayonetTip;
    [SerializeField] public BoxCollider bayonetCollider;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stabIntoFleshSfx;
    
    public enum BayonentMode { None, Spin, Stab }
    public BayonentMode currentBayonetMode = BayonentMode.None;
    
    private Collider[] bayonetHitBuffer;
    
    private readonly Dictionary<PlayerControllerB, float> cooldownTracker = new();
    private readonly List<PlayerControllerB> cooldownsToRemove = [];

    private Vector3 previousTipPosition;

    private void OnValidate()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (!rb.isKinematic)
        {
            Debug.LogWarning("Rigidbody on BayonetHitbox is not Kinematic. This is required for animation-driven hit detection. Forcing it to true.", this);
            rb.isKinematic = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        // Ensure the hitbox is off by default
        EndAttack();
    }

    private void Awake()
    {
        bayonetHitBuffer = new Collider[35];
    }

    private void FixedUpdate()
    {
        if (currentBayonetMode == BayonentMode.None) return;
        
        Vector3 currentTipPosition = bayonetTip.position;
        Vector3 direction = currentTipPosition - previousTipPosition;
        float distance = direction.magnitude;

        // Only do the cast if the tip has moved
        if (distance > 0.01f)
        {
            int hits = Physics.OverlapBoxNonAlloc(bayonetCollider.transform.position, bayonetCollider.size * 0.75f, 
                bayonetHitBuffer, Quaternion.identity, hitLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits; i++)
            {
                Collider hit = bayonetHitBuffer[i];
                TryToDamageEntity(hit);
            }
        }

        previousTipPosition = currentTipPosition;
        UpdateCooldowns();
    }

    private bool TryToDamageEntity(Collider entity)
    {
        BiodiversityPlugin.LogVerbose($"Trying to damage {entity.name}");
        if (entity.CompareTag("Player") && entity.TryGetComponent(out PlayerControllerB player) && !PlayerUtil.IsPlayerDead(player) && !cooldownTracker.ContainsKey(player))
        {
            int damage = 0;
            float knockback = 0f;
            
            switch (currentBayonetMode)
            {
                case BayonentMode.Spin:
                    damage = spinDamage;
                    knockback = spinKnockback;
                    break;
                case BayonentMode.Stab:
                    damage = stabDamage;
                    knockback = stabKnockback;
                    break;
            }

            Vector3 forceDirection = (player.transform.position - transform.position).normalized;
            DamagePlayerClientRpc(player.actualClientId, damage, forceDirection * knockback, CauseOfDeath.Stabbing);
            cooldownTracker.Add(player, Time.time + hitCooldown);
            return true;
        }

        return false;
    }

    public void BeginAttack(BayonentMode mode)
    {
        currentBayonetMode = mode;
        cooldownTracker.Clear();
        previousTipPosition = bayonetTip.transform.position;
        enabled = true; // Enables FixedUpdate
    }

    public void EndAttack()
    {
        currentBayonetMode = BayonentMode.None;
        enabled = false; // Disables FixedUpdate
    }

    private void UpdateCooldowns()
    {
        if (cooldownTracker.Count == 0) return;
        
        cooldownsToRemove.Clear();
        foreach (KeyValuePair<PlayerControllerB, float> entry in cooldownTracker)
        {
            if (Time.time >= entry.Value)
            {
                cooldownsToRemove.Add(entry.Key);
            }
        }

        for (int i = 0; i < cooldownsToRemove.Count; i++)
        {
            PlayerControllerB player = cooldownsToRemove[i];
            cooldownTracker.Remove(player);
        }
    }
    
    #region RPCs
    [ClientRpc]
    private void DamagePlayerClientRpc(ulong playerId, int damage, Vector3 force = default, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown)
    {
        PlayerControllerB player = PlayerUtil.GetPlayerFromClientId(playerId);
        if (!player) return;
        if (player == GameNetworkManager.Instance.localPlayerController)
            player.DamagePlayer(damage, true, true, causeOfDeath, 0, true, force);
        
        float oldPitch = audioSource.pitch;
        if (audioSource.isPlaying) audioSource.Stop();
        audioSource.pitch = Random.Range(oldPitch - 0.1f, oldPitch + 0.1f);
        audioSource.PlayOneShot(stabIntoFleshSfx);
        WalkieTalkie.TransmitOneShotAudio(audioSource, stabIntoFleshSfx, audioSource.volume);
        audioSource.pitch = oldPitch;
    }
    #endregion
}