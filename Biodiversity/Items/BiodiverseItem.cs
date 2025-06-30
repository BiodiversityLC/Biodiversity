using System;
using Unity.Collections;
using Unity.Netcode;

namespace Biodiversity.Items;

public class BiodiverseItem : PhysicsProp
{
    /// <summary>
    /// A unique identifier for the object, stored as a networked fixed-size string.
    /// This ID is generated as a GUID on the server and synchronized to all clients.
    /// </summary>
    private readonly NetworkVariable<FixedString32Bytes> _networkBioId = new();
    
    /// <summary>
    /// Gets the unique identifier (BioId) for this object as a string.
    /// </summary>
    public string BioId => _networkBioId.Value.ToString();
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        _networkBioId.Value = new FixedString32Bytes(Guid.NewGuid().ToString("N").Substring(0, 8));
    }
    
    #region Logging

    internal void LogInfo(object message) => BiodiversityPlugin.Logger?.LogInfo($"{GetLogPrefix()} {message}");

    internal void LogVerbose(object message)
    {
        if (BiodiversityPlugin.Config?.VerboseLoggingEnabled ?? false)
            BiodiversityPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");
    }

    internal void LogDebug(object message) => BiodiversityPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");

    internal void LogError(object message) => BiodiversityPlugin.Logger.LogError($"{GetLogPrefix()} {message}");

    internal void LogWarning(object message) => BiodiversityPlugin.Logger.LogWarning($"{GetLogPrefix()} {message}");

    protected virtual string GetLogPrefix()
    {
        return $"[{itemProperties.name}]";
    }

    #endregion
}