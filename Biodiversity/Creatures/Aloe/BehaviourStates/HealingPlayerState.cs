using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.HealingPlayer)]
internal class HealingPlayerState : BehaviourState<AloeServerAI.States, AloeServerAI>
{
    private int _healingPerInterval;

    private bool _finishedHealing;

    public HealingPlayerState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToCuddlingPlayer(EnemyAIInstance, this),
            new TransitionToRoaming(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        EnemyAIInstance.AgentMaxSpeed = 0f;
        EnemyAIInstance.AgentMaxAcceleration = 50f;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.openDoorSpeedMultiplier = AloeHandler.Instance.Config.OpenDoorSpeedMultiplier;
        
        // Start healing/damage the player
        EnemyAIInstance.LogVerbose(AloeHandler.Instance.Config.DamageInsteadOfHeal
            ? "Starting to damage the player."
            : "Starting to heal the player.");
        
        EnemyAIInstance.netcodeController.ShouldHaveDarkSkin.SafeSet(false);
        EnemyAIInstance.netcodeController.TargetPlayerClientId.SafeSet(PlayerUtil.GetClientIdFromPlayer(EnemyAIInstance.ActualTargetPlayer.Value));
        EnemyAIInstance.netcodeController.AnimationParamHealing.SafeSet(true);
        EnemyAIInstance.netcodeController.SetTargetPlayerAbleToEscapeClientRpc(true);
        EnemyAIInstance.netcodeController.UnMuffleTargetPlayerVoiceClientRpc();
        
        if (AloeHandler.Instance.Config.DamageInsteadOfHeal)
        {
            _healingPerInterval = Mathf.CeilToInt(AloeHandler.Instance.Config.DamageDeltPerSecondInsteadOfHealing);
        }
        else
        {
            int playerMaxHealth = AloeSharedData.Instance.GetPlayerMaxHealth(EnemyAIInstance.ActualTargetPlayer.Value);
            if (EnemyAIInstance.ActualTargetPlayer.Value.health == playerMaxHealth)
            {
                EnemyAIInstance.LogVerbose("Target player is already at max health, switching to cuddling player.");
                EnemyAIInstance.SwitchBehaviourState(AloeServerAI.States.CuddlingPlayer);
                return;
            }
            
            // Calculate the heal amount per AIInterval
            float baseHealingRate = 100f / EnemyAIInstance.TimeItTakesToFullyHealPlayer;
            float healingRate = baseHealingRate * playerMaxHealth / 100f;
            _healingPerInterval = Mathf.CeilToInt(healingRate * EnemyAIInstance.AIIntervalTime);

            // Calculate the total time it takes to heal the player
            float totalHealingTime = (playerMaxHealth - EnemyAIInstance.ActualTargetPlayer.Value.health) / healingRate;
            EnemyAIInstance.netcodeController.PlayHealingVfxClientRpc(totalHealingTime);
            EnemyAIInstance.PlayRandomAudioClipTypeServerRpc(
                nameof(AloeClient.AudioClipTypes.healingSfx), nameof(AloeClient.AudioSourceTypes.aloeVoiceSource));
            EnemyAIInstance.ActualTargetPlayer.Value
                .HealServerRpc(); // Doesn't actually heal them, just makes them not bleed anymore
        }
    }

    private float secondsTimer;

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        if (AloeSharedData.Instance.BrackenRoomDoorPosition != Vector3.zero)
            EnemyAIInstance.LookAtPosition(AloeSharedData.Instance.BrackenRoomDoorPosition);

        if (AloeHandler.Instance.Config.DamageInsteadOfHeal)
        {
            secondsTimer += Time.deltaTime;
            if (secondsTimer >= 1f)
            {
                secondsTimer -= 1f;
                EnemyAIInstance.netcodeController.DamagePlayerClientRpc(PlayerUtil.GetClientIdFromPlayer(EnemyAIInstance.ActualTargetPlayer.Value), _healingPerInterval);
                EnemyAIInstance.LogVerbose($"Damaged player by amount: {_healingPerInterval}");
            }
        }
        else
        {
            int targetPlayerMaxHealth = AloeSharedData.Instance.GetPlayerMaxHealth(EnemyAIInstance.ActualTargetPlayer.Value);
            if (EnemyAIInstance.ActualTargetPlayer.Value.health < targetPlayerMaxHealth)
            {
                // First check if the current heal amount will give the player too much health
                int healthIncrease = _healingPerInterval;
                if (EnemyAIInstance.ActualTargetPlayer.Value.health + _healingPerInterval >= targetPlayerMaxHealth)
                {
                    healthIncrease = targetPlayerMaxHealth - EnemyAIInstance.ActualTargetPlayer.Value.health;
                }

                _finishedHealing = false;

                // Todo: move this to clientside
                EnemyAIInstance.netcodeController.HealTargetPlayerByAmountClientRpc(healthIncrease);
                EnemyAIInstance.LogVerbose($"Healed player by amount: {healthIncrease}");
            }
            // If the player cannot be healed anymore, then switch to cuddling
            else
            {
                _finishedHealing = true;
            }
        }
    }

    private class TransitionToCuddlingPlayer(AloeServerAI enemyAIInstance, HealingPlayerState healingPlayerState)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return healingPlayerState._finishedHealing;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.CuddlingPlayer;
        }
    }
    
    private class TransitionToRoaming(AloeServerAI enemyAIInstance)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return EnemyAIInstance.ActualTargetPlayer.Value.isPlayerDead;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.Roaming;
        }

        internal override void OnTransition()
        {
            base.OnTransition();
            EnemyAIInstance.SetTargetPlayerInCaptivity(false);
        }
    }
}