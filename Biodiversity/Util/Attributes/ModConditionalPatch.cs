using HarmonyLib;
using System;

namespace Biodiversity.Util.Attributes;

/// <summary>
/// Specifies a conditional Harmony patch that is applied only if a specific mod is installed.
/// This attribute is used to mark classes or methods as conditional patches for a target class or method
/// in an external mod.
/// </summary>
/// <remarks>
/// The patch will only be applied if the mod specified in the <see cref="TargetClassName"/> is loaded.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ModConditionalPatch : Attribute
{
    /// <summary>
    /// Gets the fully qualified name of the target class in the external mod.
    /// </summary>
    public string TargetClassName { get; }
    
    /// <summary>
    /// Gets the name of the method in the target class that will be patched.
    /// </summary>
    public string TargetMethodName { get; }
    
    /// <summary>
    /// Indicates whether the target method is static.
    /// </summary>
    public bool IsStaticMethod { get; }
    
    /// <summary>
    /// Gets the name of the local method in the patch class that will be used for the patch (e.g., prefix or postfix).
    /// </summary>
    public string LocalPatchMethodName { get; }
    
    /// <summary>
    /// Gets the type of the patch (e.g., Prefix, Postfix, etc.).
    /// </summary>
    public HarmonyPatchType PatchType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModConditionalPatch"/> class.
    /// </summary>
    /// <param name="targetClassName">The fully qualified name of the target class in the external mod.</param>
    /// <param name="targetMethodName">The name of the method in the target class that will be patched.</param>
    /// <param name="isStaticMethod">Indicates whether the target method is static.</param>
    /// <param name="localPatchMethodName">The name of the local method in the patch class that will be used for the patch (e.g., prefix or postfix).</param>
    /// <param name="patchType">The type of the patch (e.g., Prefix, Postfix, Transpiler, Finalizer).</param>
    public ModConditionalPatch(
        string targetClassName, 
        string targetMethodName, 
        bool isStaticMethod,
        string localPatchMethodName,
        HarmonyPatchType patchType)
    {
        TargetClassName = targetClassName;
        TargetMethodName = targetMethodName;
        IsStaticMethod = isStaticMethod;
        LocalPatchMethodName = localPatchMethodName;
        PatchType = patchType;
    }
}