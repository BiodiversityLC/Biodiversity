using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Biodiversity.Creatures.Aloe.Types;
using GameNetcodeStuff;
using UnityEngine;

namespace Biodiversity.Creatures.Aloe;

[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Global")]
internal class AloeSharedData
{
    private static AloeSharedData _instance;
    public static AloeSharedData Instance => _instance ??= new AloeSharedData();

    public Dictionary<AloeServer, PlayerControllerB> AloeBoundKidnaps { get; } = new();
    public Dictionary<PlayerControllerB, int> PlayersMaxHealth { get; } = new();

    private List<BrackenRoomAloeNode> BrackenRoomAloeNodes { get; } = [];

    public Vector3 BrackenRoomDoorPosition { get; set; } = Vector3.zero;
    
    public void PopulateBrackenRoomAloeNodes(Transform brackenRoomTransform)
    {
        Vector3 node1 = brackenRoomTransform.position + new Vector3(0, 0f, -13.92f);
        Vector3 node2 = brackenRoomTransform.position + new Vector3(-5.3f, 0f, -11f);
        Vector3 node3 = brackenRoomTransform.position + new Vector3(-4.8f, 0f, -3.5f);
        Vector3 node4 = brackenRoomTransform.position + new Vector3(-0.2f, 0f, -1.09f);
        
        

        List<Vector3> nodes = [node1, node2, node3, node4];
        nodes.ForEach(node =>
        {
            Instance.BrackenRoomAloeNodes.Add(new BrackenRoomAloeNode(node));
        });
    }

    public Vector3 OccupyBrackenRoomAloeNode()
    {
        foreach (BrackenRoomAloeNode brackenRoomAloeNode in 
                 Instance.BrackenRoomAloeNodes.Where(brackenRoomAloeNode => !brackenRoomAloeNode.taken))
        {
            brackenRoomAloeNode.taken = true;
            return brackenRoomAloeNode.nodePosition;
        }

        return Vector3.zero;
    }

    public void UnOccupyBrackenRoomAloeNode(Vector3 nodePosition)
    {
        foreach (BrackenRoomAloeNode brackenRoomAloeNode in
                 Instance.BrackenRoomAloeNodes.Where(brackenRoomAloeNode =>
                     brackenRoomAloeNode.nodePosition == nodePosition))
        {
            brackenRoomAloeNode.taken = false;
            return;
        }
    }
    
    public void FlushDictionaries()
    {
        Instance.BrackenRoomAloeNodes.Clear();
        Instance.AloeBoundKidnaps.Clear();
        Instance.PlayersMaxHealth.Clear();
    }
}