﻿using BepInEx.Logging;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal class BaseStateMachineBehaviour : StateMachineBehaviour
{
    protected ManualLogSource Mls;
    protected string AloeId;

    protected AloeNetcodeController NetcodeController;
    protected AloeServerAI AloeServerAIInstance;
    protected AloeClient AloeClientInstance;

    private bool _networkEventsSubscribed;

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeToNetworkEvents();
    }

    internal void Initialize(
        AloeNetcodeController receivedNetcodeController,
        AloeServerAI receivedAloeServerAI,
        AloeClient receivedAloeClient)
    {
        NetcodeController = receivedNetcodeController;
        AloeServerAIInstance = receivedAloeServerAI;
        AloeClientInstance = receivedAloeClient;
        SubscribeToNetworkEvents();
    }

    private void SubscribeToNetworkEvents()
    {
        if (_networkEventsSubscribed || NetcodeController == null) return;
        NetcodeController.OnSyncAloeId += HandleSyncAloeId;
        _networkEventsSubscribed = true;
    }

    private void UnsubscribeToNetworkEvents()
    {
        if (!_networkEventsSubscribed || NetcodeController == null) return;
        NetcodeController.OnSyncAloeId -= HandleSyncAloeId;
        _networkEventsSubscribed = false;
    }

    private void HandleSyncAloeId(string receivedAloeId)
    {
        AloeId = receivedAloeId;
        Mls?.Dispose();
        Mls = Logger.CreateLogSource(
            $"Aloe Animation State Machine Behaviour {AloeId}");

        LogDebug("Successfully synced aloe identifier");
    }

    protected void LogDebug(string msg)
    {
#if DEBUG
        Mls?.LogInfo(msg);
#endif
    }
}