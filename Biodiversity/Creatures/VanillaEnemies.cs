using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Unity.Netcode;

namespace Biodiversity.Creatures;

[SuppressMessage("ReSharper", "IdentifierTypo",
    Justification = "All these variables are named very specifically, hence why the disabled message.")]
public static class VanillaEnemies
{
    public static EnemyType Flowerman { get; private set; }
    public static EnemyType Centipede { get; private set; }
    public static EnemyType MouthDog { get; private set; }
    public static EnemyType Crawler { get; private set; }
    public static EnemyType HoarderBug { get; private set; }
    public static EnemyType SandSpider { get; private set; }
    public static EnemyType Blob { get; private set; }
    public static EnemyType ForestGiant { get; private set; }
    public static EnemyType DressGirl { get; private set; }
    public static EnemyType SpringMan { get; private set; }
    public static EnemyType SandWorm { get; private set; }
    public static EnemyType Jester { get; private set; }
    public static EnemyType Puffer { get; private set; }
    public static EnemyType Doublewing { get; private set; }
    public static EnemyType DocileLocustBees { get; private set; }
    public static EnemyType RedLocustBees { get; private set; }
    public static EnemyType BaboonHawk { get; private set; }
    public static EnemyType Nutcracker { get; private set; }
    public static EnemyType MaskedPlayerEnemy { get; private set; }
    public static EnemyType RadMech { get; private set; }
    public static EnemyType Butler { get; private set; }
    public static EnemyType ButlerBees { get; private set; }
    public static EnemyType FlowerSnake { get; private set; }
    public static EnemyType BushWolf { get; private set; }
    public static EnemyType ClaySurgeon { get; private set; }
    public static EnemyType CaveDweller { get; private set; }

    internal static void Init()
    {
        List<string> unknownTypes = [];
        BiodiversityPlugin.LogVerbose("Getting all vanilla enemy types.");

        for (int i = 0; i < NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs.Count; i++)
        {
            NetworkPrefab networkPrefab = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs[i];
            if (!networkPrefab.Prefab.TryGetComponent(out EnemyAI enemyAI)) continue;
            BiodiversityPlugin.LogVerbose($"Found enemy: {enemyAI.enemyType.name}");

            PropertyInfo property = typeof(VanillaEnemies).GetProperty(enemyAI.enemyType.name);
            if (property == null) unknownTypes.Add(enemyAI.enemyType.name);
            else property.SetValue(null, enemyAI.enemyType);
        }

        BiodiversityPlugin.LogVerbose($"Unknown enemy types: {string.Join(", ", unknownTypes)}");
    }
}