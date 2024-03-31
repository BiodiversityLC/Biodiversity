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
    public float SightDistance { get; private set; } = 15;

    [field: Range(0f, 10f)]
    public float NormalSpeed { get; private set; } = 3.5f;
    public float ChargeSpeed { get; private set; } = 6f;

    [field: Header("Backup Behaviour")]
    public float MinBackupAmount { get; private set; } = 10f;
    public float MaxBackupAmount { get; private set; } = 15f;

    [field: Header("Charge Attack")]
    [field: Tooltip("Amount of damage to deal to player when hit by the Honey Feeder")]
    [field: Range(0, 100)]
    public int ChargeDamage { get; private set; } = 35;
    public float TooCloseAmount { get; private set; } = 5f;
    public float StunTimeAfterHit { get; private set; } = 3f;
}
