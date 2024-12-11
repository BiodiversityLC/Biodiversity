using Biodiversity.Creatures.Aloe.Types.Networking;
using Biodiversity.Util;
using Biodiversity.Util.Attributes;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.AloeStates.Roaming)]
internal class RoamingState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    private bool _reachedFavouriteSpotForRoaming;

    public RoamingState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
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

        EnemyAIInstance.AgentMaxSpeed = 2f;
        EnemyAIInstance.AgentMaxAcceleration = 2f;
        EnemyAIInstance.openDoorSpeedMultiplier = 2f;
        EnemyAIInstance.moveTowardsDestination = true;
        _reachedFavouriteSpotForRoaming = false;

        AloeSharedData.Instance.Unbind(EnemyAIInstance, BindType.Stalk);

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.LookTargetPosition, EnemyAIInstance.GetLookAheadVector());
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamHealing, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId, BiodiverseAI.NullPlayerId);

        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0, 0.5f);

        EnemyAIInstance.LogVerbose("Heading towards favourite position before roaming.");
        EnemyAIInstance.SetDestinationToPosition(EnemyAIInstance.FavouriteSpot);
        if (EnemyAIInstance.roamMap.inProgress) EnemyAIInstance.StopSearch(EnemyAIInstance.roamMap);
    }

    internal override void AIIntervalBehaviour()
    {
        // Check if the aloe has reached her favourite spot, so she can start roaming from that position
        if (!_reachedFavouriteSpotForRoaming &&
            Vector3.Distance(EnemyAIInstance.FavouriteSpot, EnemyAIInstance.transform.position) <= 4)
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
            _playerLookingAtAloe = EnemyAIInstance.GetClosestPlayerLookingAtPosition(EnemyAIInstance.eye.transform.position);
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
                if (!EnemyAIInstance.PlayerTargetableConditions.IsPlayerTargetable(player)) continue;
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
        {
            AloeSharedData.Instance.Bind(EnemyAIInstance, _stalkablePlayer, BindType.Stalk);
            ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId,
                _stalkablePlayer.actualClientId);
        }
    }
}