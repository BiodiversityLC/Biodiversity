using Biodiversity.Core.Attributes;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Util;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Scripting;

namespace Biodiversity.Creatures.Aloe.BehaviourStates;

[Preserve]
[State(AloeServerAI.States.KidnappingPlayer)]
internal class KidnappingPlayerState : BehaviourState<AloeServerAI.States, AloeServerAI>
{
    private float _dragPlayerTimer;

    public KidnappingPlayerState(AloeServerAI enemyAiInstance) : base(enemyAiInstance)
    {
        Transitions =
        [
            new TransitionToHealingPlayer(EnemyAIInstance),
        ];
    }

    internal override void OnStateEnter(ref StateData initData)
    {
        base.OnStateEnter(ref initData);

        EnemyAIInstance.AgentMaxSpeed = AloeHandler.Instance.Config.KidnappingPlayerDraggingMaxSpeed;
        EnemyAIInstance.AgentMaxAcceleration = AloeHandler.Instance.Config.KidnappingPlayerDraggingMaxAcceleration;
        EnemyAIInstance.openDoorSpeedMultiplier = 20f;
        EnemyAIInstance.moveTowardsDestination = true;
        EnemyAIInstance.movingTowardsTargetPlayer = false;
        EnemyAIInstance.HasTransitionedToRunningForwardsAndCarryingPlayer = false;
        _dragPlayerTimer = AloeClient.SnatchAndGrabAudioLength;

        EnemyAIInstance.netcodeController.ShouldHaveDarkSkin.SafeSet(true);
        EnemyAIInstance.netcodeController.AnimationParamCrawling.SafeSet(false);
        EnemyAIInstance.netcodeController.AnimationParamHealing.SafeSet(false);
        EnemyAIInstance.netcodeController.TargetPlayerClientId.SafeSet(EnemyAIInstance.ActualTargetPlayer.Value.actualClientId);

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
        EnemyAIInstance.netcodeController.SpawnFakePlayerBodyRagdollClientRpc(fakePlayerBodyRagdollNetworkObject);
        EnemyAIInstance.netcodeController.SetTargetPlayerAbleToEscapeClientRpc(false);
        EnemyAIInstance.netcodeController.IncreasePlayerFearLevelClientRpc(3f, EnemyAIInstance.ActualTargetPlayer.Value.actualClientId);
        EnemyAIInstance.PlayRandomAudioClipTypeServerRpc(
            nameof(AloeClient.AudioClipTypes.snatchAndDragSfx), nameof(AloeClient.AudioSourceTypes.aloeVoiceSource), false, true, false, true);

        if (BiodiverseAI.IsPathValid(
                agent: EnemyAIInstance.agent,
                targetPosition: EnemyAIInstance.FavouriteSpot) != BiodiverseAI.PathStatus.Valid)
        {
            EnemyAIInstance.LogWarning("When initializing kidnapping, no path was found to the Aloe's favourite spot.");
            EnemyAIInstance.SwitchBehaviourState(AloeServerAI.States.HealingPlayer);
            return;
        }

        EnemyAIInstance.SetDestinationToPosition(EnemyAIInstance.FavouriteSpot);
    }

    internal override void UpdateBehaviour()
    {
        base.UpdateBehaviour();
        
        _dragPlayerTimer -= Time.deltaTime;
        if (_dragPlayerTimer <= 0 && !EnemyAIInstance.HasTransitionedToRunningForwardsAndCarryingPlayer)
        {
            _dragPlayerTimer = float.MaxValue; // Better than adding ANOTHER bool value to this if statement (because we only want the code inside this if statement to trigger once)
            EnemyAIInstance.netcodeController.SetAnimationTriggerClientRpc(AloeClient.KidnapRun);

            EnemyAIInstance.StartCoroutine(EnemyAIInstance.TransitionToRunningForwardsAndCarryingPlayer(0.3f));
        }
    }

    internal override void AIIntervalBehaviour()
    {
        base.AIIntervalBehaviour();
        
        List<PlayerControllerB> playersLookingAtAloe =
            BiodiverseAI.GetAllPlayersLookingAtPositionPooled(EnemyAIInstance.eye.transform.position, playerViewWidth: 40f,
                playerViewRange: 40);

        for (int i = 0; i < playersLookingAtAloe.Count; i++)
        {
            PlayerControllerB player = playersLookingAtAloe[i];
            
            EnemyAIInstance.LogVerbose($"Increasing fear for player {player.playerUsername}");
            
            EnemyAIInstance.netcodeController.IncreasePlayerFearLevelClientRpc(0.4f, player.actualClientId);
        }

        ListPool<PlayerControllerB>.Release(playersLookingAtAloe);
    }

    private class TransitionToHealingPlayer(AloeServerAI enemyAIInstance)
        : StateTransition<AloeServerAI.States, AloeServerAI>(enemyAIInstance)
    {
        internal override bool ShouldTransitionBeTaken()
        {
            return Vector3.Distance(EnemyAIInstance.transform.position, EnemyAIInstance.FavouriteSpot) <= 2;
        }

        internal override AloeServerAI.States NextState()
        {
            return AloeServerAI.States.HealingPlayer;
        }
    }
}