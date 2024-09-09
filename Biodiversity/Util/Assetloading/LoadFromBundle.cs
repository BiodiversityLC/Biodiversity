using System;
using System.Collections.Generic;
using System.Text;

namespace Biodiversity.Util.Assetloading;
[AttributeUsage(AttributeTargets.Field)]
internal class LoadFromBundleAttribute(string bundleFile) : Attribute {
    public string BundleFile { get; private set; } = bundleFile;
}
