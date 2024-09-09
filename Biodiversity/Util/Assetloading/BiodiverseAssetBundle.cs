using Biodiversity.Patches;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Video;

namespace Biodiversity.Util.Assetloading;
internal abstract class BiodiverseAssetBundle<T> where T : BiodiverseAssetBundle<T> {

    public BiodiverseAssetBundle(string filePath) {
        AssetBundle bundle = BiodiversityPlugin.Instance.LoadBundle(filePath);

        Type type = typeof(T);
        foreach(FieldInfo field in type.GetFields()) {
            LoadFromBundleAttribute loadInstruction = (LoadFromBundleAttribute)field.GetCustomAttribute(typeof(LoadFromBundleAttribute));
            if(loadInstruction == null) continue;

            field.SetValue(this, LoadAsset(bundle, loadInstruction.BundleFile));
        }

        foreach (UnityEngine.Object asset in bundle.LoadAllAssets()) {
            if (asset is GameObject gameObject) {
                if(gameObject.GetComponent<NetworkObject>() == null) continue;
                if(GameNetworkManagerPatch.networkPrefabsToRegister.Contains(gameObject)) continue;
                GameNetworkManagerPatch.networkPrefabsToRegister.Add(gameObject);
            }

            if (asset is AudioClip clip && !clip.preloadAudioData) {
                BiodiversityPlugin.Logger.LogWarning($"Loading Audio data for '{clip.name}' because it does not have preloadAudioData enabled!");
                clip.LoadAudioData();
            }

            if (asset is VideoClip videoClip) {
                BiodiversityPlugin.Logger.LogError($"VideoClip: '{videoClip.name}' is being loaded from '{typeof(T).Name}' instead of the dedicated video clip bundle. It will not work correctly.");
            }
        }

        bundle.Unload(false);
    }

    UnityEngine.Object LoadAsset(AssetBundle bundle, string path) {
        UnityEngine.Object result = bundle.LoadAsset<UnityEngine.Object>(path);
        if(result == null) throw new ArgumentException(path + " is not valid in the assetbundle!");

        return result;
    }
}
