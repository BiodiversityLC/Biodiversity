using Biodiversity.General;
using Biodiversity.Util;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using GameNetcodeStuff;
using System.Linq;
using Unity.Netcode;
using Biodiversity.Creatures.HoneyFeeder;

namespace Biodiversity.Creatures.WaxSoldier;

public class WaxSoldierAI : BiodiverseAI
{
    public enum AIStates
    {
        STATIONARY, // Remain stationary and wait for a player to pass by
        PURSUING, //Go after the player, changing into the appropriate attacking state and switching back to this state
        ATTACKING_MUSKET, //Shoot musket, can only be used once because of the ammo capacity
        ATTACKING_BAYONET, //Use bayonet
        SEARCHING, // Searching for player after being out of sight and range (Should this state exist or go straight to RETURNING ?)
        RETURNING, // Return to guardLocation after having lost the player for x amount of time
        RELOADING  // Have another state after being returned for the soldier to reload etc? then return to stationary
    }

    public enum MoltenStates
    {
        NORMAL,
        MOLTEN //When exposed to strong heat, melt into damaged/molten form (What would strong heat be?)
    }

    private Transform guardLocation;

    [field: SerializeField]
    public WaxSoldierConfig Config { get; private set; } = BiodiversityPlugin.configWaxSoldier;

    private AIStates _state = AIStates.STATIONARY;
    private AIStates _prevState = AIStates.STATIONARY;
    private MoltenStates moltenState = MoltenStates.NORMAL;

    public AIStates State
    {
        get { return _state; }
        private set
        {
            Log($"Updating state: {_state} -> {value}");
            moveTowardsDestination = false;
            movingTowardsTargetPlayer = false;
            agent.speed = Config.NormalSpeed;
            if (currentSearch.inProgress) StopSearch(currentSearch, true);

            _prevState = _state;
            _state = value;
        }
    }

    public override void Start()
    {
        base.Start();
    }

    private void Log(string message)
    {
        BiodiversityPlugin.Logger.LogInfo($"[WaxSoldier] " + message);
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (!IsHost) return;
        if (State != AIStates.ATTACKING_BAYONET) return;
        if (other.TryGetComponent(out PlayerControllerB player))
        {
            HitPlayerClientRpc((int)player.playerClientId);
        }
    }

    [ClientRpc]
    void HitPlayerClientRpc(int playerId)
    {
        PlayerControllerB hitPlayer = PlayerUtil.GetPlayerFromClientId(playerId);

        if (hitPlayer == GameNetworkManager.Instance.localPlayerController)
        {
            CauseOfDeath causeOfDeathValue;
            int damageValue;
            switch (State) //Would if and else be better?
            {
                case AIStates.ATTACKING_MUSKET:
                    causeOfDeathValue = CauseOfDeath.Gunshots;
                    damageValue = Config.MusketDamage;
                    break;
                default:
                    causeOfDeathValue = CauseOfDeath.Bludgeoning;
                    damageValue = Config.BayonetDamage;
                    break;
            }
            hitPlayer.DamagePlayer(damageValue, causeOfDeath: causeOfDeathValue);

        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);

        targetPlayer = playerWhoHit;
        State = AIStates.ATTACKING_BAYONET;
    }
}
