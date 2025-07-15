using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier.Misc;

public class AttackSelector : NetworkBehaviour
{
    private List<AttackAction> availableAttacks = [];
    private readonly Dictionary<AttackAction, float> cooldowns = new();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer)
        {
            enabled = false;
        }
    }

    private void Start()
    {
        AttackAction spinAttack = new(
            WaxSoldierClient.SpinAttack, 0f, 2f, 2f, false, 3);
        
        AttackAction stabAttack = new(
            WaxSoldierClient.StabAttack, 0f, 6f, 2f, true, 1);
        
        AttackAction shootAttack = new(
            WaxSoldierClient.AimMusket, 2f, 200f, 2f, true, 0);
        
        availableAttacks.AddRange([spinAttack, stabAttack, shootAttack]);
        availableAttacks = availableAttacks.OrderByDescending(a => a.Priority).ToList();
    }

    private void Update()
    {
        // Process cooldowns
        if (cooldowns.Count > 0)
        {
            List<AttackAction> keys = cooldowns.Keys.ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                AttackAction attack = keys[i];
                cooldowns[attack] -= Time.deltaTime;
            }
        }
    }

    public AttackAction SelectAttack(WaxSoldierAI ai, PlayerControllerB target, float distanceToTarget = -1f)
    {
        // BiodiversityPlugin.LogVerbose($"In {nameof(SelectAttack)}");
        if (!target) return null;
        
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (distanceToTarget == -1f)
        {
            distanceToTarget = Vector3.Distance(target.transform.position, ai.transform.position);
        }
        // BiodiversityPlugin.LogVerbose($"distanceToTarget = {distanceToTarget}");

        for (int i = 0; i < availableAttacks.Count; i++)
        {
            AttackAction attack = availableAttacks[i];
            // BiodiversityPlugin.LogVerbose($"Checking attack[{i}] = priority={attack.Priority} (MinRange={attack.MinRange}, MaxRange={attack.MaxRange}, RequiresLOS={attack.RequiresLineOfSight})");

            if (cooldowns.ContainsKey(attack) && cooldowns[attack] > 0)
            {
                // BiodiversityPlugin.LogVerbose($"Attack is on cooldown ({cooldowns[attack]} seconds left). Skipping.");
                continue;
            }

            if (distanceToTarget >= attack.MaxRange || distanceToTarget <= attack.MinRange)
            {
                // BiodiversityPlugin.LogVerbose($"Attack failed: {distanceToTarget} >= {attack.MaxRange} || {distanceToTarget} <= {attack.MinRange}");
                continue;
            }

            if (attack.RequiresLineOfSight)
            {
                bool hasLineOfSightToTarget = ai.HasLineOfSight(
                    target.gameplayCamera.transform.position, ai.Context.Adapter.EyeTransform,
                    ai.Context.Blackboard.ViewWidth, ai.Context.Blackboard.ViewRange, 1f);
                if (!hasLineOfSightToTarget)
                {
                    // BiodiversityPlugin.LogVerbose("LOS check failed");
                    continue;
                }
            }

            return attack;
        }

        return null;
    }
    
    public void StartCooldown(AttackAction attack)
    {
        if (attack == null) return;
        cooldowns[attack] = attack.Cooldown;
    }
}