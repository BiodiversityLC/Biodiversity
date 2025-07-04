using HarmonyLib;
using System;

namespace Biodiversity.Core.Attributes;

/// <summary>
/// Specifies a conditional Harmony patch that is applied only if a specific mod is installed.
/// This attribute is used to mark classes or methods as conditional patches for a target class or method in an external mod.
/// </summary>
/// <remarks>
/// <para>
/// The patch will only be applied if the mod specified in the <see cref="AssemblyName"/> is loaded.
/// Note that you cannot specify parameters when targeting methods, so this attribute will not work with overloaded methods.
/// Overloading refers to methods that have the same name but different parameters.
/// </para>
/// <para>
/// If you are having problems with getting the exact <see cref="AssemblyName"/>, <see cref="TargetClassName"/>, <see cref="TargetMethodName"/>,
/// or are having problems in general, then turn on verbose logging in the <c>Lethal Company\BepInEx\config\com.github.biodiversitylc.Biodiversity.cfg</c> config file.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class ModConditionalPatch : Attribute
{
    /// <summary>
    /// The name of the assembly of the mod where the target class resides.
    /// The patch will only be applied if this assembly is loaded.
    /// </summary>
    public string AssemblyName { get; }
    
    /// <summary>
    /// The name of the method in the target class that will be patched.
    /// Note that this does not support method overloading, so if there are multiple methods with the same name
    /// and different parameters, the patch may not work as expected.
    /// </summary>
    public string TargetClassName { get; }
    
    /// <summary>
    /// The name of the method in the target class that will be patched.
    /// </summary>
    public string TargetMethodName { get; }
    
    /// <summary>
    /// The name of the local method in the patch class that will be used for the patch (e.g., prefix or postfix).
    /// </summary>
    public string LocalPatchMethodName { get; }
    
    /// <summary>
    /// The type of the patch (e.g., Prefix, Postfix, etc.).
    /// </summary>
    public HarmonyPatchType PatchType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ModConditionalPatch"/> class.
    /// </summary>
    /// <param name="assemblyName">The name of the assembly where the target class resides. The patch will only be applied if this assembly is loaded.</param>
    /// <param name="targetClassName">The fully qualified name of the target class in the external mod.</param>
    /// <param name="targetMethodName">The name of the method in the target class that will be patched.
    /// This does not support overloaded methods (i.e., methods with the same name but different parameter types).</param>
    /// <param name="localPatchMethodName">The name of the local method in the patch class that will be used for the patch (e.g., prefix or postfix).</param>
    /// <param name="patchType">The type of the patch (e.g., Prefix, Postfix, Transpiler, Finalizer).</param>
    public ModConditionalPatch(
        string assemblyName,
        string targetClassName, 
        string targetMethodName, 
        string localPatchMethodName,
        HarmonyPatchType patchType)
    {
        AssemblyName = assemblyName;
        TargetClassName = targetClassName;
        TargetMethodName = targetMethodName;
        LocalPatchMethodName = localPatchMethodName;
        PatchType = patchType;
    }
}