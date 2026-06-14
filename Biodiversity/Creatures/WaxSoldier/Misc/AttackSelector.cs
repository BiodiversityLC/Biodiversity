using Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;
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

    // todo: absorb this whole class into WaxSoldierAI

    private void Start()
    {
        SpinAttack spinAttack = new(
            WaxSoldierClient.SpinAttack, 0f, 2f, 2f, 3);

        StabAttack stabAttack = new(
            WaxSoldierClient.StabAttack, 0f, 5f, 1.5f, 1);

        ShootAttack shootAttack = new(
            WaxSoldierClient.AimMusket, 2f, 200f, 4f, 0);

        LungeAttack lungeAttack = new(
            WaxSoldierClient.LungeAttack, 2f, 6f, 3f, 0);

        SwingAttack swingAttack = new(
            WaxSoldierClient.SwingAttack, 2f, 2f, 1.5f, 0);

        FlailAttack flailAttack = new(
            WaxSoldierClient.FlailAttack, 0f, 2f, 1.5f, 0);

        availableAttacks.AddRange([spinAttack, stabAttack, shootAttack, lungeAttack, swingAttack, flailAttack]);
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

    public AttackAction SelectAttack(WaxSoldierAI ai)
    {
        // BiodiversityPlugin.LogVerbose($"In {nameof(SelectAttack)}");
        if (!ai.Context.Adapter.TargetPlayer) return null;

        for (int i = 0; i < availableAttacks.Count; i++)
        {
            AttackAction attack = availableAttacks[i];
            // BiodiversityPlugin.LogVerbose($"Checking attack[{i}] = priority={attack.Priority} (MinRange={attack.MinRange}, MaxRange={attack.MaxRange}, RequiresLOS={attack.RequiresLineOfSight})");

            if (cooldowns.ContainsKey(attack) && cooldowns[attack] > 0)
            {
                // BiodiversityPlugin.LogVerbose($"Attack is on cooldown ({cooldowns[attack]} seconds left). Skipping.");
                continue;
            }

            if (!attack.AreRequirementsMet(ai.Context)) continue;

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