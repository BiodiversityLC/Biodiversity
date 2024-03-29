using BepInEx.Configuration;
using Biodiversity.Util.Config;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Biodiversity.Creatures.WaxSoldier;

[Serializable]
internal class WaxSoldierConfig(ConfigFile configFile) : BiodiverseConfig<WaxSoldierConfig>(configFile)
{
    // All values are currently random
    [Header("General Settings")]

    public float NormalSpeed = 5f;
    public float MoltenSpeed = 10f;

    public float AggroRange = 50f; //Distance from guardLocation until Wax Soldier returns?

    public float StunTimeAfterHit = 1f; //Time stunned after changing into molten state
}