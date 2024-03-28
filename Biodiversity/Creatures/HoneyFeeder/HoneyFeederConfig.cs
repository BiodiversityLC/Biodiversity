using Biodiversity.Util.Config;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Biodiversity.Creatures.HoneyFeeder;
[Serializable]
public class HoneyFeederConfig : BiodiverseConfig<HoneyFeederConfig> {
    public float SightDistance = 15;

    public float NormalSpeed = 3.5f;
    public float ChargeSpeed = 6f;

    [Header("Backup Behaviour")]
    public float MinBackupAmount = 10f;
    public float MaxBackupAmount = 15f;

    [Header("Charge Attack")]
    public int ChargeDamage = 35;
    public float TooCloseAmount = 5f;
    public float StunTimeAfterHit = 3f;
}
