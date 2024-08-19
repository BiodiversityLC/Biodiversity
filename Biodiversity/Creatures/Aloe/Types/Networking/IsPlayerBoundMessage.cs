using Unity.Netcode;

namespace Biodiversity.Creatures.Aloe.Types.Networking;

public struct IsPlayerBoundMessage : INetworkSerializable
{
    public ulong PlayerId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerId);
    }
}