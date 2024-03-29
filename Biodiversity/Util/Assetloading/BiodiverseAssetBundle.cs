using Biodiversity.Patches;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Biodiversity.Util.Assetloading;
internal abstract class BiodiverseAssetBundle<T> where T : BiodiverseAssetBundle<T> {

    public BiodiverseAssetBundle(string filePath) {
        AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), filePath));

        Type type = typeof(T);
        foreach(FieldInfo field in type.GetFields()) {
            LoadFromBundleAttribute loadInstruction = (LoadFromBundleAttribute)field.GetCustomAttribute(typeof(LoadFromBundleAttribute));
            if(loadInstruction == null) continue;

            field.SetValue(this, LoadAsset(bundle, loadInstruction.BundleFile));
        }

        bundle.Unload(false);
    }

    UnityEngine.Object LoadAsset(AssetBundle bundle, string path) {
        UnityEngine.Object result = bundle.LoadAsset<UnityEngine.Object>(path);
        if(result == null) throw new ArgumentException(path + " is not valid in the assetbundle!");

        if(result is GameObject) {
            if((result as GameObject).GetComponent<NetworkObject>() != null) {
                GameNetworkManagerPatch.networkPrefabsToRegister.Add(result as GameObject);
            }
        }

        return result;
    }
}
