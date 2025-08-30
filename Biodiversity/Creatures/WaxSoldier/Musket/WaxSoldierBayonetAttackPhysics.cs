using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// This component is used for bayonet collision detection when wielded by the Wax Soldier.
/// The collisions for when it is wielded by the player, is handled in the main Musket script (it is meant to mimic how the shovel works (monty wanted it this way)).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class WaxSoldierBayonetAttackPhysics : NetworkBehaviour
{
    [Header("Attack Properties")]
    [SerializeField] private int spinDamage = 40;
    [SerializeField] private float spinKnockback = 16f;
    [SerializeField] private int stabDamage = 50;
    [SerializeField] private float stabKnockback = 8f;
    
    [Tooltip("How long (in seconds) a player is immune after being hit.")]
    [SerializeField] private float hitCooldown = 0.5f;
    
    [Header("References")]
    [SerializeField] private Transform bayonetTip;
    [SerializeField] public BoxCollider bayonetCollider;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stabIntoFleshSfx;
    
    public enum BayonentMode { None, Spin, Stab }
    public BayonentMode currentBayonetMode = BayonentMode.None;
    
    private Collider[] bayonetHitBuffer;
    
    private readonly PlayerCooldownTracker _playerCooldownTracker = new();
    
    private CachedValue<Vector3> _bayonetColliderHalfExtents;
    private Vector3 previousTipPosition;
    
    private int _bayonetHitMask;
    
    private void Awake()
    {
        bayonetHitBuffer = new Collider[15];
        _bayonetHitMask = 1084754248; // This is the mask used by the vanilla shovel, see Util.VanillaLayersUtil for more details

        if (TryGetComponent(out Rigidbody rb))
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
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

    private void Start()
    {
        _bayonetColliderHalfExtents = new CachedValue<Vector3>(() => bayonetCollider.size * 0.75f);
    }

    private void FixedUpdate()
    {
        if (currentBayonetMode == BayonentMode.None) return;
        
        Vector3 currentTipPosition = bayonetTip.position;
        Vector3 direction = currentTipPosition - previousTipPosition;

        // Only do the cast if the tip has moved
        if (direction.magnitude > 0.01f)
        {
            int hits = Physics.OverlapBoxNonAlloc(bayonetCollider.transform.position, _bayonetColliderHalfExtents.Value, 
                bayonetHitBuffer, bayonetCollider.transform.rotation, _bayonetHitMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits; i++)
            {
                Collider hit = bayonetHitBuffer[i];
                TryToDamageEntity(hit);
            }
        }

        previousTipPosition = currentTipPosition;
    }

    private bool TryToDamageEntity(Collider entity)
    {
        if (entity.CompareTag("Player") && entity.TryGetComponent(out PlayerControllerB player) && !PlayerUtil.IsPlayerDead(player))
        {
            ulong playerClientId = PlayerUtil.GetClientIdFromPlayer(player);
            if (_playerCooldownTracker.IsOnCooldown(playerClientId))
                return false;
            
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
            DamagePlayerClientRpc(playerClientId, damage, forceDirection * knockback, CauseOfDeath.Stabbing);
            _playerCooldownTracker.Start(playerClientId, hitCooldown);
            
            return true;
        }

        return false;
    }

    public void BeginAttack(BayonentMode mode)
    {
        currentBayonetMode = mode;
        previousTipPosition = bayonetTip.transform.position;
        enabled = true; // Enables FixedUpdate
    }

    public void EndAttack()
    {
        currentBayonetMode = BayonentMode.None;
        enabled = false; // Disables FixedUpdate
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