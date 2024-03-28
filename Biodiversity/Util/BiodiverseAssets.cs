using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Biodiversity.Util;
internal static class BiodiverseAssets {
    static AssetBundle MainAssetBundle;

    internal static EnemyType HoneyFeeder;

    internal static void Init() {
        MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "biodiversity_honeyfeeder"));

        HoneyFeeder = LoadAsset<EnemyType>("HoneyFeeder.asset");
    }

    static T LoadAsset<T>(string path) where T : UnityEngine.Object {
        T result = MainAssetBundle.LoadAsset<T>(path);
        if(result == null) throw new ArgumentException(path + " is not valid in the assetbundle!");
        return result;
    }
}
