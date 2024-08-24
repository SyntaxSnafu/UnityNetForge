using Microsoft.AspNetCore.Mvc;
using UnityNetForge.Relay.Server.Models.Sessions;
using UnityNetForge.Relay.Server.Relay;

namespace UnityNetForge.Relay.Server.Controllers;

[ApiController]
[Route("[controller]")]
public class SessionController : ControllerBase
{
    private readonly ILogger<SessionController> _logger;
    private readonly RelayServer _relay;

    public SessionController(ILogger<SessionController> logger, RelayServer server)
    {
        _logger = logger;
        _relay = server;
    }

    [HttpPut("Create")]
    public SessionInfo CreateSession()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        //var joinCode = new string(Enumerable.Repeat(chars, 6).Select(s => s[RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
        var joinCode = "TEST";
        _relay.CreateSession(joinCode);
        var sci = new SessionInfo
        {
            JoinCode = joinCode,
            RelayAddress = "aus628.relays.net.fireworkeyes.com",
            RelayPort = 15593
        };

        _logger.LogInformation("User {}:{} created session with join code {}",
            HttpContext.Connection.RemoteIpAddress,
            HttpContext.Connection.RemotePort,
            sci.JoinCode);

        return sci;
    }

    [HttpPut("Join")]
    public ActionResult<SessionInfo> JoinSession([FromBody] SessionRequestJoinInfo joinCode)
    {
        var session = _relay.GetSession(joinCode.JoinCode);
        if (session == null) return NotFound();
        return new SessionInfo
        {
            JoinCode = session.JoinCode,
            RelayAddress = "aus628.relays.net.fireworkeyes.com",
            RelayPort = 15593
        };
    }

    public struct SessionRequestJoinInfo
    {
        public string JoinCode { get; set; }
    }
}