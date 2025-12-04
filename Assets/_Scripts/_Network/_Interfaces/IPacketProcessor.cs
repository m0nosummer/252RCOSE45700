using Arena.Core;

namespace Arena.Network
{
    /// <summary>
    /// Handles packet serialization, fragmentation, and compression.
    /// </summary>
    public interface IPacketProcessor
    {
        NetworkPacket[] CreatePackets(INetworkMessage message);
        INetworkMessage ProcessPacket(NetworkPacket packet);
    }
}