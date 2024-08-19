using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.Creatures.Aloe.Types.Networking;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

public class RoamingState : BehaviourState
{
    private bool _reachedFavouriteSpotForRoaming;
    
    public RoamingState(AloeServer aloeServerInstance, AloeServer.States stateType) : base(aloeServerInstance, stateType)
    {
        Transitions =
        [
            new TransitionToAvoidingPlayer(aloeServerInstance),
            new TransitionToPassivelyStalkingPlayer(aloeServerInstance)
        ];
    }
    
    public override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);
        
        AloeServerInstance.agentMaxSpeed = 2f;
        AloeServerInstance.agentMaxAcceleration = 2f;
        AloeServerInstance.openDoorSpeedMultiplier = 2f;
        AloeServerInstance.moveTowardsDestination = true;
        _reachedFavouriteSpotForRoaming = false;
        
        AloeSharedData.Instance.Unbind(AloeServerInstance, BindType.Stalk);
        
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.LookTargetPosition, AloeServerInstance.GetLookAheadVector());
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.ShouldHaveDarkSkin, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamCrawling, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.AnimationParamHealing, false);
        AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.TargetPlayerClientId, AloeServer.NullPlayerId);

        AloeServerInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(AloeServerInstance.aloeId, 0, 0.5f);
        
        AloeServerInstance.LogDebug("Heading towards favourite position before roaming.");
        AloeServerInstance.SetDestinationToPosition(AloeServerInstance.favouriteSpot);
        if (AloeServerInstance.roamMap.inProgress) AloeServerInstance.StopSearch(AloeServerInstance.roamMap);
    }

    public override void AIIntervalBehaviour()
    {
        // Check if the aloe has reached her favourite spot, so she can start roaming from that position
        if (!_reachedFavouriteSpotForRoaming && Vector3.Distance(AloeServerInstance.favouriteSpot, AloeServerInstance.transform.position) <= 4)
        {
            _reachedFavouriteSpotForRoaming = true;
        }
        else
        {
            if (!AloeServerInstance.roamMap.inProgress)
            {
                AloeServerInstance.StartSearch(AloeServerInstance.transform.position, AloeServerInstance.roamMap);
                AloeServerInstance.LogDebug("Starting to roam map.");
            }
        }
    }

    public override void OnStateExit()
    {
        base.OnStateExit();
        if (AloeServerInstance.roamMap.inProgress) 
            AloeServerInstance.StopSearch(AloeServerInstance.roamMap);
    }

    private class TransitionToAvoidingPlayer(AloeServer aloeServerInstance) 
        : StateTransition(aloeServerInstance)
    {
        private PlayerControllerB _playerLookingAtAloe;
        
        public override bool ShouldTransitionBeTaken()
        {
            // Check if a player sees the aloe
            _playerLookingAtAloe = AloeUtils.GetClosestPlayerLookingAtPosition
                (AloeServerInstance.eye.transform, logSource: AloeServerInstance.Mls);
            return _playerLookingAtAloe != null;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.AvoidingPlayer;
        }

        public override void OnTransition()
        {
            AloeServerInstance.AvoidingPlayer.Value = _playerLookingAtAloe;
        }
    }

    private class TransitionToPassivelyStalkingPlayer(AloeServer aloeServerInstance)
        : StateTransition(aloeServerInstance)
    {
        private PlayerControllerB _stalkablePlayer;

        public override bool ShouldTransitionBeTaken()
        {
            // Check if a player has below "playerHealthThresholdForStalking" % of health
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (!AloeUtils.IsPlayerTargetable(player)) continue;
                if (player.health > AloeServerInstance.PlayerHealthThresholdForStalking) continue;
                if (AloeSharedData.Instance.IsPlayerStalkBound(player)) continue;
                
                _stalkablePlayer = player;
                return true;
            }

            return false;
        }

        public override AloeServer.States NextState()
        {
            return AloeServer.States.PassiveStalking;
        }

        public override void OnTransition()
        {
            AloeSharedData.Instance.Bind(AloeServerInstance, _stalkablePlayer, BindType.Stalk);
            AloeUtils.ChangeNetworkVar(AloeServerInstance.netcodeController.TargetPlayerClientId, _stalkablePlayer.actualClientId);
        }
    }
}