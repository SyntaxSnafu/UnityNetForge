﻿using UnityNetForge.Relay.Protocol;
using UnityNetForge.Relay.Server.Relay.Sessions;

namespace UnityNetForge.Relay.Server.Relay;

public class RelayServer
{
    private readonly ILoggerFactory _factory;

    private readonly ILogger<RelayServer> _logger;

    private readonly Dictionary<string, RelaySession> _sessionsByCode = new();
    private readonly Dictionary<int, RelaySession> _sessionsByPeer = new();

    public RelayServer(ILogger<RelayServer> logger, ILoggerFactory factory)
    {
        _logger = logger;
        _factory = factory;
        UnityNetForgeManager = new UnityNetForgeManager();


        UnityNetForgeManager.OnReceive += OnNetworkReceive;
        UnityNetForgeManager.OnConnectionRequest += OnConnectionRequest;
        UnityNetForgeManager.OnPeerConnected += OnPeerConnected;
        UnityNetForgeManager.OnPeerDisconnected += OnPeerDisconnected;
    }

    public UnityNetForgeManager UnityNetForgeManager { get; }

    public Dictionary<string, RelaySession> GetAllSessions()
    {
        return _sessionsByCode;
    }

    public void CreateSession(string joinCode)
    {
        _sessionsByCode[joinCode] = new RelaySession(joinCode, this, _factory.CreateLogger<RelaySession>());
    }

    public RelaySession? GetSession(string joinCode)
    {
        return _sessionsByCode.GetValueOrDefault(joinCode);
    }

    public async Task DestroySession(RelaySession session)
    {
        foreach (var peer in session.Peers) _sessionsByPeer.Remove(peer.Id);

        await session.DisconnectAll();
        _sessionsByCode.Remove(session.JoinCode);
    }

    public async ValueTask OnNetworkReceive(PeerBase peer, CompositeReader reader, byte channelNumber,
        DeliveryMethod deliveryMethod)
    {
        var packet = reader.ReadRelayControlMessage();

        const string format = "Disconnecting {} ({}) because {}";
        if (!_sessionsByPeer.TryGetValue(peer.Id, out var session))
        {
            _logger.LogInformation(format, peer.Id, peer.EndPoint, "because they are not attached to a session.");
            await UnityNetForgeManager.DisconnectPeerAsync(peer);
            return;
        }

        await session.OnReceive(peer, packet, deliveryMethod);
    }

    public async ValueTask OnConnectionRequest(ConnectionRequest request)
    {
        var joinCode = request.Data.ReadString();

        if (!_sessionsByCode.TryGetValue(joinCode, out var keyedSession))
        {
            const string format = "Rejecting {} because {}";
            _logger.LogInformation(format, request.RemoteEndPoint,
                "because they requested to join a session that does not exist.");
            await request.RejectAsync(force: true);
            return;
        }

        var peer = await request.AcceptAsync();
        await keyedSession.OnJoinAsync(peer);
        _sessionsByPeer[peer.Id] = keyedSession;
    }

    public async ValueTask OnPeerConnected(PeerBase peer)
    {
        _logger.LogInformation($"Connected to {peer.EndPoint}");
    }

    public async ValueTask OnPeerDisconnected(PeerBase peer, DisconnectInfo disconnectInfo)
    {
        _logger.LogInformation(
            $"Peer {peer.Id} disconnected: {disconnectInfo.Reason} {disconnectInfo.SocketErrorCode}");
        if (_sessionsByPeer.TryGetValue(peer.Id, out var session))
        {
            await session.OnLeave(peer);
            _sessionsByPeer.Remove(peer.Id);
        }
    }
}