using Biodiversity.Util;
using Biodiversity.Util.DataStructures;
using Biodiversity.Util.Scripts;
using Biodiversity.Util.SharedVariables;
using GameNetcodeStuff;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Biodiversity.Creatures.Ogopogo;

internal class OgopogoAI : BiodiverseAI
{
    internal enum State
    {
        Idle,
        Warning,
        Rising,
        Goingdown,
        Reset
    }

    private static readonly int AnimID = Animator.StringToHash("AnimID");
    private static readonly int Stun = Animator.StringToHash("Stun");
    private const int roomLayerMask = 1 << 8; /**Bitmasks are weird. This references layer 8 which is "Room"**/

    // Movement
    private Vector3 idlePos;
    private float loseRange;
    private float attackDistance;
    private const float RiseSpeed = 75f;
    private const float RiseHeight = 100f;
    [SerializeField] private Transform RaycastPos;
    private float warningTimer;
    private bool dropRaycast;

    // Grab conrol
    public bool playerHasBeenGrabbed;
    [SerializeField] private Transform GrabPos;

    // Player references
    private CachedUnityObject<PlayerControllerB> playerGrabbed;
    private LazyUnityObjectDictionary<ulong, MeshRenderer> playerVisorRenderers;
    private float[] _playerDistances;

    // Audio
    [SerializeField] private AudioClip warning;
    [SerializeField] private AudioClip emerge;
    [SerializeField] private AudioClip returnToWater;
    [SerializeField] private KeepY normalAudio;

    // Inside Audio
    [SerializeField] private AudioSource insideAudio;
    [SerializeField] private AudioClip insideWarning;
    [SerializeField] private AudioClip insideEmerge;
    [SerializeField] private AudioClip insideMineshaftSounds;
    [SerializeField] private AudioClip TerrainHitSound;

    private float mineshaftAudioTimer = 30;

    // Timer for reset state
    private float resetTimer;

    // Default position of this.eye (needed for water stun to work)
    [SerializeField] private Transform defaultEye;

    // Mapping
    public Transform MapDot;

    private Vector3 SavedGrabPos = Vector3.zero;

    public override void Start()
    {
        base.Start();

        loseRange = OgopogoHandler.Instance.Config.LoseRange;
        attackDistance = OgopogoHandler.Instance.Config.AttackDistance;

        LogVerbose($"Ogo says the level name is:{Levels.Compatibility.GetLLLNameOfLevel(RoundManager.Instance.currentLevel.name)}:{RoundManager.Instance.currentLevel.name}");

        //Gorgonzola
        if (Levels.Compatibility.GetLLLNameOfLevel(RoundManager.Instance.currentLevel.name) == "gorgonzolalevel")
        {
            skinnedMeshRenderers[0].sharedMaterial = OgopogoHandler.Instance.Assets.CheeseOgoMaterial;
        }
        else
        {
            skinnedMeshRenderers[0].sharedMaterial = OgopogoHandler.Instance.Assets.OgoMaterial;
        }

        /*
        foreach (SelectableLevel level in StartOfRound.Instance.levels)
        {
            LogInfo(level.PlanetName);
            foreach (SpawnableEnemyWithRarity enemy in level.DaytimeEnemies)
            {
                LogInfo(enemy.enemyType.enemyName);
            }
        }
        */

        _playerDistances = new float[StartOfRound.Instance.allPlayerScripts.Length];

        // Move ogo to right position
        try
        {
            if (OgopogoHandler.LevelsParsedStaticSpawns.TryGetValue(StartOfRound.Instance.currentLevel.name, out List<Vector3> posVectors))
            {
                //LogInfo("The thing is working");
                int numberOfPos = posVectors.Count;

                int random = Random.Range(0, numberOfPos);

                transform.position = posVectors[random];
                idlePos = transform.position;
            } else
            {
                BiodiversityPlugin.LogVerbose("No config found for spawning Ogopogo.");
                RoundManager.Instance.DespawnEnemyOnServer(new NetworkObjectReference(gameObject.GetComponent<NetworkObject>()));
            }
        }
        catch (Exception ex)
        {
            LogError(ex);
        }

        // Set default y pos of audio
        normalAudio.Init();

        playerVisorRenderers = new LazyUnityObjectDictionary<ulong, MeshRenderer>(playerId =>
        {
            PlayerControllerB player = PlayerUtil.GetPlayerFromClientId(playerId);
            if (player != null && player.localVisor != null)
            {
                return player.localVisor.gameObject.GetComponentInChildren<MeshRenderer>();
            }
            return null;
        });
    }

    public override void Update()
    {
        base.Update();

        if (StartOfRound.Instance.mapScreen.targetedPlayer != null)
            MapDot.position = StartOfRound.Instance.mapScreen.targetedPlayer.isInsideFactory ? transform.position : new Vector3(transform.position.x, StartOfRound.Instance.mapScreen.targetedPlayer.transform.position.y, transform.position.z);

        skinnedMeshRenderers[0].enabled = !GameNetworkManager.Instance.localPlayerController.isInsideFactory;

        mineshaftAudioTimer -= Time.deltaTime;

        if (GameNetworkManager.Instance.localPlayerController.isInsideFactory)
        {
            creatureVoice.mute = true;
            insideAudio.mute = false;
        }
        else
        {
            creatureVoice.mute = false;
            insideAudio.mute = true;
        }

        switch (currentBehaviourStateIndex)
        {
            // Step timers

            case (int)State.Reset:
                resetTimer += Time.deltaTime;
                break;

            case (int)State.Warning:
                warningTimer -= Time.deltaTime;
                break;
        }

        // Set eye position to handle stun. (Calculated on both client and server)
        if (currentBehaviourStateIndex is (int)State.Idle or (int)State.Warning)
        {
            eye.position = defaultEye.transform.position;
            eye.rotation = defaultEye.transform.rotation;
        }
        else if (currentBehaviourStateIndex == (int)State.Rising)
        {
            eye.position = GetClosestPlayer().transform.position + GetClosestPlayer().transform.forward * 1;
            TurnObjectTowardsLocation(GetClosestPlayer().transform.position, eye);
        }

        SavedGrabPos = GrabPos.position;
    }

    private Vector3 PlayerVelocity = Vector3.zero;

    public void LateUpdate()
    {
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            _playerDistances[player.playerClientId] = Distance2d(StartOfRound.Instance.shipBounds.gameObject, player.gameObject);
        }

        // Move the grabbed player
        if (playerGrabbed.HasValue)
        {
            playerGrabbed.Value.transform.position = Vector3.SmoothDamp(playerGrabbed.Value.transform.position, SavedGrabPos, ref PlayerVelocity, 0.1f);
            playerGrabbed.Value.transform.rotation = Quaternion.Slerp(playerGrabbed.Value.transform.rotation, GrabPos.rotation, Time.deltaTime / 0.1f);

            ulong playerGrabbedId = PlayerUtil.GetClientIdFromPlayer(playerGrabbed.Value);
            if (playerGrabbedId == GameNetworkManager.Instance.localPlayerController.playerClientId)
            {
                try
                {
                    playerGrabbed.Value.thisPlayerModelArms.enabled = false;
                    playerVisorRenderers[playerGrabbedId].enabled = false;
                }
                catch
                {
                    // Somehow players joined after the match
                }
            }

            /*
            if (!GameNetworkManager.Instance.localPlayerController.isPlayerDead)
            {
                GameNetworkManager.Instance.localPlayerController.thisPlayerModelArms.enabled = true;
                playerVisorRenderers[playerGrabbedId].enabled = true;
            }
            */
        }
        else
        {
            try
            {
                GameNetworkManager.Instance.localPlayerController.thisPlayerModelArms.enabled = true;
                playerVisorRenderers[PlayerUtil.GetClientIdFromPlayer(GameNetworkManager.Instance.localPlayerController)].enabled = true;
            }
            catch
            {
                // players joined after the match started
            }
        }
    }

    // Use Physics.Raycast they said. It would be fun they said.
    public void FixedUpdate()
    {
        dropRaycast = Physics.Raycast(GrabPos.position, Vector3.down, 20f, roomLayerMask);
    }

    // Get the closest player in 2d space
    private PlayerControllerB GetClosestPlayer()
    {
        PlayerControllerB ret = null;
        float smallestDistance = 0f;
        foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
        {
            if ((player.transform.position.y >= transform.position.y || currentBehaviourStateIndex == (int)State.Rising) && !player.isInsideFactory)
            {
                if (smallestDistance == 0f)
                {
                    smallestDistance = Distance2d(player.gameObject, gameObject);
                    ret = player;
                }
                else
                {
                    float distance = Distance2d(player.gameObject, gameObject);

                    if (distance < smallestDistance)
                    {
                        smallestDistance = distance;
                        ret = player;
                    }
                }
            }
        }
        return ret;
    }

    // Check if players are within a certain range
    private bool PlayerCheck(float range)
    {
        float rangeSq = range * range;

        PlayerControllerB[] players = StartOfRound.Instance.allPlayerScripts;
        for (int i = 0; i < players.Length; i++)
        {
            PlayerControllerB player = players[i];
            // LogInfo(PlayerDistances[0]);
            if (Distance2dSq(player.gameObject, gameObject) < rangeSq &&
                (player.transform.position.y >= transform.position.y ||
                 currentBehaviourStateIndex == (int)State.Rising) && !player.isInsideFactory &&
                _playerDistances[player.playerClientId] > 15)
                return true;
        }

        return false;
    }

    // Turn towards position
    private void TurnTowardsLocation(Vector3 location)
    {
        transform.LookAt(new Vector3(location.x, transform.position.y, location.z));
    }

    // Turn object toward position
    private static void TurnObjectTowardsLocation(Vector3 location, Transform objectRef)
    {
        objectRef.LookAt(new Vector3(location.x, objectRef.transform.position.y, location.z));
    }

    // Rise at given speed. Enemy AI already updates position across client so no need for RPC
    private void Rise(float speed)
    {
        transform.Translate(Vector3.up * (speed * Time.deltaTime));
    }

    // Go down at given speed. Same as above
    private void GoDown(float speed)
    {
        transform.Translate(Vector3.down * (speed * Time.deltaTime));
    }

    // Set the grabbed player or set as null
    [ClientRpc]
    public void SetPlayerGrabbedClientRpc(int playerID, bool setNull = false, bool resetSpecialPlayer = true)
    {
        if (!setNull)
        {
            playerGrabbed.Set(PlayerUtil.GetPlayerFromClientId(playerID));
            playerHasBeenGrabbed = true;
            PlayerFallDamage.FallDamageRemoved[playerID] = true;
        }
        else
        {
            playerGrabbed.Reset();
            playerHasBeenGrabbed = false;
        }
        try
        {
            if (resetSpecialPlayer)
            {
                inSpecialAnimationWithPlayer.inAnimationWithEnemy = null;
                inSpecialAnimationWithPlayer.inSpecialInteractAnimation = false;
            }
        }
        catch (Exception)
        {
            // ignored
        }
        if (setNull) inSpecialAnimationWithPlayer = null;
    }

    // Play sounds
    [ClientRpc]
    public void PlayVoiceClientRpc(int id)
    {
        AudioClip audio = id switch
        {
            0 => warning,
            1 => emerge,
            2 => returnToWater,
            3 => insideMineshaftSounds,
            4 => TerrainHitSound,
            _ => warning
        };

        if (id == 0)
        {
            insideAudio.PlayOneShot(insideWarning);
        }
        else if (id == 1)
        {
            insideAudio.PlayOneShot(insideEmerge);
        }

        if (id != 3)
        {
            creatureVoice.PlayOneShot(audio);
        }
        else
        {
            insideAudio.PlayOneShot(audio);
        }
    }

    private bool CheckForWall()
    {
        return Physics.Raycast(RaycastPos.position, RaycastPos.forward, 7.5f, 1 << 8 /**Bitmasks are weird. This references layer 8 which is "Room"**/);
    }

    // Reset enemy variables
    private void ResetEnemy()
    {
        playerGrabbed.Reset();
        playerHasBeenGrabbed = false;
        resetTimer = 0f;
        SwitchToBehaviourClientRpc((int)State.Idle);
        SetPlayerGrabbedClientRpc(0, true);
        creatureAnimator.SetInteger(AnimID, 1);
    }

    private void SpawnVermin()
    {
        return; // Make sure this does not run until problems are solved
        foreach (int i in Enumerable.Range(0, 4))
        {
            GameObject vermin = Instantiate(OgopogoHandler.Instance.Assets.VerminEnemyType.enemyPrefab, transform.position, Quaternion.Euler(new Vector3(0, 0, 0)));
            vermin.GetComponentInChildren<NetworkObject>().Spawn(true);
            VerminAI aIscript = vermin.gameObject.GetComponent<VerminAI>();
            //aIscript.SetWater = water; TODO: find a way to set the vermin's water so tey work right
            aIscript.SetPos = transform.position;
            aIscript.SpawnedByOgo = true;
        }
    }

    [ClientRpc]
    public void TakeOutOfVehicleClientRpc(ulong playerID)
    {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[playerID];

        if (player.inVehicleAnimation)
        {
            player.CancelSpecialTriggerAnimations();
        }
    }

    // Handle grabbing
    public override void OnCollideWithPlayer(Collider other)
    {
        if (isEnemyDead || !IsServer) return;
        if (inSpecialAnimationWithPlayer != null) return;

        PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
        if (player.isInsideFactory || _playerDistances[player.playerClientId] < 15) return;


        if (currentBehaviourStateIndex != (int)State.Reset && currentBehaviourStateIndex != (int)State.Goingdown)
        {
            player.inAnimationWithEnemy = this;
            player.inSpecialInteractAnimation = true;

            inSpecialAnimationWithPlayer = player;

            TakeOutOfVehicleClientRpc(player.playerClientId);
            SetPlayerGrabbedClientRpc((int)player.playerClientId, false, false);
            creatureAnimator.SetInteger(AnimID, 3);
        }
    }

    public void TerrainDetect()
    {
        if (currentBehaviourStateIndex != (int)State.Rising) return;
        if (transform.position.y - idlePos.y < 90) return;
        SwitchToBehaviourClientRpc((int)State.Goingdown);
        PlayVoiceClientRpc(2);
        PlayVoiceClientRpc(4);
    }

    public override void HitEnemy(int force = 1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1)
    {
        base.HitEnemy(force, playerWhoHit, playHitSFX, hitID);
        // LogInfo("It's running");
        enemyHP -= force;
        if (enemyHP <= 0)
        {
            transform.position = new Vector3(transform.position.x, idlePos.y + 80, transform.position.z);
            creatureAnimator.SetBool(Stun, false);
            KillEnemyOnOwnerClient();
        }
    }

    public override void CancelSpecialAnimationWithPlayer()
    {
        base.CancelSpecialAnimationWithPlayer();
        SetPlayerGrabbedClientRpc(0, true);
        inSpecialAnimationWithPlayer = null;
    }

    public override void DoAIInterval()
    {
        // Stupid LC base code disabled here because it was cauing error spam.
        //base.DoAIInterval();

        // Do this manually because the base code is disabled
        SyncPositionToClients();

        if (isEnemyDead)
        {
            return;
        }

        // handle stun
        if (stunNormalizedTimer > 0)
        {
            SetPlayerGrabbedClientRpc(0, true);
            creatureAnimator.SetBool(Stun, true);
            return;
        }

        // mineshaft sounds
        if (RoundManager.Instance.currentDungeonType == 4 && mineshaftAudioTimer < 0)
        {
            PlayVoiceClientRpc(3);
            mineshaftAudioTimer = Random.Range(OgopogoHandler.Instance.Config.OgopogoAmbienceMin, OgopogoHandler.Instance.Config.OgopogoAmbienceMax);
        }

        creatureAnimator.SetBool(Stun, false);

        switch (currentBehaviourStateIndex)
        {
            case (int)State.Idle:
                if (PlayerCheck(attackDistance))
                {
                    SwitchToBehaviourClientRpc((int)State.Warning);
                    warningTimer = 3f;
                    PlayVoiceClientRpc(0);
                }
                break;

            case (int)State.Warning:
                PlayerControllerB player = GetClosestPlayer();

                if (player == null)
                {
                    SwitchToBehaviourClientRpc((int)State.Idle);
                    return;
                }

                TurnTowardsLocation(player.gameObject.transform.position);


                if (Distance2d(gameObject, player.gameObject) <= attackDistance && warningTimer < 0)
                {
                    SwitchToBehaviourClientRpc((int)State.Rising);
                    PlayVoiceClientRpc(1);
                    creatureAnimator.SetInteger(AnimID, 2);
                    SpawnVermin();
                }

                if (!PlayerCheck(attackDistance))
                {
                    SwitchToBehaviourClientRpc((int)State.Idle);
                }

                break;

            case (int)State.Rising:
                if (transform.position.y - idlePos.y < RiseHeight)
                {
                    Rise(RiseSpeed);
                    TurnTowardsLocation(GetClosestPlayer().gameObject.transform.position);
                }

                if (playerHasBeenGrabbed)
                {
                    SwitchToBehaviourClientRpc((int)State.Goingdown);
                    PlayVoiceClientRpc(2);
                }

                //Move forward

                if (!PlayerCheck(loseRange))
                {
                    SwitchToBehaviourClientRpc((int)State.Goingdown);
                    PlayVoiceClientRpc(2);
                }

                if (!(transform.position.y - idlePos.y < RiseHeight))
                {
                    transform.position = new Vector3(transform.position.x, idlePos.y + RiseHeight, transform.position.z);
                    SwitchToBehaviourClientRpc((int)State.Goingdown);
                    PlayVoiceClientRpc(2);
                }
                break;

            case (int)State.Goingdown:

                if (transform.position.y - idlePos.y > 0f)
                {
                    GoDown(RiseSpeed);
                }

                if (playerGrabbed.HasValue && dropRaycast)
                {
                    SetPlayerGrabbedClientRpc(0, true);
                    inSpecialAnimationWithPlayer = null;
                }

                if (!(transform.position.y - idlePos.y > 0f))
                {
                    transform.position = new Vector3(transform.position.x, idlePos.y, transform.position.z);
                    SwitchToBehaviourClientRpc((int)State.Reset);
                    SetPlayerGrabbedClientRpc(0, true);
                }
                break;

            case (int)State.Reset:
                if (resetTimer >= 12)
                {
                    ResetEnemy();
                }
                break;
        }
    }
}