using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Biodiversity.Creatures.HoneyFeeder;
[Serializable]
public class HoneyFeederConfig(ConfigFile configFile) : BiodiverseConfig<HoneyFeederConfig>(configFile) {
    [field: Header("General Settings")]
    public float SightDistance { get; private set; } = 25;
    [field: Tooltip("24 hour time on when the honey feeder will wake up and start moving.")]
    public string WakeUpTime { get; private set; } = "13:00";

    [field: Tooltip("TEMPORARY SETTING, WILL BE REMOVED LATER.")]
    public int Rarity { get; private set; } = 100;

    [field: Range(0f, 10f)]
    public float NormalSpeed { get; private set; } = 3.5f;
    public float ChargeSpeed { get; private set; } = 12f;

    [field: Header("Backup Behaviour")]
    public float MinBackupAmount { get; private set; } = 5f;
    public float MaxBackupAmount { get; private set; } = 7f;

    [field: Header("Charge Attack")]
    [field: Tooltip("Amount of damage to deal to player when hit by the Honey Feeder")]
    [field: Range(0, 100)]
    public int ChargeDamage { get; private set; } = 35;
    public float TooCloseAmount { get; private set; } = 5f;
    public float StunTimeAfterHit { get; private set; } = 3f;

    [field: Tooltip("After hitting player how much further should the HoneyFeeder go through?")]
    public float FollowthroughAmount { get; private set; } = 5f;

    [field: Header("Digestion")]
    [field: Tooltip("24 hour time on when the hive becomes partly digested.")]
    public string TimeWhenPartlyDigested { get; private set; } = "20:00";

    [field: Tooltip("Multiplier on the hive when it becomes partly digested.")]
    public float PartlyDigestedScrapMultiplier { get; private set; } = 1.5f;
}
