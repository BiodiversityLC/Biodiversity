﻿using UnityEngine;

namespace Biodiversity.Creatures.Aloe.AnimatorStateMachineBehaviours;

internal class CrushPlayerAnimationStateBehaviour : AloeStateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // todo: use the crush player neck bool in the server class
        BiodiversityPlugin.LogVerbose("Crush player animation complete.");
        // if (AloeServerInstance.IsServer) 
        
        // This code isnt implemented yet, and im not sure if it will be used
    }
}