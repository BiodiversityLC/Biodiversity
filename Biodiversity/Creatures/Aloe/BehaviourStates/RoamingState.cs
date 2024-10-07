using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
internal class RoamingState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    private bool _reachedFavouriteSpotForRoaming;

    protected RoamingState(AloeServerAI enemyAiInstance, AloeServerAI.AloeStates stateType) : base(
        enemyAiInstance, stateType)
    {
        Transitions =
        [
            new TransitionToAvoidingPlayer(EnemyAIInstance),
            new TransitionToPassivelyStalkingPlayer(EnemyAIInstance)
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.agentMaxSpeed = 2f;
        EnemyAIInstance.agentMaxAcceleration = 2f;
        EnemyAIInstance.openDoorSpeedMultiplier = 2f;
        EnemyAIInstance.moveTowardsDestination = true;
        _reachedFavouriteSpotForRoaming = false;

        AloeSharedData.Instance.Unbind(EnemyAIInstance, BindType.Stalk);

        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.LookTargetPosition, EnemyAIInstance.GetLookAheadVector());
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, false);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamHealing, false);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, AloeServerAI.NullPlayerId);

        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0, 0.5f);

        EnemyAIInstance.LogVerbose("Heading towards favourite position before roaming.");
        EnemyAIInstance.SetDestinationToPosition(EnemyAIInstance.favouriteSpot);
        if (EnemyAIInstance.roamMap.inProgress) EnemyAIInstance.StopSearch(EnemyAIInstance.roamMap);
    }

    internal override void AIIntervalBehaviour()
    {
        // Check if the aloe has reached her favourite spot, so she can start roaming from that position
        if (!_reachedFavouriteSpotForRoaming &&
            Vector3.Distance(EnemyAIInstance.favouriteSpot, EnemyAIInstance.transform.position) <= 4)
        {
            _reachedFavouriteSpotForRoaming = true;
        }
        else
        {
            if (!EnemyAIInstance.roamMap.inProgress)
            {
                EnemyAIInstance.StartSearch(EnemyAIInstance.transform.position, EnemyAIInstance.roamMap);
                EnemyAIInstance.LogVerbose("Starting to roam map.");
            }
        }
    }

    internal override void OnStateExit()
    {
        base.OnStateExit();
        if (EnemyAIInstance.roamMap.inProgress)
            EnemyAIInstance.StopSearch(EnemyAIInstance.roamMap);
    }

    private class TransitionToAvoidingPlayer(AloeServerAI enemyAIInstance)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        private PlayerControllerB _playerLookingAtAloe;

        internal override bool ShouldTransitionBeTaken()
        {
            // Check if a player sees the aloe
            _playerLookingAtAloe = AloeUtils.GetClosestPlayerLookingAtPosition(EnemyAIInstance.eye.transform);
            return _playerLookingAtAloe != null;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.AvoidingPlayer;
        }

        internal override void OnTransition()
        {
            EnemyAIInstance.AvoidingPlayer.Value = _playerLookingAtAloe;
        }
    }

    private class TransitionToPassivelyStalkingPlayer(AloeServerAI enemyAIInstance)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        private PlayerControllerB _stalkablePlayer;

        internal override bool ShouldTransitionBeTaken()
        {
            // Check if a player has below "playerHealthThresholdForStalking" % of health
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (!AloeUtils.IsPlayerTargetable(player)) continue;
                if (player.health > EnemyAIInstance.PlayerHealthThresholdForStalking) continue;
                if (AloeSharedData.Instance.IsPlayerStalkBound(player)) continue;

                _stalkablePlayer = player;
                return true;
            }

            return false;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.PassiveStalking;
        }

        internal override void OnTransition()
        { ;
            AloeSharedData.Instance.Bind(EnemyAIInstance, _stalkablePlayer, BindType.Stalk);
            AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId,
                _stalkablePlayer.actualClientId);
        }
    }
}