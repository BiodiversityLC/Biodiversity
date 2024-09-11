using Biodiversity.Patches;
using Biodiversity.Util.Attributes;
using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Video;

namespace Biodiversity.Util.Assetloading;

/// <summary>
/// Abstract class to handle loading assets from an asset bundle for Biodiversity.
/// This class is used as a generic base class where <typeparamref name="T"/> is a specific implementation of the asset bundle loader.
/// </summary>
/// <typeparam name="T">The derived class that inherits from <see cref="BiodiverseAssetBundle{T}"/>.</typeparam>
internal abstract class BiodiverseAssetBundle<T> where T : BiodiverseAssetBundle<T> 
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BiodiverseAssetBundle{T}"/> class and loads the specified asset bundle from the given file path.
    /// </summary>
    /// <param name="filePath">The file path of the asset bundle to load.</param>
    /// <exception cref="ArgumentException">Thrown if any asset specified in the <see cref="LoadFromBundleAttribute"/> is not found in the bundle.</exception>
    protected BiodiverseAssetBundle(string filePath) 
    {
        AssetBundle bundle = BiodiversityPlugin.Instance.LoadBundle(filePath);

        Type type = typeof(T);
        foreach (FieldInfo field in type.GetFields()) 
        {
            LoadFromBundleAttribute loadInstruction = (LoadFromBundleAttribute)field.GetCustomAttribute(typeof(LoadFromBundleAttribute));
            if (loadInstruction == null) continue;

            field.SetValue(this, LoadAsset(bundle, loadInstruction.BundleFile));
        }

        foreach (UnityEngine.Object asset in bundle.LoadAllAssets()) 
        {
            if (asset is GameObject gameObject) 
            {
                if(gameObject.GetComponent<NetworkObject>() == null) continue;
                if(GameNetworkManagerPatch.NetworkPrefabsToRegister.Contains(gameObject)) continue;
                GameNetworkManagerPatch.NetworkPrefabsToRegister.Add(gameObject);
            }

            if (asset is AudioClip { preloadAudioData: false } clip) 
            {
                BiodiversityPlugin.Logger.LogWarning($"Loading Audio data for '{clip.name}' because it does not have preloadAudioData enabled!");
                clip.LoadAudioData();
            }

            if (asset is VideoClip videoClip) 
            {
                BiodiversityPlugin.Logger.LogError($"VideoClip: '{videoClip.name}' is being loaded from '{typeof(T).Name}' instead of the dedicated video clip bundle. It will not work correctly.");
            }
        }

        bundle.Unload(false);
    }

    /// <summary>
    /// Loads a specific asset from the asset bundle based on the provided path.
    /// </summary>
    /// <param name="bundle">The asset bundle from which the asset will be loaded.</param>
    /// <param name="path">The path of the asset to load within the asset bundle.</param>
    /// <returns>The loaded asset as a <see cref="UnityEngine.Object"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the asset could not be found in the bundle.</exception>
    private static UnityEngine.Object LoadAsset(AssetBundle bundle, string path) 
    {
        UnityEngine.Object result = bundle.LoadAsset<UnityEngine.Object>(path);
        if (result == null) throw new ArgumentException(path + " is not valid in the assetbundle!");

        return result;
    }
}
