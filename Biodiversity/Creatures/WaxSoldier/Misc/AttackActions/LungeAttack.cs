using Biodiversity.Creatures.Core;
using Biodiversity.Creatures.Core.StateMachine;
using Biodiversity.Creatures.WaxSoldier.BehaviourStates;
using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;

namespace Biodiversity.Creatures.WaxSoldier.Misc.AttackActions;

public class LungeAttack : AttackAction
{
    private const float LUNGE_DURATION = 0.21f; // This is from the lunge animation
    private const float ARC_HEIGHT = 3f;
    private const float MAX_LUNGE_DISTANCE = 12f;

    private Vector3 startPosition, targetPosition;
    private float elapsed;
    private bool lungeActive;

    private readonly PlayerCooldownTracker _playerDamageCooldowns = new();

    public LungeAttack(int animationTriggerHash, float minRange, float maxRange, float cooldown, int priority) : base(animationTriggerHash, minRange, maxRange, cooldown, priority)
    {
        AddRequirement(ctx => IsMolten(ctx));
    }

    public override void Start(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        startPosition = ctx.Adapter.Transform.position;

        Vector3 lungeDir = ctx.Adapter.TargetPlayer.transform.position - startPosition;
        lungeDir.y = 0f;
        float lungeDirSqrMagnitude = lungeDir.sqrMagnitude;

        float lungeDistance = Mathf.Min(lungeDirSqrMagnitude * lungeDirSqrMagnitude, MAX_LUNGE_DISTANCE);
        lungeDir = lungeDirSqrMagnitude > 0.0001f ? lungeDir.normalized : ctx.Adapter.Transform.forward;

        Vector3 desiredTargetPosition = startPosition + lungeDir * lungeDistance;
        targetPosition = NavMesh.SamplePosition(desiredTargetPosition, out NavMeshHit hit, 4f, ctx.Adapter.Agent.areaMask)
            ? hit.position
            : desiredTargetPosition;

        elapsed = 0f;
        lungeActive = true;

        ctx.Adapter.Agent.enabled = false;
    }

    public override void Update(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.Update(ctx);

        if (!lungeActive) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / LUNGE_DURATION);

        Vector3 position = Vector3.Lerp(startPosition, targetPosition, t);
        position.y += ARC_HEIGHT * 4f * (t - t * t);
        ctx.Adapter.Transform.position = position;

        if (t >= 1f)
        {
            lungeActive = false;

            if (NavMesh.SamplePosition(ctx.Adapter.Transform.position, out NavMeshHit hit, 5f, ctx.Adapter.Agent.areaMask))
                ctx.Adapter.Transform.position = hit.position;
        }
    }

    public override void Finish(AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        ctx.Adapter.Agent.enabled = true;
        ctx.Adapter.Agent.Warp(ctx.Adapter.Transform.position);

        _playerDamageCooldowns.Clear();
    }

    public override void HandleCustomEvent(string eventName, StateData eventData, AIContext<WaxSoldierBlackboard, WaxSoldierAdapter> ctx)
    {
        base.HandleCustomEvent(eventName, eventData, ctx);

        switch (eventName)
        {
            case nameof(AttackingState.OnCollideWithPlayer):
                if (!lungeActive) return;

                Collider playerCollider = eventData.Get<Collider>("playerCollider");
                if (!playerCollider) break;
                if (!playerCollider.TryGetComponent(out PlayerControllerB player) || !player) break;

                ulong playerClientId = PlayerUtil.GetClientIdFromPlayer(player);
                if (_playerDamageCooldowns.IsOnCooldown(playerClientId)) break;

                _playerDamageCooldowns.Start(playerClientId, float.MaxValue);
                ctx.Blackboard.NetcodeController.DamagePlayerClientRpc(
                    PlayerUtil.GetClientIdFromPlayer(player), WaxSoldierHandler.Instance.Config.LungeDamage,
                    causeOfDeath: CauseOfDeath.Crushing);

                break;

            // case nameof(WaxSoldierAnimationEventHandler.OnAnimationEventEndMoltenLunge):
            //     break;
        }
    }
}