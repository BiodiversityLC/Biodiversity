using Biodiversity.Util.Animation;
using System.Diagnostics.CodeAnalysis;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public class AloeStateMachineBehaviour : BaseAnimationStateBehaviour
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