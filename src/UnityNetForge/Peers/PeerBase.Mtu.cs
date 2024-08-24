using System;
using System.Threading.Tasks;
namespace UnityNetForge
{
    public partial class PeerBase
    {
        /// <summary>
        ///     Handles all packets of type <see cref="PacketProperty.MtuOk" /> and <see cref="PacketProperty.MtuCheck" />.
        /// </summary>
        private async Task ProcessMtuPacketAsync(NetworkPacket packet)
        {
            // Ignore MTU values below our configured minimum.
            if (packet.Data.Count < NetConstants.PossibleMtu[0])
            {
                LogDebug($"[MTU] Peer requested an MTU ({packet.Data.Count}) lower than minimum ({NetConstants.PossibleMtu[0]}.");
                return;
            }
            // Validate the following:
            // 1. The size of the data packet (minus the header) is the same as the MTU they're requesting. This confirms that we CAN actually receive a packet of
            // the requested MTU.
            // 2. The front MTU check value (at the start of the data) is the same size as the size of the packet.
            // 3. The end MTU check value (4 bytes before the end of the packet) is the same as the front value. This confirms they're not sending garbage.
            // 4. The MTU they're requesting is below what we permit (NetConstants.MaxPacketSize)

            // So, to simplify,
            // ((Size of packet minus header) == (Front check value) == (Back check value)) <= NetConstants.MaxPacketSize

            var frontCheck = BitConverter.ToInt32(packet.Data.Array, packet.Data.Offset + 1);

            var backCheck = BitConverter.ToInt32(packet.Data.Array, packet.Data.Offset + packet.Data.Count - 4);

            if (frontCheck != packet.Data.Count)
            {
                LogDebug("[MTU] MTU negotiation resulted in a broken packet. " +
                                    $"Peer requested {frontCheck} while packet was only {packet.Data.Count} bytes.");
                return;
            }

            if (frontCheck != backCheck)
            {
                LogDebug(
                    $"[MTU] MTU negotiation resulted in a broken end MTU check. Received {backCheck}, expected to be same as packet size of {packet.Data.Count} bytes.");
                return;
            }

            if (frontCheck > NetConstants.MaxPacketSize)
            {
                LogDebug(
                    $"[MTU] Peer requested an MTU ({frontCheck}) higher than permitted by policy ({NetConstants.MaxPacketSize}).");
                return;
            }

            switch (packet.Property)
            {
                case PacketProperty.MtuCheck:
                    _mtuCheckAttempts = 0;
                    packet.Property = PacketProperty.MtuOk;
                    await UnityNetForgeManager.RawSendAsync(packet, EndPoint);
                    break;
                case PacketProperty.MtuOk when frontCheck > MaximumTransferUnit && !_mtuNegotiationComplete:
                {
                    // Validate the packet.
                    if (frontCheck != NetConstants.PossibleMtu[_currentMtuIndex + 1] -
                        UnityNetForgeManager.ExtraPacketSizeForLayer)
                        return;

                    await _mtuMutex.WaitAsync();
                    try
                    {
                        SetMtu(_currentMtuIndex + 1);
                    }
                    finally
                    {
                        _mtuMutex.Release();
                    }

                    // If we've reached the maximum possible MTU permitted by policy, then end negotiations.
                    if (_currentMtuIndex == NetConstants.PossibleMtu.Length - 1)
                    {
                        _mtuNegotiationComplete = true;
                        LogDebug($"[MTU] Negotiation complete. MTU for this session: {MaximumTransferUnit}.");
                    }

                    break;
                }
            }
        }

        /// <summary>
        ///     Attempts to negotiate the MTU, if negotiations are not complete.
        /// </summary>
        /// <param name="deltaTime">The change in time from the last MTU negotiation request.</param>
        private async Task CheckMtuAsync(long deltaTime)
        {
            if (_mtuNegotiationComplete)
                return;

            _mtuCheckTimer += deltaTime;
            if (_mtuCheckTimer < MtuCheckDelay)
                return;

            _mtuCheckTimer = 0;
            _mtuCheckAttempts++;
            if (_mtuCheckAttempts >= MaxMtuCheckAttempts)
            {
                _mtuNegotiationComplete = true;
                return;
            }

            await _mtuMutex.WaitAsync();
            try
            {
                if (_currentMtuIndex >= NetConstants.PossibleMtu.Length - 1)
                    return;

                var newMtu = NetConstants.PossibleMtu[_currentMtuIndex + 1] - UnityNetForgeManager.ExtraPacketSizeForLayer;

                // The new MTU packet must be the EXACT size of the requested MTU, so we
                // subtract 1 to account for the header
                var p = NetworkPacket.FromProperty(PacketProperty.MtuCheck, newMtu - 1);
                FastBitConverter.GetBytes(p.Data.Array, p.Data.Offset + 1, newMtu);
                FastBitConverter.GetBytes(p.Data.Array, p.Data.Offset + p.Data.Count - 4, newMtu);

                if (await UnityNetForgeManager.RawSendAsync(p, EndPoint) <= 0)
                    _mtuNegotiationComplete = true;
            }
            finally
            {
                _mtuMutex.Release();
            }
        }
    }
}