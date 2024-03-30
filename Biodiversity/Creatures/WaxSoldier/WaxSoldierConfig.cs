using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

[Serializable]
public class WaxSoldierConfig(ConfigFile configFile) : BiodiverseConfig<WaxSoldierConfig>(configFile)
{
    // All values are currently random
    [Header("General Settings")]

    public int MusketDamage = 100;
    public int BayonetDamage = 35;

    public float NormalSpeed = 5f;
    public float MoltenSpeed = 10f;

    public float StartAggroRange = 5f; //Maximum distance to change into PURSUING state (yes no?)
    public float AggroRange = 50f; //Distance from guardLocation until Wax Soldier returns?

    public float StunTimeAfterHit = 1f; //Time stunned after changing into molten state
}