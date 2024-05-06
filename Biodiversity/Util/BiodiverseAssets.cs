using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Biodiversity.Util;
internal static class BiodiverseAssets {
    static AssetBundle MainAssetBundle;
    static AssetBundle OgopogoBundle;

    internal static EnemyType HoneyFeeder;
    internal static EnemyType Ogopogo;

    internal static TerminalNode OgopogoNode;
    internal static TerminalKeyword OgopogoKeyword;

    internal static void Init() {
        MainAssetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "biodiversity_honeyfeeder"));

        HoneyFeeder = LoadAsset<EnemyType>("HoneyFeeder.asset", MainAssetBundle);



        OgopogoBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "biodiversity_ogopogo"));

        Ogopogo = LoadAsset<EnemyType>("Ogopogo", OgopogoBundle);

        OgopogoNode = LoadAsset<TerminalNode>("OgopogoTN", OgopogoBundle);
        OgopogoKeyword = LoadAsset<TerminalKeyword>("OgopogoKW", OgopogoBundle);
    }

    static T LoadAsset<T>(string path, AssetBundle bundle) where T : UnityEngine.Object {
        T result = bundle.LoadAsset<T>(path);
        if(result == null) throw new ArgumentException(path + " is not valid in the assetbundle!");
        return result;
    }
}
