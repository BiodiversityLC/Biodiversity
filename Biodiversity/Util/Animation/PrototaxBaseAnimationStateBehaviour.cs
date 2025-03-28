using Biodiversity.Creatures.Critters.Prototax;

namespace Biodiversity.Util.Animation;

public class PrototaxBaseAnimationStateBehaviour : BaseAnimationStateBehaviour
{
    protected PrototaxAI PrototaxAiInstance;

    internal void Initialize(PrototaxAI prototaxAiInstance)
    {
        PrototaxAiInstance = prototaxAiInstance;
    }
}