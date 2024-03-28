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
        Gizmos.DrawWireSphere(ai.transform.position, ai.Config.HiveDetectionDistance);
    }
}
