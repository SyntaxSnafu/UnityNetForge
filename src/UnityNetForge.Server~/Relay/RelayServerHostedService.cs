using System.Net;

namespace UnityNetForge.Relay.Server.Relay;

public class RelayServerHostedService : BackgroundService
{
    private readonly ILogger<RelayServerHostedService> _logger;
    private readonly RelayServer _relayServer;

    public RelayServerHostedService(RelayServer relayServer, ILogger<RelayServerHostedService> logger)
    {
        _relayServer = relayServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _relayServer.UnityNetForgeManager.Ipv6Enabled = false;
        if (_relayServer.UnityNetForgeManager.Bind(IPAddress.Any, IPAddress.Any, 4098))
        {
            _logger.LogInformation("Listening on port 4098");
            await _relayServer.UnityNetForgeManager.ListenAsync(stoppingToken);
        }
        else
        {
            _logger.LogError("Failed to start relay server.");
            return;
        }

        await _relayServer.UnityNetForgeManager.StopAsync();
    }
}