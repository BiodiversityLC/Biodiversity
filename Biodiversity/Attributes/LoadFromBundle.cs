using System;
using Biodiversity.Util.Assetloading;

namespace Biodiversity.Util.Attributes;

/// <summary>
/// Marks a field to be automatically populated with an asset loaded from an asset bundle.
/// This attribute is used to specify the asset file to load from the bundle, based on the provided asset path.
/// </summary>
/// <remarks>
/// This attribute is applied to fields within classes that inherit from <see cref="BiodiverseAssetBundle{T}"/>.
/// The asset specified in the <see cref="BundleFile"/> path will be loaded into the field during the bundle loading process.
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
internal class LoadFromBundleAttribute : Attribute 
{
    /// <summary>
    /// The file path of the asset within the asset bundle that should be loaded into the field.
    /// </summary>
    public string BundleFile { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadFromBundleAttribute"/> class, specifying the asset path in the bundle.
    /// </summary>
    /// <param name="bundleFile">The file path of the asset within the asset bundle to be loaded into the field.</param>
    public LoadFromBundleAttribute(string bundleFile)
    {
        BundleFile = bundleFile;
    }
}
