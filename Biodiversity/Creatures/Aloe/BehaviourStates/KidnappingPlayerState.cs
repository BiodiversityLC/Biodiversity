using Biodiversity.Util;
using System.Collections.Generic;
using Biodiversity.Util.Types;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
internal class KidnappingPlayerState : BehaviourState<AloeServerAI.AloeStates, AloeServerAI>
{
    private float _dragPlayerTimer;

    protected KidnappingPlayerState(AloeServerAI enemyAiInstance, AloeServerAI.AloeStates stateType) : base(
        enemyAiInstance, stateType)
    {
        Transitions =
        [
            new TransitionToHealingPlayer(EnemyAIInstance),
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = AloeHandler.Instance.Config.KidnappingPlayerMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = 8f;
        EnemyAIInstance.openDoorSpeedMultiplier = 20f;
        EnemyAIInstance.moveTowardsDestination = true;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.HasTransitionedToRunningForwardsAndCarryingPlayer = false;
        _dragPlayerTimer = AloeClient.SnatchAndGrabAudioLength;

        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamHealing, false);
        ExtensionMethods.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId,
            EnemyAIInstance.ActualTargetPlayer.Value.actualClientId);

        // Spawn fake player body ragdoll
        GameObject fakePlayerBodyRagdollGameObject =
            Object.Instantiate(
                AloeHandler.Instance.Assets.FakePlayerBodyRagdollPrefab,
                EnemyAIInstance.ActualTargetPlayer.Value.thisPlayerBody.position + Vector3.up * 1.25f,
                EnemyAIInstance.ActualTargetPlayer.Value.thisPlayerBody.rotation,
                null);

        NetworkObject fakePlayerBodyRagdollNetworkObject =
            fakePlayerBodyRagdollGameObject.GetComponent<NetworkObject>();
        fakePlayerBodyRagdollNetworkObject.Spawn();

        EnemyAIInstance.SetTargetPlayerInCaptivity(true);
        EnemyAIInstance.netcodeController.SpawnFakePlayerBodyRagdollClientRpc(EnemyAIInstance.BioId, fakePlayerBodyRagdollNetworkObject);
        EnemyAIInstance.netcodeController.SetTargetPlayerAbleToEscapeClientRpc(EnemyAIInstance.BioId, false);
        EnemyAIInstance.netcodeController.IncreasePlayerFearLevelClientRpc(EnemyAIInstance.BioId, 3f, EnemyAIInstance.ActualTargetPlayer.Value.actualClientId);
        EnemyAIInstance.netcodeController.PlayAudioClipTypeServerRpc(EnemyAIInstance.BioId, AloeClient.AudioClipTypes.SnatchAndDrag);
        EnemyAIInstance.netcodeController.ChangeLookAimConstraintWeightClientRpc(EnemyAIInstance.BioId, 0f,
            0.25f);

        if (BiodiverseAI.IsPathValid(
                agent: EnemyAIInstance.agent,
                position: EnemyAIInstance.FavouriteSpot) != BiodiverseAI.PathStatus.Valid)
        {
            EnemyAIInstance.LogVerbose("When initializing kidnapping, no path was found to the Aloe's favourite spot.");
            EnemyAIInstance.SwitchBehaviourState(AloeServerAI.AloeStates.HealingPlayer);
            return;
        }

        EnemyAIInstance.SetDestinationToPosition(EnemyAIInstance.FavouriteSpot);
    }

    internal override void UpdateBehaviour()
    {
        _dragPlayerTimer -= Time.deltaTime;
        if (_dragPlayerTimer <= 0 && !EnemyAIInstance.HasTransitionedToRunningForwardsAndCarryingPlayer)
        {
            _dragPlayerTimer = float.MaxValue; // Better than adding ANOTHER bool value to this if statement
            EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(
                EnemyAIInstance.BioId, AloeClient.KidnapRun);

            EnemyAIInstance.StartCoroutine(EnemyAIInstance.TransitionToRunningForwardsAndCarryingPlayer(0.3f));
        }
    }

    internal override void AIIntervalBehaviour()
    {
        List<PlayerControllerB> playersLookingAtAloe =
            EnemyAIInstance.GetAllPlayersLookingAtPosition(EnemyAIInstance.eye.transform.position, playerViewWidth: 40f,
                playerViewRange: 40);
        foreach (PlayerControllerB player in playersLookingAtAloe)
        {
            EnemyAIInstance.netcodeController.IncreasePlayerFearLevelClientRpc(
                EnemyAIInstance.BioId, 0.4f, player.actualClientId);
        }
    }

    private class TransitionToHealingPlayer(AloeServerAI enemyAIInstance)
        : StateTransition<AloeServerAI.AloeStates, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.FavouriteSpot) <= 2;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.HealingPlayer;
        }
    }
}