using UnityEngine;

namespace Biodiversity.Creatures.Aloe.DevTools;

public class LogicalTransformParent : MonoBehaviour
{
    public Transform logicalParent;

    private void LateUpdate()
    {
        if (logicalParent == null) return;
        transform.position = logicalParent.transform.position;
        transform.rotation = logicalParent.transform.rotation;
    }
}