using Biodiversity.Creatures.Aloe.Types;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class HealingPlayerState : BehaviourState
{
    private int _healingPerInterval;

    private bool _finishedHealing;
    
    public HealingPlayerState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            new TransitionToCuddlingPlayer(aloeServerInstance, this),
        ];
    }

    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agent.speed = 0;
        AloeServerInstance.agentMaxSpeed = 0f;
        AloeServerInstance.agentMaxAcceleration = 50f;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.openDoorSpeedMultiplier = 4f;

        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamHealing, true);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.TargetPlayerClientId,
            AloeServerInstance.ActualTargetPlayer.Value.actualClientId);
        
        AloeServerInstance.netcodeController.SetTargetPlayerAbleToEscapeClientRpc(AloeServerInstance.aloeId, true);
        AloeServerInstance.netcodeController.UnMuffleTargetPlayerVoiceClientRpc(AloeServerInstance.aloeId);
        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(AloeServerInstance.aloeId, 1f, 1f);

        int playerMaxHealth = AloeSharedData.Instance.GetPlayerMaxHealth(AloeServerInstance.ActualTargetPlayer.Value);
        if (AloeServerInstance.ActualTargetPlayer.Value.health == playerMaxHealth)
        {
            AloeServerInstance.LogDebug("Target player is already at max health, switching to cuddling player.");
            AloeServerInstance.SwitchBehaviourState(AloeServer.States.CuddlingPlayer);
            return;
        }
        
        // Start healing the player
        AloeServerInstance.LogDebug("Starting to heal the player");
        
        // Calculate the heal amount per AIInterval
        float baseHealingRate = 100f / AloeServerInstance.TimeItTakesToFullyHealPlayer;
        float healingRate = baseHealingRate * playerMaxHealth / 100f;
        _healingPerInterval = Mathf.CeilToInt(healingRate * AloeServerInstance.AIIntervalTime);
        
        // Calculate the total time it takes to heal the player
        float totalHealingTime = (playerMaxHealth - AloeServerInstance.ActualTargetPlayer.Value.health) / healingRate;
        AloeServerInstance.netcodeController.PlayHealingVfxClientRpc(AloeServerInstance.aloeId, totalHealingTime);
        
        AloeServerInstance.ActualTargetPlayer.Value.HealServerRpc(); // Doesn't actually heal them, just makes them not bleed anymore
    }

    public override void AIIntervalBehaviour()
    {
        AloeServerInstance.netcodeController.LookTargetPosition.Value =
            AloeServerInstance.ActualTargetPlayer.Value.gameplayCamera.transform.position;
        if (AloeSharedData.Instance.BrackenRoomDoorPosition != Vector3.zero)
            AloeServerInstance.LookAtPosition(AloeSharedData.Instance.BrackenRoomDoorPosition);
        
        int targetPlayerMaxHealth = AloeSharedData.Instance.GetPlayerMaxHealth(AloeServerInstance.ActualTargetPlayer.Value);
        if (AloeServerInstance.ActualTargetPlayer.Value.health < targetPlayerMaxHealth)
        {
            // First check if the current heal amount will give the player too much health
            int healthIncrease = _healingPerInterval;
            if (AloeServerInstance.ActualTargetPlayer.Value.health + _healingPerInterval >= targetPlayerMaxHealth)
            {
                healthIncrease = targetPlayerMaxHealth - AloeServerInstance.ActualTargetPlayer.Value.health;
            }
            
            _finishedHealing = false;
            
            // Todo: move this to clientside
            AloeServerInstance.netcodeController.HealTargetPlayerByAmountClientRpc(AloeServerInstance.aloeId, healthIncrease);
            AloeServerInstance.LogDebug($"Healed player by amount: {healthIncrease}");
        }
        // If the player cannot be healed anymore, then switch to cuddling
        else
        {
            _finishedHealing = true;
        }
    }

    private class TransitionToCuddlingPlayer(AloeServer aloeServerInstance, HealingPlayerState healingPlayerState)
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            return healingPlayerState._finishedHealing;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.CuddlingPlayer;
        }
    }
}