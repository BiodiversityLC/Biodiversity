using Biodiversity.Core.AssetLoading;
using Biodiversity.Core.Attributes;

namespace Biodiversity.Creatures.MicBird;

#pragma warning disable CS0649

internal class MicBirdAssets(string filePath) : BiodiverseAssetBundle<MicBirdAssets>(filePath)
{
    [LoadFromBundle("MicBird.asset")]
    public EnemyType MicBirdEnemyType;

    [LoadFromBundle("MicBirdTN")]
    public TerminalNode MicBirdTerminalNode;

    [LoadFromBundle("MicBirdKW")]
    public TerminalKeyword MicBirdTerminalKeyword;
}