using Biodiversity;
using Biodiversity.Util;
using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

// This component is used for bayonet collision detection when wielded by the Wax Soldier.
// The collisions for when it is wielded by the player, is handled in the main Musket script (it is meant to mimic how the shovel works (monty wanted it this way)).
public class MusketBayonetCollisionDetection : NetworkBehaviour
{
    [SerializeField] private Transform bayonetTip;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stabIntoFleshSfx;
    
    public enum BayonentMode
    {
        Spin,
        Stab
    }

    [NonSerialized] public BayonentMode bayonetMode;
    
    private int spinDamageToPlayers;
    private int stabDamageToPlayers;

    private float spinKnockback;
    private float stabKnockback;

    private float hitCooldown;
    private Dictionary<ulong, float> playersHitByBayonet;

    public bool colliderEnabled;
    
    private void Start()
    {
        if (!IsServer) return;
        
        spinDamageToPlayers = 40;
        stabDamageToPlayers = 50;
        
        spinKnockback = 4f;
        stabKnockback = 2f;

        hitCooldown = 0.5f;
        playersHitByBayonet = new Dictionary<ulong, float>(StartOfRound.Instance.allPlayerScripts.Length);

        colliderEnabled = false;
    }

    private void Update()
    {
        if (!IsServer) return;

        List<ulong> ids = playersHitByBayonet.Keys.ToList();
        for (int i = 0; i < ids.Count; i++)
        {
            ulong id = ids[i];
            playersHitByBayonet[id] -= Time.deltaTime;
            if (playersHitByBayonet[id] <= 0)
                playersHitByBayonet.Remove(id);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !colliderEnabled) return;
        if (other.CompareTag("Player") && other.TryGetComponent(out PlayerControllerB player) &&
            !playersHitByBayonet.ContainsKey(player.actualClientId) && !PlayerUtil.IsPlayerDead(player))
        {
            BiodiversityPlugin.LogVerbose($"Hitting player {player.playerUsername}");
            playersHitByBayonet.Add(player.actualClientId, hitCooldown);

            int damage = 0;
            float forceScalar = 0;
            switch (bayonetMode)
            {
                case BayonentMode.Spin:
                    damage = spinDamageToPlayers;
                    forceScalar = spinKnockback;
                    break;
                case BayonentMode.Stab:
                    damage = stabDamageToPlayers;
                    forceScalar = stabKnockback;
                    break;
            }
            
            DamagePlayerClientRpc(player.actualClientId, damage, CauseOfDeath.Stabbing, forceScalar * (bayonetTip.forward * -1));
        }
    }
    
    #region RPCs
    [ClientRpc]
    private void DamagePlayerClientRpc(ulong playerId, int damage, CauseOfDeath causeOfDeath = CauseOfDeath.Unknown, Vector3 force = default)
    {
        PlayerControllerB player = PlayerUtil.GetPlayerFromClientId(playerId);
        if (!player) return;
        if (player == GameNetworkManager.Instance.localPlayerController)
            player.DamagePlayer(damage, true, true, causeOfDeath, 0, true, force);
        
        float oldPitch = audioSource.pitch;
        if (audioSource.isPlaying) audioSource.Stop();
        audioSource.pitch = UnityEngine.Random.Range(oldPitch - 0.1f, oldPitch + 0.1f);
        audioSource.PlayOneShot(stabIntoFleshSfx);
        WalkieTalkie.TransmitOneShotAudio(audioSource, stabIntoFleshSfx, audioSource.volume);
        audioSource.pitch = oldPitch;
    }
    #endregion
}