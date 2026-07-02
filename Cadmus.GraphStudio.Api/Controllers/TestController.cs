using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

namespace Cadmus.GraphStudio.Api.Controllers;

[ApiController]
public class TestController : ControllerBase
{
    private readonly ILogger<TestController> _logger;

    public TestController(ILogger<TestController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a diagnostic entry to the log.
    /// </summary>
    /// <returns>OK</returns>
    [HttpPost("api/test/log")]
    [ProducesResponseType(200)]
    public IActionResult AddLogEntry()
    {
        _logger.LogInformation("Diagnostic log entry posted at {Now} UTC " +
                               "from IP {IP}",
            DateTime.UtcNow,
            HttpContext.Connection.RemoteIpAddress);
        return Ok();
    }

    /// <summary>
    /// Raises an handled exception to test for logging.
    /// </summary>
    /// <exception cref="Exception">error</exception>
    [HttpGet("api/test/exception")]
    [ProducesResponseType(500)]
    public IActionResult RaiseError()
    {
        Exception exception = new("Fake exception raised for test purposes at "
            + DateTime.Now);
        _logger.LogError(exception, "Fake exception");
        throw exception;
    }
}
