using UnityEngine;

namespace Biodiversity.Creatures.Aloe.Types;

[System.Serializable]
public class BrackenRoomAloeNode(Vector3 nodePosition, bool taken = false)
{
    public Vector3 nodePosition = nodePosition;
    public bool taken = taken;
}