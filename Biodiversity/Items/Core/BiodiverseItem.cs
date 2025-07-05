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
    
    protected EnemyAI enemyHeldBy;
    protected bool isHeldByPlayer;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        _networkBioId.Value = new FixedString32Bytes(Guid.NewGuid().ToString("N").Substring(0, 8));
    }
    
    #region Abstract Item Class Event Functions
    public override void EquipItem()
    {
        base.EquipItem();

        isHeld = true;
        isHeldByPlayer = true;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }
    
    public override void PocketItem()
    {
        base.PocketItem();
        
        isHeld = false;
        isHeldByPlayer = false;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }
    
    public override void GrabItem()
    {
        base.GrabItem();
        
        isHeld = true;
        isHeldByPlayer = true;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }
    
    public override void GrabItemFromEnemy(EnemyAI enemy)
    {
        base.GrabItemFromEnemy(enemy);
        
        isHeld = true;
        isHeldByPlayer = false;
        isHeldByEnemy = true;
        enemyHeldBy = enemy;
        playerHeldBy = null;
    }
    
    public override void DiscardItem()
    {
        base.DiscardItem();

        isHeld = false;
        isHeldByPlayer = false;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }
    
    public override void DiscardItemFromEnemy()
    {
        base.DiscardItemFromEnemy();
        
        isHeld = false;
        isHeldByPlayer = false;
        isHeldByEnemy = false;
        enemyHeldBy = null;
    }
    #endregion
    
    #region Logging
    protected void LogInfo(object message) => BiodiversityPlugin.Logger?.LogInfo($"{GetLogPrefix()} {message}");

    protected void LogVerbose(object message)
    {
        if (BiodiversityPlugin.Config?.VerboseLoggingEnabled ?? false)
            BiodiversityPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");
    }

    protected void LogDebug(object message) => BiodiversityPlugin.Logger.LogDebug($"{GetLogPrefix()} {message}");

    protected void LogError(object message) => BiodiversityPlugin.Logger.LogError($"{GetLogPrefix()} {message}");

    protected void LogWarning(object message) => BiodiversityPlugin.Logger.LogWarning($"{GetLogPrefix()} {message}");

    protected virtual string GetLogPrefix()
    {
        return $"[{itemProperties.name}]";
    }
    #endregion
}