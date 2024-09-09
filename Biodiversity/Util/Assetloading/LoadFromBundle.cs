using System;

namespace Biodiversity.Util.Assetloading;
[AttributeUsage(AttributeTargets.Field)]
internal class LoadFromBundleAttribute(string bundleFile) : Attribute {
    public string BundleFile { get; private set; } = bundleFile;
}
