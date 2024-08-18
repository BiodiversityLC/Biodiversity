using System.Collections.Generic;
using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class KidnappingPlayerState : BehaviourState
{
    private float _dragPlayerTimer;
    
    public KidnappingPlayerState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            new TransitionToHealingPlayer(aloeServerInstance),
        ];
    }
    
    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agentMaxSpeed = 6f;
        AloeServerInstance.agentMaxAcceleration = 8f;
        AloeServerInstance.openDoorSpeedMultiplier = 20f;
        AloeServerInstance.moveTowardsDestination = true;
        AloeServerInstance.movingTowardsTargetPlayer = false;
        AloeServerInstance.hasTransitionedToRunningForwardsAndCarryingPlayer = false;
        _dragPlayerTimer = AloeClient.SnatchAndGrabAudioLength;

        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, true);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamHealing, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.TargetPlayerClientId, AloeServerInstance.ActualTargetPlayer.Value.actualClientId);
        
        // Spawn fake player body ragdoll
        GameObject fakePlayerBodyRagdollGameObject = 
            Object.Instantiate(
                AloeHandler.Instance.Assets.FakePlayerBodyRagdollPrefab, 
                AloeServerInstance.ActualTargetPlayer.Value.thisPlayerBody.position + Vector3.up * 1.25f, 
                AloeServerInstance.ActualTargetPlayer.Value.thisPlayerBody.rotation, 
                null);
        
        NetworkObject fakePlayerBodyRagdollNetworkObject =
            fakePlayerBodyRagdollGameObject.GetComponent<NetworkObject>();
        fakePlayerBodyRagdollNetworkObject.Spawn();
        
        AloeServerInstance.SetTargetPlayerInCaptivity(true);
        AloeServerInstance.netcodeController.SpawnFakePlayerBodyRagdollClientRpc(AloeServerInstance.aloeId, fakePlayerBodyRagdollNetworkObject);
        AloeServerInstance.netcodeController.SetTargetPlayerAbleToEscapeClientRpc(AloeServerInstance.aloeId, false);
        AloeServerInstance.netcodeController.IncreasePlayerFearLevelClientRpc(AloeServerInstance.aloeId, 3f, AloeServerInstance.ActualTargetPlayer.Value.actualClientId);
        AloeServerInstance.netcodeController.PlayAudioClipTypeServerRpc(AloeServerInstance.aloeId, AloeClient.AudioClipTypes.SnatchAndDrag);
        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(AloeServerInstance.aloeId, 0f, 0.25f);
        
        if (AloeUtils.IsPathValid(
                agent: AloeServerInstance.agent, 
                position: AloeServerInstance.favouriteSpot, 
                logSource: AloeServerInstance.Mls) != AloeUtils.PathStatus.Valid) 
        { 
            AloeServerInstance.LogDebug("When initializing kidnapping, no path was found to the Aloe's favourite spot."); 
            AloeServerInstance.SwitchBehaviourState(AloeServer.States.HealingPlayer); 
            return;
        }
                
        AloeServerInstance.SetDestinationToPosition(AloeServerInstance.favouriteSpot);
    }

    public override void UpdateBehaviour()
    {
        _dragPlayerTimer -= Time.deltaTime;
        if (_dragPlayerTimer <= 0 && !AloeServerInstance.hasTransitionedToRunningForwardsAndCarryingPlayer)
        {
            _dragPlayerTimer = float.MaxValue; // Better than adding ANOTHER bool value to this if statement
            AloeServerInstance.netcodeController.SetAnimationTriggerClientRpc(
                AloeServerInstance.aloeId, AloeClient.KidnapRun);
            
            AloeServerInstance.StartCoroutine(AloeServerInstance.TransitionToRunningForwardsAndCarryingPlayer(0.3f));
        }
    }

    public override void AIIntervalBehaviour()
    {
        List<PlayerControllerB> playersLookingAtAloe = AloeUtils.GetAllPlayersLookingAtPosition(AloeServerInstance.eye.transform, playerViewWidth: 40f, playerViewRange: 40);
        foreach (PlayerControllerB player in playersLookingAtAloe)
        {
            AloeServerInstance.netcodeController.IncreasePlayerFearLevelClientRpc(
                AloeServerInstance.aloeId, 0.4f, player.actualClientId);
        }
    }

    private class TransitionToHealingPlayer(AloeServer aloeServerInstance) 
        : StateTransition(aloeServerInstance)
    {
        public override bool ShouldTransitionBeTaken()
        {
            return Vector3.Distance(AloeServerInstance.transform.position, AloeServerInstance.favouriteSpot) <= 2;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.HealingPlayer;
        }
    }
}