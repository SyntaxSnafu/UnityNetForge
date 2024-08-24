﻿using System.IO;
using System.Net.Sockets;
namespace UnityNetForge
{
    /// <summary>
    ///     Additional information about disconnection
    /// </summary>
    public struct DisconnectInfo
    {
        /// <summary>
        ///     Additional info why peer disconnected
        /// </summary>
        public DisconnectReason Reason;

        /// <summary>
        ///     Error code (if reason is SocketSendError or SocketReceiveError)
        /// </summary>
        public SocketError SocketErrorCode;

        /// <summary>
        ///     Additional data that can be accessed (only if reason is RemoteConnectionClose)
        /// </summary>
        public BinaryReader? AdditionalData;

        public override string ToString()
        {
            return $"{Reason:G} ({SocketErrorCode:G})";
        }
    }
}