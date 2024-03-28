using Biodiversity.Util.Config;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Rendering.HighDefinition;

namespace Biodiversity.Creatures.HoneyFeeder;
[Serializable]
public class HoneyFeederConfig : BiodiverseConfig<HoneyFeederConfig> {
    public float HiveDetectionDistance = 15;

    public float NormalSpeed = 3.5f;
    public float ChargeSpeed = 6f;
}
