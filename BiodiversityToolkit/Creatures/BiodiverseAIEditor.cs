using Biodiversity.General;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BiodiversityToolkit.Creatures;
[CustomEditor(typeof(BiodiverseAI))]
public class BiodiverseAIEditor {
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
    static void DrawGizmos(BiodiverseAI ai, GizmoType gizmoType) {
        if(ai.searchCoroutine != null) {
            Gizmos.color = Color.yellow;
            foreach(GameObject unsearched in ai.currentSearch.unsearchedNodes) {
                Gizmos.DrawWireSphere(unsearched.transform.position, 1);
            }
        }
    }
}