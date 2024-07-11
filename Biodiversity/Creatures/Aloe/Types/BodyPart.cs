using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Types;

[System.Serializable]
public class BodyPart
{
    public Rigidbody limbRigidbody;
    public Collider limbCollider;
    public Transform attachedTo;
    public bool active;
    public string name;
}