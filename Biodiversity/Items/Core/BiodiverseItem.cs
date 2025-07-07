using Biodiversity.Creatures.Aloe;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

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
    
    /// <summary>
    /// A dictionary containing arrays of <see cref="AudioClip"/>s, indexed by category name.
    /// </summary>
    protected Dictionary<string, AudioClip[]> AudioClips { get; set; } = new();
    
    /// <summary>
    /// A dictionary containing <see cref="AudioSource"/> components, indexed by category name.
    /// </summary>
    protected Dictionary<string, AudioSource> AudioSources { get; set; } = new();
    
    protected EnemyAI enemyHeldBy;
    protected bool isHeldByPlayer;
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsServer) return;
        _networkBioId.Value = new FixedString32Bytes(Guid.NewGuid().ToString("N").Substring(0, 8));
    }

    public override void Start()
    {
        base.Start();
        CollectAudioClipsAndSources();
    }

    /// <summary>
    /// Collects all <see cref="AudioClip"/> and <see cref="AudioSource"/> fields defined on the object.
    /// Then it populates the <see cref="BiodiverseItem.AudioClips"/> and <see cref="BiodiverseItem.AudioSources"/> dictionaries based on field names and values.
    /// </summary>
    protected virtual void CollectAudioClipsAndSources()
    {
        AudioClips = new Dictionary<string, AudioClip[]>();
        AudioSources = new Dictionary<string, AudioSource>();
    }
    
    /// <summary>
    /// Requests the server to play a specific category of audio clip on a designated <see cref="UnityEngine.AudioSource"/>.
    /// It will randomly select an audio clip from the array of clips assigned to that particular audio  .
    /// This method ensures that the selected audio clip is synchronized across all clients.
    /// </summary>
    /// <param name="audioClipType">
    /// A string identifier representing the type/category of the audio clip to be played 
    /// (e.g., "Stun", "Laugh", "Ambient").
    /// </param>
    /// <param name="audioSourceType">
    /// A string identifier representing the specific <see cref="UnityEngine.AudioSource"/> on which the audio clip should be played 
    /// (e.g., "CreatureVoice", "CreatureSFX", "Footsteps").
    /// </param>
    /// <param name="interrupt">
    /// Determines whether the current audio playback on the specified <see cref="UnityEngine.AudioSource"/> should be interrupted 
    /// before playing the new audio clip.
    /// </param>
    /// <param name="audibleInWalkieTalkie">
    /// Indicates whether the played audio should be transmitted through the walkie-talkie system, making it audible 
    /// to players using walkie-talkies.
    /// </param>
    /// <param name="audibleByEnemies">
    /// Determines whether the played audio should be detectable by enemy AI, potentially alerting them to the player's 
    /// actions.
    /// </param>
    /// <param name="slightlyVaryPitch">
    /// Whether to slightly vary the pitch between 0.9 and 1.1 randomly.
    /// </param>
    [ServerRpc]
    protected void PlayRandomAudioClipTypeServerRpc(
        string audioClipType,
        string audioSourceType,
        bool interrupt = false,
        bool audibleInWalkieTalkie = true,
        bool audibleByEnemies = false,
        bool slightlyVaryPitch = false)
    {
        // Validate audio clip type
        if (!AudioClips.TryGetValue(audioClipType, out AudioClip[] clipArr) || clipArr == null || clipArr.Length == 0)
        {
            LogWarning($"Audio Clip Type '{audioClipType}' not found, is null, or empty.");
            return;
        }

        // Validate audio source type
        if (!AudioSources.TryGetValue(audioSourceType, out AudioSource source) || !source)
        {
            LogWarning($"Audio Source Type '{audioSourceType}' not found or null.");
            return;
        }

        // Select a random clip index
        int clipIndex = UnityEngine.Random.Range(0, clipArr.Length);
        PlayAudioClipTypeClientRpc(audioClipType, audioSourceType, clipIndex, interrupt, audibleInWalkieTalkie,
            audibleByEnemies, slightlyVaryPitch);
    }

    /// <summary>
    /// Plays the selected audio clip on the specified <see cref="UnityEngine.AudioSource"/> across all clients.
    /// This method is invoked by the server to ensure synchronized audio playback.
    /// </summary>
    /// <param name="audioClipType">
    /// A string identifier representing the type/category of the audio clip to be played 
    /// (e.g., "Stun", "Chase", "Ambient").
    /// </param>
    /// <param name="audioSourceType">
    /// A string identifier representing the specific <see cref="UnityEngine.AudioSource"/> on which the audio clip should be played 
    /// (e.g., "CreatureVoice", "CreatureSfx", "Footsteps").
    /// </param>
    /// <param name="clipIndex">
    /// The index of the <see cref="AudioClip"/> within the array corresponding to <paramref name="audioClipType"/> 
    /// that should be played.
    /// </param>
    /// <param name="interrupt">
    /// Determines whether the current audio playback on the specified <see cref="UnityEngine.AudioSource"/> should be interrupted 
    /// before playing the new audio clip.
    /// </param>
    /// <param name="audibleInWalkieTalkie">
    /// Indicates whether the played audio should be transmitted through the walkie-talkie system, making it audible 
    /// to players using walkie-talkies.
    /// </param>
    /// <param name="audibleByEnemies">
    /// Determines whether the played audio should be detectable by enemy AI, potentially alerting them to the player's 
    /// actions.
    /// </param>
    /// <param name="slightlyVaryPitch">
    /// Whether to slightly vary the pitch between the original pitch +/- 0.1.
    /// </param>
    [ClientRpc]
    private void PlayAudioClipTypeClientRpc(
        string audioClipType,
        string audioSourceType,
        int clipIndex,
        bool interrupt = false,
        bool audibleInWalkieTalkie = true,
        bool audibleByEnemies = false,
        bool slightlyVaryPitch = false)
    {
        // Validate audio clip type
        if (!AudioClips.TryGetValue(audioClipType, out AudioClip[] clipArr) || clipArr == null || clipArr.Length == 0)
        {
            LogWarning($"Client: Audio Clip Type '{audioClipType}' not found, is null, or empty.");
            return;
        }
        
        if (clipIndex < 0 || clipIndex >= clipArr.Length)
        {
            LogWarning($"Client: Invalid clip index {clipIndex} received for type '{audioClipType}' (Count: {clipArr.Length}).");
            return;
        }

        AudioClip clipToPlay = clipArr[clipIndex];
        if (!clipToPlay)
        {
            LogWarning($"Client: Audio clip at index {clipIndex} for type '{audioClipType}' is null.");
            return;
        }
        
        if (!AudioSources.TryGetValue(audioSourceType, out AudioSource selectedAudioSource) || selectedAudioSource == null)
        {
            LogWarning($"Client: Audio Source Type '{audioSourceType}' not found or is null.");
            return;
        }

        LogVerbose(
            $"Client: Playing audio clip: {clipToPlay.name} for type '{audioClipType}' on AudioSource '{audioSourceType}'.");

        float oldPitch = selectedAudioSource.pitch;
        
        if (interrupt && selectedAudioSource.isPlaying) selectedAudioSource.Stop();
        if (slightlyVaryPitch) selectedAudioSource.pitch = UnityEngine.Random.Range(oldPitch - 0.1f, oldPitch + 0.1f);
        
        selectedAudioSource.PlayOneShot(clipToPlay);
        
        if (audibleInWalkieTalkie) WalkieTalkie.TransmitOneShotAudio(selectedAudioSource, clipToPlay, selectedAudioSource.volume);
        if (audibleByEnemies) RoundManager.Instance.PlayAudibleNoise(selectedAudioSource.transform.position);

        if (slightlyVaryPitch) selectedAudioSource.pitch = oldPitch;
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