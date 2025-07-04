using Biodiversity.Core.Attributes;
using Biodiversity.Patches;
using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Biodiversity.Core.AssetLoading;

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
        AssetBundle bundle = BiodiversityPlugin.LoadBundle(filePath);
        if (bundle == null)
        {
            BiodiversityPlugin.Logger.LogError($"AssetBundle '{filePath}' failed to load. Cannot initialize {typeof(T).Name}.");
            return;
        }
        
        Type assetBundleType = typeof(T);
        for (int i = 0; i < assetBundleType.GetFields().Length; i++)
        {
            FieldInfo assetBundleFields = assetBundleType.GetFields()[i];
            LoadFromBundleAttribute loadInstruction =
                (LoadFromBundleAttribute)assetBundleFields.GetCustomAttribute(typeof(LoadFromBundleAttribute));
            
            if (loadInstruction == null) continue;
            
            try
            {
                assetBundleFields.SetValue(this, LoadAsset(bundle, loadInstruction.BundleFile));
            }
            catch (ArgumentException e)
            {
                BiodiversityPlugin.Logger.LogError($"Failed to load asset '{loadInstruction.BundleFile}' from bundle '{filePath}': {e.Message}");
            }
        }
        
        Object[] assets = bundle.LoadAllAssets();
        for (int i = 0; i < assets.Length; i++)
        {
            Object asset = assets[i];

            switch (asset)
            {
                case GameObject gameObject:
                {
                    if (gameObject.GetComponent<NetworkObject>() && GameNetworkManagerPatch.NetworkPrefabsToRegister.Add(gameObject))
                    {
                        BiodiversityPlugin.LogVerbose($"Added NetworkPrefab '{gameObject.name}' from bundle '{filePath}' to registration queue.");
                    }

                    break;
                }

                case AudioClip { preloadAudioData: false } clip:
                {
                    BiodiversityPlugin.Logger.LogWarning($"Loading Audio data for '{clip.name}' because it does not have preloadAudioData enabled!");
                    clip.LoadAudioData();
                    break;
                }

                case VideoClip videoClip:
                {
                    BiodiversityPlugin.Logger.LogWarning($"VideoClip: '{videoClip.name}' is being loaded from '{typeof(T).Name}' instead of the dedicated video clip bundle. It will not work correctly.");
                    break;
                }
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
    private static Object LoadAsset(AssetBundle bundle, string path) 
    {
        Object result = bundle.LoadAsset<Object>(path);
        if (result == null) throw new ArgumentException(path + " is not valid in the assetbundle!");
        
        return result;
    }
}