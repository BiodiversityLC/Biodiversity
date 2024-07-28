using Biodiversity.Creatures.HoneyFeeder;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Biodiversity.Creatures;
[CustomEditor(typeof(HoneyFeederAI))]
public static class HoneyFeederAIEditor {
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawGizmos(HoneyFeederAI ai, GizmoType gizmoType) {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ai.transform.position, ai.Config.SightDistance);

        if(ai.State == HoneyFeederAI.AIStates.ATTACKING_BACKINGUP) {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(ai.targetPlayer.transform.position, ai.Config.MinBackupAmount);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(ai.targetPlayer.transform.position, ai.Config.MaxBackupAmount);
        }
    }
}
