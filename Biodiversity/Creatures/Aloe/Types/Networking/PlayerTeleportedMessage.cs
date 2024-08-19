using Unity.Netcode;

namespace Biodiversity.Creatures.Aloe.Types.Networking;

public struct PlayerTeleportedMessage : INetworkSerializable
{
    public string AloeId;
    public ulong PlayerId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref AloeId);
        serializer.SerializeValue(ref PlayerId);
    }
}