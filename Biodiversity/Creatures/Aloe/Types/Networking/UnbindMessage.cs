using Unity.Netcode;

namespace Biodiversity.Creatures.Aloe.Types.Networking;

public struct UnbindMessage : INetworkSerializable
{
    public string AloeId;
    public BindType BindType;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref AloeId);
        serializer.SerializeValue(ref BindType);
    }
}