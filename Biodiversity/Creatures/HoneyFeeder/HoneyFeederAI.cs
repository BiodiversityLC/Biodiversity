using Biodiversity.Util;
using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Creatures.HoneyFeeder;

internal class HoneyFeederAI : BiodiverseAI
{
    public enum AIStates
    {
        ASLEEP, // starts asleep until 1pm
        WANDERING, // wandering looking for hives
        FOUND_HIVE, // heading to hive
        ATTACKING_BACKINGUP,
        ATTACKING_CHARGING,
        RETURNING,
        DIGESTING
    }

    public enum DigestionStates
    {
        NONE,
        PARTLY
    }

    private List<GrabbableObject> possibleHives = [];
    private List<RedLocustBees> beesCache = [];
    private GrabbableObject targetHive;

    private HoneyFeederNest nest;
    internal static HoneyFeederAI Instance { get; private set; }

    [field: SerializeField] public HoneyFeederConfig Config { get; private set; } = HoneyFeederHandler.Instance.Config;

    private AIStates _state = AIStates.WANDERING;
    private DigestionStates digestion = DigestionStates.NONE;

    private float beeDigestionTimer = 0;
    private int originalDefenseDistance;

    public AIStates State
    {
        get => _state;
        private set
        {
            LogVerbose($"Updating state: {_state} -> {value}");
            moveTowardsDestination = false;
            movingTowardsTargetPlayer = false;
            agent.enabled = true;
            agent.speed = Config.NormalSpeed;
            if (currentSearch.inProgress) StopSearch(currentSearch, true);
            _state = value;
        }
    }

    private AISearchRoutine roamingRoutine = new();

    public override void Start()
    {
        base.Start();

        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        StartCoroutine(RefreshCollectableHivesDelayed());
        // FIXME: Replace with nest prefab when we have modle
        nest = new GameObject("Honeyfeeder Nest").AddComponent<HoneyFeederNest>();
        nest.transform.position = transform.position;
    }

    private IEnumerator RefreshCollectableHivesDelayed(float delay = 1)
    {
        yield return new WaitForSeconds(delay);
        RefreshCollectableHives();
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    public override void DoAIInterval()
    {
        // biodiversity calculates everything host end, so this should always be run on the host.
        //if(!ShouldProcessEnemy()) return; // <- disabled for testing
        base.DoAIInterval();

        switch (State)
        {
            case AIStates.ASLEEP:
                if (TimeOfDay.Instance.HasPassedTime(TimeOfDay.Instance.ParseTimeString(Config.WakeUpTime)))
                {
                    LogVerbose("Honeyfeeder is waking up!");
                    State = AIStates.WANDERING;
                }

                break;
            case AIStates.WANDERING:
                if (!roamingRoutine.inProgress) StartSearch(transform.position, roamingRoutine);

                if (targetHive != null)
                {
                    // reset incase player successfully runs away with the hive.
                    if (Vector3.Distance(targetHive.transform.position, transform.position) <= Config.SightDistance)
                    {
                        State = AIStates.FOUND_HIVE;
                        break;
                    }

                    targetHive = null;
                }

                foreach (GrabbableObject hive in possibleHives)
                {
                    if (Vector3.Distance(hive.transform.position, transform.position) <= Config.SightDistance)
                    {
                        targetHive = hive;
                        originalDefenseDistance = GetBees().defenseDistance;
                        if (targetHive.playerHeldBy == null)
                        {
                            State = AIStates.FOUND_HIVE;
                        }
                        else
                        {
                            targetPlayer = targetHive.playerHeldBy;
                            State = AIStates.ATTACKING_BACKINGUP;
                        }

                        break;
                    }
                }

                break;
            case AIStates.FOUND_HIVE:
                if (targetHive.playerHeldBy != null)
                {
                    targetPlayer = targetHive.playerHeldBy;
                    State = AIStates.ATTACKING_BACKINGUP;
                    break;
                }

                LogVerbose(
                    $"Distance to targetHive: {Vector3.Distance(transform.position, targetHive.transform.position)}");
                if (Vector3.Distance(transform.position, targetHive.transform.position) < 3.5f)
                {
                    // todo: have animation and wait for animation to finish.

                    GrabItem(targetHive);
                    GrabItemClientRPC(targetHive.NetworkObject);

                    destination = nest.transform.position;
                    moveTowardsDestination = true;
                    GetBees().defenseDistance = 0;

                    State = AIStates.RETURNING;
                }
                else
                {
                    destination = targetHive.transform.position;
                    moveTowardsDestination = true;
                }

                break;
            case AIStates.ATTACKING_BACKINGUP:
                if (!moveTowardsDestination)
                {
                    StartBackingUp();
                }

                if (targetPlayer.isPlayerDead || targetHive.playerHeldBy == null)
                {
                    State = AIStates.WANDERING;
                    break;
                }

                targetPlayer = targetHive.playerHeldBy;
                if (HasFinishedAgentPath())
                {
                    float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);
                    if (distance > Config.SightDistance)
                    {
                        // too far
                        State = AIStates
                            .WANDERING; // wandering state will automatically fix if the targetHive is still in range.
                    }
                    else if (distance < Config.TooCloseAmount)
                    {
                        // too close
                        StartBackingUp();
                    }
                    else
                    {
                        // perfect distance
                        State = AIStates.ATTACKING_CHARGING;
                        destination = targetPlayer.transform.position +
                                      (transform.position.Direction(targetPlayer.transform.position) *
                                       Config.FollowthroughAmount);
                        moveTowardsDestination = true;
                    }
                }

                break;
            case AIStates.ATTACKING_CHARGING:
                agent.speed = Config.ChargeSpeed;
                moveTowardsDestination = true;

                if (HasFinishedAgentPath())
                {
                    State = AIStates.ATTACKING_BACKINGUP;
                }

                break;
            case AIStates.RETURNING:
                destination = nest.transform.position;
                moveTowardsDestination = true;

                DigestBees();
                if (HasFinishedAgentPath())
                {
                    State = AIStates.DIGESTING;
                }

                break;
            case AIStates.DIGESTING:
                if (targetHive.isHeldByEnemy)
                {
                    DropItem(targetHive);
                    DropItemClientRPC(targetHive.NetworkObject);
                }

                agent.enabled = false;

                LogVerbose(
                    $"Current time: {TimeOfDay.Instance.GetCurrentTime()}. Digested time: {TimeOfDay.Instance.ParseTimeString(Config.TimeWhenPartlyDigested)}");

                DigestBees();
                if (targetHive.playerHeldBy != null)
                {
                    targetPlayer = targetHive.playerHeldBy;
                    State = AIStates.ATTACKING_BACKINGUP;
                    if (TryGetBees(out var bees))
                        bees.defenseDistance = originalDefenseDistance;
                }

                if (TimeOfDay.Instance.HasPassedTime(
                        TimeOfDay.Instance.ParseTimeString(Config.TimeWhenPartlyDigested)) &&
                    digestion != DigestionStates.PARTLY)
                {
                    digestion = DigestionStates.PARTLY;
                    LogVerbose("Set digestion status to Partly.");

                    // play sound?
                    targetHive.scrapValue =
                        Mathf.RoundToInt(targetHive.scrapValue * Config.PartlyDigestedScrapMultiplier);
                }

                break;
            default:
                BiodiversityPlugin.Logger.LogError($"[HoneyFeeder] {State} isn't valid?!??!??!?! NOT GOOD!!");
                break;
        }
    }

    public RedLocustBees GetBees()
    {
        return beesCache.First(bees => bees.hive == targetHive);
    }

    public bool TryGetBees(out RedLocustBees bees)
    {
        bees = GetBees();
        return bees != null;
    }

    public override void Update()
    {
        base.Update();

        if (!IsOwner) return;
        if (State == AIStates.RETURNING || State == AIStates.DIGESTING)
        {
            beeDigestionTimer += Time.deltaTime;
        }
        else
        {
            beeDigestionTimer = 0;
        }
    }

    private void DigestBees()
    {
        if (!TryGetBees(out var bees)) return;

        LogVerbose($"nom timer :3 {beeDigestionTimer}");
        if (beeDigestionTimer >= Config.BeeDigestionTime)
        {
            Destroy(bees.gameObject);
        }
    }

    // this is cooked :sob::sob:
    private void GrabItem(GrabbableObject item)
    {
        item.parentObject = transform; // change to hold in hands i think??
        item.hasHitGround = false;
        item.isHeldByEnemy = true;
        item.GrabItemFromEnemy(this);
        item.EnablePhysics(false);
    }

    private void DropItem(GrabbableObject item)
    {
        item.parentObject = null;
        item.transform.SetParent(StartOfRound.Instance.propsContainer, true);
        item.EnablePhysics(true);
        item.fallTime = 0f;
        item.startFallingPosition = item.transform.parent.InverseTransformPoint(item.transform.position);
        item.targetFloorPosition = item.transform.parent.InverseTransformPoint(
            RoundManager.Instance.RandomlyOffsetPosition(targetHive.GetItemFloorPosition(default), 1.2f, 0.4f));
        item.floorYRot = -1;
        item.isHeldByEnemy = false;
        item.DiscardItemFromEnemy();
    }

    [ClientRpc]
    private void DropItemClientRPC(NetworkObjectReference itemRef)
    {
        if (IsOwner) return;
        if (itemRef.TryGet(out var networkObject))
        {
            DropItem(networkObject.GetComponent<GrabbableObject>());
        }
    }


    [ClientRpc]
    private void GrabItemClientRPC(NetworkObjectReference itemRef)
    {
        if (IsOwner) return;
        if (itemRef.TryGet(out var networkObject))
        {
            GrabItem(networkObject.GetComponent<GrabbableObject>());
        }
    }

    private void RefreshCollectableHives()
    {
        LogVerbose("Refreshing possible hives.");
        beesCache = FindObjectsOfType<RedLocustBees>().ToList();
        possibleHives = beesCache.Select(bees => bees.hive).ToList();
        LogVerbose("Possible hives count: " + possibleHives.Count);
    }

    private void StartBackingUp()
    {
        float radius = UnityEngine.Random.Range(Config.MinBackupAmount, Config.MaxBackupAmount);
        Vector3 directionFromPlayer = targetPlayer.transform.position.Direction(transform.position);
        Vector3 backupOrigin = targetPlayer.transform.position + (directionFromPlayer * radius);
        destination = GetRandomPositionOnNavMesh(backupOrigin, 2);
        moveTowardsDestination = true;
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        base.OnCollideWithPlayer(other);
        if (!IsOwner) return;

        if (State != AIStates.ATTACKING_CHARGING) return;
        if (other.TryGetComponent(out PlayerControllerB player))
        {
            HitPlayerClientRpc((int)player.playerClientId);
            updateDestinationInterval = Config.StunTimeAfterHit;

            State = AIStates.ATTACKING_BACKINGUP;
        }
    }

    [ClientRpc]
    private void HitPlayerClientRpc(int playerId)
    {
        if (playerId == (int)GameNetworkManager.Instance.localPlayerController.playerClientId)
        {
            GameNetworkManager.Instance.localPlayerController.DamagePlayer(Config.ChargeDamage,
                causeOfDeath: CauseOfDeath.Mauling);
        }
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false,
        int hitId = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX);

        targetPlayer = playerWhoHit;
        State = AIStates.ATTACKING_BACKINGUP;
    }
}