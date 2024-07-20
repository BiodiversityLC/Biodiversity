using BepInEx.Logging;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using System;
using System.Collections.Concurrent;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace Biodiversity.Creatures.Aloe;

[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
internal class AloeSharedData
{
    private static readonly object Padlock = new();
    private static AloeSharedData _instance;
    private static readonly ManualLogSource Mls = Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Shared Data");

    public static AloeSharedData Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Padlock)
            {
                _instance ??= new AloeSharedData();
            }

            return _instance;
        }
    }

    private readonly ConcurrentDictionary<AloeServer, PlayerControllerB> _aloeBoundKidnaps = new();
    private readonly ConcurrentDictionary<AloeServer, PlayerControllerB> _aloeBoundStalks = new();
    private readonly ConcurrentDictionary<PlayerControllerB, int> _playersMaxHealth = new();
    private readonly List<BrackenRoomAloeNode> _brackenRoomAloeNodes = [];
    private readonly ConcurrentBag<GameObject> _insideAINodes = [];
    private readonly ConcurrentBag<GameObject> _outsideAINodes = [];

    public Vector3 BrackenRoomDoorPosition { get; set; } = Vector3.zero;

    public IReadOnlyDictionary<AloeServer, PlayerControllerB> AloeBoundKidnaps => _aloeBoundKidnaps;
    public IReadOnlyDictionary<AloeServer, PlayerControllerB> AloeBoundStalks => _aloeBoundStalks;
    public IReadOnlyDictionary<PlayerControllerB, int> PlayersMaxHealth => _playersMaxHealth;
    
    public IReadOnlyList<BrackenRoomAloeNode> BrackenRoomAloeNodes
    {
        get
        {
            lock (Padlock)
            {
                return new List<BrackenRoomAloeNode>(_brackenRoomAloeNodes);
            }
        }
    }

    public void BindKidnap(AloeServer server, PlayerControllerB player)
    {
        _aloeBoundKidnaps.TryAdd(server, player);
    }

    public void UnbindKidnap(AloeServer server)
    {
        _aloeBoundKidnaps.TryRemove(server, out _);
    }

    public void BindStalk(AloeServer server, PlayerControllerB player)
    {
        _aloeBoundStalks.TryAdd(server, player);
    }

    public void UnbindStalk(AloeServer server)
    {
        _aloeBoundStalks.TryRemove(server, out _);
    }

    public bool IsAloeKidnapBound(AloeServer server)
    {
        if (server == null)
        {
            throw new ArgumentNullException(nameof(server), "The provided aloe server is null.");
        }

        return _aloeBoundKidnaps.ContainsKey(server);
    }

    public bool IsPlayerKidnapBound(PlayerControllerB player)
    {
        return _aloeBoundKidnaps.Values.Contains(player);
    }

    public bool IsPlayerStalkBound(PlayerControllerB player)
    {
        return _aloeBoundStalks.Values.Contains(player);
    }

    public void SetPlayerMaxHealth(PlayerControllerB player, int maxHealth)
    {
        _playersMaxHealth[player] = maxHealth;
    }

    /// <summary>
    /// Gets the max health of the given player
    /// This is needed because mods may increase the max health of a player
    /// </summary>
    /// <param name="player">The player to get the max health.</param>
    /// <returns>The player's max health</returns>
    public int GetPlayerMaxHealth(PlayerControllerB player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player), "The provided player is null, cannot get max health.");
        }
        
        _playersMaxHealth.TryGetValue(player, out int maxHealth);
        if (maxHealth > 0) return maxHealth;
        
        Mls.LogWarning($"Max health of given player {player.playerUsername} is {maxHealth}. This should not happen. Returning a max health of 100 as a failsafe.");
        return 100;
    }

    public GameObject[] GetInsideAINodes()
    {
        lock (Padlock)
        {
            if (_insideAINodes.Count == 0)
            {
                GameObject[] insideAINodes = GameObject.FindGameObjectsWithTag("AINode");
                foreach (GameObject node in insideAINodes)
                {
                    _insideAINodes.Add(node);
                }
            }
        
            return _insideAINodes.ToArray();
        }
    }

    public GameObject[] GetOutsideAINodes()
    {
        lock (Padlock)
        {
            if (_outsideAINodes.Count == 0)
            {
                GameObject[] outsideAINodes = GameObject.FindGameObjectsWithTag("OutsideAINode");
                foreach (GameObject node in outsideAINodes)
                {
                    _outsideAINodes.Add(node);
                }
            }

            return _outsideAINodes.ToArray();
        }
    }
    
    public void PopulateBrackenRoomAloeNodes(Transform brackenRoomTransform)
    {
        lock (Padlock)
        {
            _brackenRoomAloeNodes.Clear();
            List<Vector3> nodes =
            [
                brackenRoomTransform.position + new Vector3(0, 0f, -13.92f),
                brackenRoomTransform.position + new Vector3(-5.3f, 0f, -11f),
                brackenRoomTransform.position + new Vector3(-4.8f, 0f, -3.5f),
                brackenRoomTransform.position + new Vector3(-0.2f, 0f, -1.09f)
            ];

            foreach (Vector3 node in nodes)
            {
                _brackenRoomAloeNodes.Add(new BrackenRoomAloeNode(node));
            }
        }
    }

    public Vector3 OccupyBrackenRoomAloeNode()
    {
        lock (Padlock)
        {
            BrackenRoomAloeNode node = _brackenRoomAloeNodes.FirstOrDefault(n => !n.taken);
            if (node == null) return Vector3.zero;
            node.taken = true;
            return node.nodePosition;
        }
    }

    public void UnOccupyBrackenRoomAloeNode(Vector3 nodePosition)
    {
        lock (Padlock)
        {
            BrackenRoomAloeNode node = _brackenRoomAloeNodes.FirstOrDefault(n => n.nodePosition == nodePosition);
            if (node != null)
            {
                node.taken = false;
            }
        }
    }
    
    public void FlushDictionaries()
    {
        lock (Padlock)
        {
            _brackenRoomAloeNodes.Clear();
            _aloeBoundKidnaps.Clear();
            _aloeBoundStalks.Clear();
            _playersMaxHealth.Clear();
            _insideAINodes.Clear();
            _outsideAINodes.Clear();
        }
    }

    private void LogDebug(string msg)
    {
#if DEBUG
        Mls?.LogInfo(msg);
#endif
    }
}