using Unity.Netcode;

namespace Biodiversity.Creatures.Aloe.Types.Networking;

public struct UnbindMessage : INetworkSerializable
{
    public string BioId;
    public BindType BindType;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref BioId);
        serializer.SerializeValue(ref BindType);
    }
}