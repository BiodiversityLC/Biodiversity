﻿using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

public class SpottedAnimationStateBehaviour : BaseStateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        LogDebug("Spotted animation complete.");
        if (AloeServerInstance.IsServer) NetcodeController.HasFinishedSpottedAnimation.Value = true;
    }
}