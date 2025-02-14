using Unity.Netcode;

namespace Biodiversity.Creatures.Aloe.Types.Networking;

public struct BindMessage : INetworkSerializable
{
    public string BioId;
    public ulong PlayerId;
    public BindType BindType;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref BioId);
        serializer.SerializeValue(ref PlayerId);
        serializer.SerializeValue(ref BindType);
    }
}