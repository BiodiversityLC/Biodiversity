using BepInEx.Logging;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Biodiversity.Creatures.Aloe.Types;
using Biodiversity.Creatures.Aloe.Types.Networking;
using GameNetcodeStuff;
using System;
using System.Collections.Concurrent;
using Unity.Collections;
using Unity.Netcode;
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
                _instance.RegisterMessageHandlers();
            }

            return _instance;
        }
    }
    
    private AloeSharedData() { }

    private readonly ConcurrentDictionary<string, ulong> _aloeBoundKidnaps = new();
    private readonly ConcurrentDictionary<AloeServer, PlayerControllerB> _aloeBoundKidnapsServer = new();
    private readonly ConcurrentDictionary<string, ulong> _aloeBoundStalks = new();
    private readonly ConcurrentDictionary<ulong, int> _playersMaxHealth = new();
    private readonly List<BrackenRoomAloeNode> _brackenRoomAloeNodes = [];
    private readonly ConcurrentBag<GameObject> _insideAINodes = [];
    private readonly ConcurrentBag<GameObject> _outsideAINodes = [];

    public Vector3 BrackenRoomDoorPosition { get; set; } = Vector3.zero;

    public IReadOnlyDictionary<string, ulong> AloeBoundKidnaps => _aloeBoundKidnaps;
    public IReadOnlyDictionary<string, ulong> AloeBoundStalks => _aloeBoundStalks;
    public IReadOnlyDictionary<ulong, int> PlayersMaxHealth => _playersMaxHealth;
    
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
    
    public void Bind(AloeServer server, PlayerControllerB player, BindType bindType)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SendBindToClients(server.aloeId, player.actualClientId, bindType);
            if (bindType == BindType.Kidnap) _aloeBoundKidnapsServer.TryAdd(server, player);
        }
        
        else SendBindRequestToServer(server.aloeId, player.actualClientId, bindType);
    }

    public void Unbind(AloeServer server, BindType bindType)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SendUnbindToClients(server.aloeId, bindType);
            if (bindType == BindType.Kidnap) _aloeBoundKidnapsServer.TryRemove(server, out _);
        }
        
        else SendUnbindRequestToServer(server.aloeId, bindType);
    }

    public bool IsAloeKidnapBound(AloeServer server)
    {
        if (server == null)
        {
            throw new ArgumentNullException(nameof(server), "The provided aloe server instance is null, cannot determine whether she is kidnap bound.");
        }

        return _aloeBoundKidnaps.ContainsKey(server.aloeId);
    }

    public bool IsPlayerKidnapBound(PlayerControllerB player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player),
                "The given player object instance is null, cannot determine whether they are kidnap bound.");
        }

        return _aloeBoundKidnaps.Values.Any(p => p == player.actualClientId);
    }
    
    public bool IsPlayerStalkBound(PlayerControllerB player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player),
                "The given player object instance is null, cannot determine whether they are stalk bound.");
        }

        return _aloeBoundStalks.Values.Any(p => p == player.actualClientId);
    }

    private static void SendBindRequestToServer(string aloeId, ulong playerId, BindType bindType)
    {
        BindMessage networkMessage = new() { AloeId = aloeId, PlayerId = playerId };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage($"Aloe_Bind{bindType}Request", NetworkManager.ServerClientId, writer);
    }
    
    private static void SendUnbindRequestToServer(string aloeId, BindType bindType)
    {
        UnbindMessage networkMessage = new() { AloeId = aloeId};
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage($"Aloe_Unbind{bindType}Request", NetworkManager.ServerClientId, writer);
    }

    private static void SendBindToClients(string aloeId, ulong playerId, BindType bindType)
    {
        BindMessage networkMessage = new() { AloeId = aloeId, PlayerId = playerId };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll($"Aloe_Bind{bindType}", writer);
    }
    
    private static void SendUnbindToClients(string aloeId, BindType bindType)
    {
        BindMessage networkMessage = new() { AloeId = aloeId };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll($"Aloe_Unbind{bindType}", writer);
    }

    private void RegisterMessageHandlers()
    {
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_BindKidnapMessage", HandleBindKidnapMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_UnbindKidnapMessage", HandleUnbindKidnapMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_BindStalkMessage", HandleBindStalkMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_UnbindStalkMessage", HandleUnbindStalkMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_PlayerTeleportedMessage", HandlePlayerTeleportedMessage);
    }

    private void HandleBindKidnapMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out BindMessage message);
        _aloeBoundKidnaps.TryAdd(message.AloeId, message.PlayerId);
    }

    private void HandleUnbindKidnapMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out UnbindMessage message);
        _aloeBoundKidnaps.TryRemove(message.AloeId, out _);
    }
    
    private void HandleBindStalkMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out BindMessage message);
        _aloeBoundStalks.TryAdd(message.AloeId, message.PlayerId);
    }

    private void HandleUnbindStalkMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out UnbindMessage message);
        _aloeBoundStalks.TryRemove(message.AloeId, out _);
    }

    private void HandlePlayerTeleportedMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out PlayerTeleportedMessage message);
        AloeServer aloeServer = _aloeBoundKidnapsServer.Keys.FirstOrDefault(aloe => aloe.aloeId == message.AloeId);
        
        if (aloeServer == null) Mls.LogError($"In {nameof(HandlePlayerTeleportedMessage)}, the given Aloe ID: '{message.AloeId}' does not belong to any Aloe currently in the game.");
        else aloeServer.SetTargetPlayerEscapedByTeleportation();
    }

    public void SetPlayerMaxHealth(PlayerControllerB player, int maxHealth)
    {
        _playersMaxHealth[player.actualClientId] = maxHealth;
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
        
        _playersMaxHealth.TryGetValue(player.actualClientId, out int maxHealth);
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