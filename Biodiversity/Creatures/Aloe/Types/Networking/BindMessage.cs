using Unity.Netcode;

namespace Biodiversity.Creatures.Aloe.Types.Networking;

public struct BindMessage : INetworkSerializable
{
    public string AloeId;
    public ulong PlayerId;
    public BindType BindType;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref AloeId);
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref BindType);
    }
}