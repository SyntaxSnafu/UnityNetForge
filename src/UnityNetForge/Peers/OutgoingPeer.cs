using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
namespace UnityNetForge
{
    public class OutgoingPeer : PeerBase
    {
        private readonly NetworkPacket _connectRequestPacket;

        public OutgoingPeer(UnityNetForgeManager manager, IPEndPoint remote, int id, byte connectionNumber,
            ArraySegment<byte> data)
            : base(manager, remote, id, DateTime.UtcNow.Ticks, connectionNumber)
        {
            var packet = NetConnectRequestPacket.Make(data, remote.Serialize(), ConnectTime, id);
            packet.ConnectionNumber = connectionNumber;
            ConnectionState = ConnectionState.Outgoing;
            _connectRequestPacket = packet;
        }

        internal async Task SendConnectionRequestAsync()
        {
            await UnityNetForgeManager.RawSendAsync(_connectRequestPacket, EndPoint);
        }

        internal bool ProcessConnectionAccepted(NetConnectAcceptPacket packet)
        {
            if (ConnectionState != ConnectionState.Outgoing)
                return false;

            //check connection id
            if (packet.ConnectionTime != ConnectTime)
            {
                LogDebug($"[NC] Invalid connectId: {packet.ConnectionTime} != our({ConnectTime})");
                return false;
            }

            //check connect num
            ConnectionNumber = packet.ConnectionNumber;
            RemoteId = packet.PeerId;

            Interlocked.Exchange(ref _timeSinceLastPacket, 0);
            ConnectionState = ConnectionState.Connected;
            return true;
        }

        internal override async Task<ConnectRequestResult> ProcessReconnectionRequestAsync(
            NetConnectRequestPacket connRequest)
        {
            switch (ConnectionState)
            {
                //P2P case
                case ConnectionState.Outgoing:
                    //fast check
                    if (connRequest.ConnectionTime < ConnectTime) return ConnectRequestResult.P2PLose;
                    //slow rare case check
                    if (connRequest.ConnectionTime == ConnectTime)
                    {
                        var remoteBytes = EndPoint.Serialize();
                        var localBytes = connRequest.TargetAddress;
                        for (var i = remoteBytes.Size - 1; i >= 0; i--)
                        {
                            var rb = remoteBytes[i];
                            if (rb == localBytes[i])
                                continue;
                            if (rb < localBytes[i])
                                return ConnectRequestResult.P2PLose;
                        }
                    }

                    break;

                case ConnectionState.Connected:
                    // Old connect request
                    if (connRequest.ConnectionTime == ConnectTime)
                        LogDebug($"Received connection request while in Connected state");
                    //just reply accept
                    //await UnityNetForgeManager.SendRaw(_connectAcceptPacket, EndPoint);
                    // New connect request
                    else if (connRequest.ConnectionTime > ConnectTime) return ConnectRequestResult.Reconnection;
                    break;

                case ConnectionState.Disconnected:
                case ConnectionState.ShutdownRequested:
                    if (connRequest.ConnectionTime >= ConnectTime)
                        return ConnectRequestResult.NewConnection;
                    break;
            }

            return ConnectRequestResult.None;
        }
    }
}