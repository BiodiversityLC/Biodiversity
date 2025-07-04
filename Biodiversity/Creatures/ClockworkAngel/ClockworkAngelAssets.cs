using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;
using UnityEngine;

namespace Biodiversity.Creatures.ClockworkAngel;

#pragma warning disable CS0649

internal class ClockworkAngelAssets(string filePath) : BiodiverseAssetBundle<ClockworkAngelAssets>(filePath)
{
    [LoadFromBundle("ClockworkAngel.prefab")]
    public GameObject enemy;

    [LoadFromBundle("AngelSpotlight.prefab")]
    public GameObject AngelSpotlight;

    [LoadFromBundle("AngelAgent.prefab")]
    public GameObject AngelAgent;
}