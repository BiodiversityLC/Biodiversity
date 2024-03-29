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
    [Header("General Settings")]
    public float SightDistance = 15;

    [Range(0f, 10f)]
    public float NormalSpeed = 3.5f;
    public float ChargeSpeed = 6f;

    [Header("Backup Behaviour")]
    public float MinBackupAmount = 10f;
    public float MaxBackupAmount = 15f;

    [Header("Charge Attack")]
    [Tooltip("Amount of damage to deal to player when hit by the Honey Feeder")]
    public int ChargeDamage = 35;
    public float TooCloseAmount = 5f;
    public float StunTimeAfterHit = 3f;
}
