using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
internal class BaseStateMachineBehaviour : StateMachineBehaviour
{
    protected AloeNetcodeController NetcodeController;
    protected AloeServerAI AloeServerAIInstance;
    protected AloeClient AloeClientInstance;

    internal void Initialize(
        AloeNetcodeController receivedNetcodeController,
        AloeServerAI receivedAloeServerAI,
        AloeClient receivedAloeClient)
    {
        NetcodeController = receivedNetcodeController;
        AloeServerAIInstance = receivedAloeServerAI;
        AloeClientInstance = receivedAloeClient;
    }
}