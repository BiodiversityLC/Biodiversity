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

namespace Biodiversity.Creatures.Aloe;

[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
internal class AloeSharedData
{
    private static readonly object Padlock = new();
    private static AloeSharedData _instance;

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
    private readonly ConcurrentDictionary<ulong, string> _kidnappedPlayerToAloe = new();
    
    private readonly ConcurrentDictionary<string, ulong> _aloeBoundStalks = new();
    private readonly ConcurrentDictionary<ulong, string> _stalkedPlayerToAloe = new();
    
    private readonly ConcurrentDictionary<AloeServerAI, PlayerControllerB> _aloeBoundKidnapsServer = new();
    
    private readonly ConcurrentDictionary<ulong, int> _playersMaxHealth = new();
    
    private readonly List<BrackenRoomAloeNode> _brackenRoomAloeNodes = [];
    
    private readonly ConcurrentBag<GameObject> _insideAINodes = [];
    private readonly ConcurrentBag<GameObject> _outsideAINodes = [];

    public Vector3 BrackenRoomDoorPosition { get; set; } = Vector3.zero;

    public IReadOnlyDictionary<string, ulong> AloeBoundKidnaps => _aloeBoundKidnaps;
    public IReadOnlyDictionary<string, ulong> AloeBoundStalks => _aloeBoundStalks;
    public IReadOnlyDictionary<ulong, int> PlayersMaxHealth => _playersMaxHealth;
    public IReadOnlyList<BrackenRoomAloeNode> BrackenRoomAloeNodes => _brackenRoomAloeNodes.AsReadOnly();
    
    public void Bind(AloeServerAI serverAI, PlayerControllerB player, BindType bindType)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SendBindToClients(serverAI.BioId, player.actualClientId, bindType);
            if (bindType == BindType.Kidnap) _aloeBoundKidnapsServer.TryAdd(serverAI, player);
        }
        else
        {
            SendBindRequestToServer(serverAI.BioId, player.actualClientId, bindType);
        }
    }

    public void Unbind(AloeServerAI serverAI, BindType bindType)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            SendUnbindToClients(serverAI.BioId, bindType);
            if (bindType == BindType.Kidnap) _aloeBoundKidnapsServer.TryRemove(serverAI, out _);
        }
        
        else SendUnbindRequestToServer(serverAI.BioId, bindType);
    }

    public bool IsAloeKidnapBound(AloeServerAI serverAI)
    {
        if (serverAI == null)
        {
            throw new ArgumentNullException(nameof(serverAI), "The provided aloe server instance is null, cannot determine whether she is kidnap bound.");
        }

        return _aloeBoundKidnaps.ContainsKey(serverAI.BioId);
    }
    
    public bool IsPlayerKidnapBound(PlayerControllerB player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player),
                "The given player object instance is null, cannot determine whether they are kidnap bound.");
        }
        
        return _kidnappedPlayerToAloe.ContainsKey(player.actualClientId);
    }
    
    public bool IsPlayerStalkBound(PlayerControllerB player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player),
                "The given player object instance is null, cannot determine whether they are stalk bound.");
        }

        return _stalkedPlayerToAloe.ContainsKey(player.actualClientId);
    }

    private static void SendBindRequestToServer(string bioId, ulong playerId, BindType bindType)
    {
        BiodiversityPlugin.LogVerbose($"Sending bind request to server: BioId: {bioId}, playerId: {playerId}, bindType: {bindType}");
        BindMessage networkMessage = new() { BioId = bioId, PlayerId = playerId, BindType = bindType};
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("Aloe_BindRequest", NetworkManager.ServerClientId, writer);
    }
    
    private static void SendUnbindRequestToServer(string bioId, BindType bindType)
    {
        BiodiversityPlugin.LogVerbose($"Sending unbind request to server: BioId: {bioId}, bindType: {bindType}");
        UnbindMessage networkMessage = new() { BioId = bioId, BindType = bindType};
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("Aloe_UnbindRequest", NetworkManager.ServerClientId, writer);
    }

    private static void SendBindToClients(string bioId, ulong playerId, BindType bindType)
    {
        BiodiversityPlugin.LogVerbose($"Sending bind request to clients: BioId: {bioId}, playerId: {playerId}, bindType: {bindType}");
        BindMessage networkMessage = new() { BioId = bioId, PlayerId = playerId, BindType = bindType };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("Aloe_BindMessage", writer);
    }
    
    private static void SendUnbindToClients(string bioId, BindType bindType)
    {
        BiodiversityPlugin.LogVerbose($"Sending unbind request to clients: BioId: {bioId}, bindType: {bindType}");
        UnbindMessage networkMessage = new() { BioId = bioId, BindType = bindType };
        using FastBufferWriter writer = new(128, Allocator.Temp, 128);
        writer.WriteNetworkSerializable(networkMessage);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll("Aloe_UnbindMessage", writer);
    }

    private void RegisterMessageHandlers()
    {
        BiodiversityPlugin.LogVerbose($"Registering message handlers for {nameof(AloeSharedData)}.");
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_BindMessage", HandleBindMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_UnbindMessage", HandleUnbindMessage);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_BindRequest", HandleBindRequest);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_UnbindRequest", HandleUnbindRequest);
        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("Aloe_PlayerTeleportedRequest", HandlePlayerTeleportedMessage);
    }

    private static void HandleBindRequest(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out BindMessage message);
        SendBindToClients(message.BioId, message.PlayerId, message.BindType);
    }
    
    private static void HandleUnbindRequest(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out UnbindMessage message);
        SendUnbindToClients(message.BioId, message.BindType);
    }

    private void HandleBindMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out BindMessage message);
        BiodiversityPlugin.LogVerbose($"Processing bind message from server: BioId: {message.BioId}, playerId: {message.PlayerId}, bindType: {message.BindType}");

        switch (message.BindType)
        {
            case BindType.Kidnap:
                _aloeBoundKidnaps.TryAdd(message.BioId, message.PlayerId);
                _kidnappedPlayerToAloe.TryAdd(message.PlayerId, message.BioId);
                break;
            case BindType.Stalk:
                _aloeBoundStalks.TryAdd(message.BioId, message.PlayerId);
                _stalkedPlayerToAloe.TryAdd(message.PlayerId, message.BioId);
                break;
        }
    }
    
    private void HandleUnbindMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out UnbindMessage message);
        BiodiversityPlugin.LogVerbose($"Processing unbind message from server: BioId: {message.BioId}, bindType: {message.BindType}");

        switch (message.BindType)
        {
            case BindType.Kidnap:
                if (_aloeBoundKidnaps.TryRemove(message.BioId, out ulong playerId))
                    _kidnappedPlayerToAloe.TryRemove(playerId, out _);
                break;
            case BindType.Stalk:
                if (_aloeBoundStalks.TryRemove(message.BioId, out ulong stalkedPlayerId)) 
                    _stalkedPlayerToAloe.TryRemove(stalkedPlayerId, out _);
                break;
        }
    }

    private void HandlePlayerTeleportedMessage(ulong clientId, FastBufferReader reader)
    {
        reader.ReadNetworkSerializable(out PlayerTeleportedMessage message);
        BiodiversityPlugin.LogVerbose($"Received player teleported message: BioId: {message.BioId}, playerId: {message.PlayerId}");
        AloeServerAI aloeServerAI = _aloeBoundKidnapsServer.Keys.FirstOrDefault(aloe => aloe.BioId == message.BioId);
        
        if (aloeServerAI == null) BiodiversityPlugin.Logger.LogError($"In {nameof(HandlePlayerTeleportedMessage)}, the given Aloe ID: '{message.BioId}' does not belong to any Aloe currently in the game.");
        else aloeServerAI.SetTargetPlayerEscapedByTeleportation();
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
            BiodiversityPlugin.Logger.LogWarning($"The provided player is null, we will assume that their max health is 100.");
            return 100;
        }
        
        _playersMaxHealth.TryGetValue(player.actualClientId, out int maxHealth);
        if (maxHealth > 0) return maxHealth;
        
        BiodiversityPlugin.Logger.LogWarning($"Max health of given player {player.playerUsername} is {maxHealth}. This should not happen. Returning a max health of 100 as a failsafe.");
        return 100;
    }

    public GameObject[] GetInsideAINodes()
    {
        lock (Padlock)
        {
            if (_insideAINodes.Count == 0)
            {
                GameObject[] insideAINodes = GameObject.FindGameObjectsWithTag("AINode");
                for (int i = 0; i < insideAINodes.Length; i++)
                {
                    GameObject node = insideAINodes[i];
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
                for (int i = 0; i < outsideAINodes.Length; i++)
                {
                    GameObject node = outsideAINodes[i];
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

            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3 node = nodes[i];
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
            _kidnappedPlayerToAloe.Clear();
            _aloeBoundStalks.Clear();
            _stalkedPlayerToAloe.Clear();
            _playersMaxHealth.Clear();
            _insideAINodes.Clear();
            _outsideAINodes.Clear();
        }
    }
}