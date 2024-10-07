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

        EnemyAIInstance.agentMaxSpeed = AloeHandler.Instance.Config.KidnappingPlayerMaxSpeed;
        EnemyAIInstance.agentMaxAcceleration = 8f;
        EnemyAIInstance.openDoorSpeedMultiplier = 20f;
        EnemyAIInstance.moveTowardsDestination = true;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.hasTransitionedToRunningForwardsAndCarryingPlayer = false;
        _dragPlayerTimer = AloeClient.SnatchAndGrabAudioLength;

        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.ShouldHaveDarkSkin, true);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamCrawling, false);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.AnimationParamHealing, false);
        AloeUtils.ChangeNetworkVar(EnemyAIInstance.netcodeController.TargetPlayerClientId,
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
                position: EnemyAIInstance.favouriteSpot) != BiodiverseAI.PathStatus.Valid)
        {
            EnemyAIInstance.LogVerbose("When initializing kidnapping, no path was found to the Aloe's favourite spot.");
            EnemyAIInstance.SwitchBehaviourState(AloeServerAI.AloeStates.HealingPlayer);
            return;
        }

        EnemyAIInstance.SetDestinationToPosition(EnemyAIInstance.favouriteSpot);
    }

    internal override void UpdateBehaviour()
    {
        _dragPlayerTimer -= Time.deltaTime;
        if (_dragPlayerTimer <= 0 && !EnemyAIInstance.hasTransitionedToRunningForwardsAndCarryingPlayer)
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
            AloeUtils.GetAllPlayersLookingAtPosition(EnemyAIInstance.eye.transform, playerViewWidth: 40f,
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
            return Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.favouriteSpot) <= 2;
        }

        internal override AloeServerAI.AloeStates NextState()
        {
            return AloeServerAI.AloeStates.HealingPlayer;
        }
    }
}