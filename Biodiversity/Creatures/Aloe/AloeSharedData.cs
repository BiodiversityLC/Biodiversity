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

    private static readonly ManualLogSource Mls =
        Logger.CreateLogSource($"{MyPluginInfo.PLUGIN_GUID} | Aloe Shared Data");

    private readonly bool _hasRegisteredMessageHandlers;

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

    private AloeSharedData()
    {
        if (_hasRegisteredMessageHandlers) return;
        _hasRegisteredMessageHandlers = true;
        RegisterMessageHandlers();
    }

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
        else
        {
            SendBindRequestToServer(server.aloeId, player.actualClientId, bindType);
        }
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

    private void SendBindRequestToServer(string aloeId, ulong playerId, BindType bindType)
    {
        LogDebug($"Sending bind request to server: aloeId: {aloeId}, playerId: {playerId}, bindType: {bindType}");
        BindMessage networkMessage = new() { AloeId = aloeId, PlayerId = playerId, BindType = bindType};
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage($"Aloe_BindRequest", NetworkManager.ServerClientId, writer);
    }
    
    private void SendUnbindRequestToServer(string aloeId, BindType bindType)
    {
        LogDebug($"Sending unbind request to server: aloeId: {aloeId}, bindType: {bindType}");
        UnbindMessage networkMessage = new() { AloeId = aloeId, BindType = bindType};
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage($"Aloe_UnbindRequest", NetworkManager.ServerClientId, writer);
    }

    private void SendBindToClients(string aloeId, ulong playerId, BindType bindType)
    {
        LogDebug($"Sending bind request to clients: aloeId: {aloeId}, playerId: {playerId}, bindType: {bindType}");
        BindMessage networkMessage = new() { AloeId = aloeId, PlayerId = playerId, BindType = bindType };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll($"Aloe_BindMessage", writer);
    }
    
    private void SendUnbindToClients(string aloeId, BindType bindType)
    {
        LogDebug($"Sending unbind request to clients: aloeId: {aloeId}, bindType: {bindType}");
        UnbindMessage networkMessage = new() { AloeId = aloeId, BindType = bindType };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll($"Aloe_UnbindMessage", writer);
    }

    private void RegisterMessageHandlers()
    {
        LogDebug("Registering message handlers.");
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_BindMessage", HandleBindMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_UnbindMessage", HandleUnbindMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_BindRequest", HandleBindRequest);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_UnbindRequest", HandleUnbindRequest);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_PlayerTeleportedRequest", HandlePlayerTeleportedMessage);
    }

    private void HandleBindRequest(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out BindMessage message);
        SendBindToClients(message.AloeId, message.PlayerId, message.BindType);
    }
    
    private void HandleUnbindRequest(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out UnbindMessage message);
        SendUnbindToClients(message.AloeId, message.BindType);
    }

    private void HandleBindMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out BindMessage message);
        LogDebug($"Processing bind message from server: aloeId: {message.AloeId}, playerId: {message.PlayerId}, bindType: {message.BindType}");

        switch (message.BindType)
        {
            case BindType.Kidnap:
                _aloeBoundKidnaps.TryAdd(message.AloeId, message.PlayerId);
                break;
            case BindType.Stalk:
                _aloeBoundStalks.TryAdd(message.AloeId, message.PlayerId);
                break;
        }
    }
    
    private void HandleUnbindMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out UnbindMessage message);
        LogDebug($"Processing unbind message from server: aloeId: {message.AloeId}, bindType: {message.BindType}");

        switch (message.BindType)
        {
            case BindType.Kidnap:
                _aloeBoundKidnaps.TryRemove(message.AloeId, out _);
                break;
            case BindType.Stalk:
                _aloeBoundStalks.TryRemove(message.AloeId, out _);
                break;
        }
    }

    private void HandlePlayerTeleportedMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out PlayerTeleportedMessage message);
        LogDebug($"Received player teleported message: aloeId: {message.AloeId}, playerId: {message.PlayerId}");
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