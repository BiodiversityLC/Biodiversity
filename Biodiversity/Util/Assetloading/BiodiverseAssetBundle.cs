﻿using Biodiversity.Patches;
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
        AssetBundle bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "assets", filePath));
        BiodiversityPlugin.Logger.LogDebug($"[AssetBundle Loading] {filePath} contains these objects: {string.Join(",", bundle.GetAllAssetNames())}");

        Type type = typeof(T);
        foreach(FieldInfo field in type.GetFields()) {
            LoadFromBundleAttribute loadInstruction = (LoadFromBundleAttribute)field.GetCustomAttribute(typeof(LoadFromBundleAttribute));
            if(loadInstruction == null) continue;

            field.SetValue(this, LoadAsset(bundle, loadInstruction.BundleFile));
        }

        foreach(GameObject gameObject in bundle.LoadAllAssets<GameObject>()) {
            if(gameObject.GetComponent<NetworkObject>() == null) continue;
            if(GameNetworkManagerPatch.networkPrefabsToRegister.Contains(gameObject)) continue;
            GameNetworkManagerPatch.networkPrefabsToRegister.Add(gameObject);
        }

        bundle.Unload(false);
    }

    UnityEngine.Object LoadAsset(AssetBundle bundle, string path) {
        UnityEngine.Object result = bundle.LoadAsset<UnityEngine.Object>(path);
        if(result == null) throw new ArgumentException(path + " is not valid in the assetbundle!");

        return result;
    }
}