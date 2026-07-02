using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Cadmus.Bricks.Api.Controllers;

/// <summary>
/// Generic proxy controller, used for some lookup services like DBPedia.
/// </summary>
/// <seealso cref="ControllerBase" />
[ApiController]
[Route("api/proxy")]
public sealed class ProxyController(HttpClient httpClient) : ControllerBase
{
    private readonly HttpClient _httpClient = httpClient;

    // headers that should not be forwarded from client to target
    private static readonly HashSet<string> RestrictedHeaders =
        new(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Connection", "Transfer-Encoding", "Upgrade", "Proxy-Connection",
        "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailers"
    };

    /// <summary>
    /// Attempts to determine if content is JSON by examining its structure.
    /// </summary>
    /// <param name="content">The content to examine.</param>
    /// <returns>True if the content appears to be JSON.</returns>
    private static bool IsJsonContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        content = content.Trim();

        // basic JSON structure detection
        return (content.StartsWith('{') && content.EndsWith('}')) ||
               (content.StartsWith('[') && content.EndsWith(']'));
    }

    /// <summary>
    /// Attempts to add a header to the request, handling both request headers
    /// and content headers.
    /// </summary>
    /// <param name="request">The HTTP request message.</param>
    /// <param name="name">The header name.</param>
    /// <param name="values">The header values.</param>
    /// <returns>True if the header was successfully added.</returns>
    private static bool TryAddHeader(HttpRequestMessage request, string name,
        StringValues values)
    {
        try
        {
            // try to add as request header first
            if (request.Headers.TryAddWithoutValidation(name,
                values.AsEnumerable()))
            {
                return true;
            }

            // if that fails, try as content header (if there's content)
            if (request.Content != null &&
                request.Content.Headers.TryAddWithoutValidation(name,
                values.AsEnumerable()))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines the appropriate content type for the response.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="requestedAccept">The Accept header from the request.</param>
    /// <param name="content">The response content.</param>
    /// <returns>The determined content type.</returns>
    private static string DetermineContentType(HttpResponseMessage response,
        string requestedAccept, string content)
    {
        // first, check the response's Content-Type header
        if (response.Content.Headers.ContentType?.MediaType != null)
        {
            string responseContentType =
                response.Content.Headers.ContentType.MediaType;

            // if the response says it's JSON, trust it
            if (responseContentType.Contains("json",
                StringComparison.OrdinalIgnoreCase))
            {
                return "application/json";
            }

            // if response says HTML but we requested JSON, try to detect
            // actual content
            if (responseContentType.Contains("html",
                StringComparison.OrdinalIgnoreCase) &&
                requestedAccept.Contains("json",
                StringComparison.OrdinalIgnoreCase))
            {
                // check if content looks like JSON despite HTML content-type
                if (IsJsonContent(content))
                {
                    return "application/json";
                }
            }

            return responseContentType;
        }

        // no content type from response, try to detect from content
        if (IsJsonContent(content))
        {
            return "application/json";
        }

        // if we requested JSON but got something else, still try to return
        // as JSON
        if (requestedAccept.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return "application/json";
        }

        // default fallback
        return "text/plain";
    }

    /// <summary>
    /// Gets the response from the specified URI with optional custom headers.
    /// </summary>
    /// <param name="uri">The URI, e.g.
    /// <c>https://lookup.dbpedia.org/api/search?query=plato&format=json&maxResults=10</c>
    /// or <c>https://viaf.org/viaf/AutoSuggest?query=a</c>.</param>
    /// <param name="accept">Optional Accept header value (defaults to
    /// application/json).</param>
    /// <param name="userAgent">Optional User-Agent header value.</param>
    /// <param name="contentType">Optional Content-Type header for the response 
    /// (overrides auto-detection).</param>
    /// <returns>Response with appropriate content type.</returns>
    [HttpGet]
    [ResponseCache(Duration = 60 * 10,
        VaryByQueryKeys = ["uri", "accept", "userAgent"], NoStore = false)]
    public async Task<IActionResult> Get(
        [FromQuery] string uri,
        [FromQuery] string? accept = null,
        [FromQuery] string? userAgent = null,
        [FromQuery] string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return BadRequest("URI parameter is required");

        try
        {
            // validate URI
            if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? targetUri))
                return BadRequest("Invalid URI format");

            // create request message
            using HttpRequestMessage request = new(HttpMethod.Get, targetUri);

            // set Accept header (default to JSON if not specified)
            string acceptHeader = accept ?? "application/json";
            request.Headers.Add("Accept", acceptHeader);

            // set User-Agent if provided
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                request.Headers.Add("User-Agent", userAgent);
            }
            else
            {
                // set a default user agent to avoid being blocked
                request.Headers.Add("User-Agent", "CadmusAPI/1.0 (Proxy)");
            }

            // forward non-restricted headers from the original request
            foreach (var header in Request.Headers)
            {
                if (!RestrictedHeaders.Contains(header.Key) &&
                    !request.Headers.Contains(header.Key) &&
                    header.Key != "Accept" && header.Key != "User-Agent")
                {
                    if (TryAddHeader(request, header.Key, header.Value))
                    {
                        // Header was successfully added
                    }
                }
            }

            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();

                // determine content type
                string responseContentType;
                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    // use explicitly provided content type
                    responseContentType = contentType;
                }
                else
                {
                    // auto-detect based on response or request
                    responseContentType = DetermineContentType(
                        response, acceptHeader, content);
                }

                // set response headers
                foreach (KeyValuePair<string, IEnumerable<string>> header
                    in response.Headers)
                {
                    if (!RestrictedHeaders.Contains(header.Key))
                    {
                        Response.Headers.TryAdd(header.Key,
                            new StringValues(header.Value.ToArray()));
                    }
                }

                return Content(content, responseContentType);
            }

            return StatusCode((int)response.StatusCode, response.ReasonPhrase);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"HTTP request exception: {ex}");
            return StatusCode(502, "Bad Gateway: " + ex.Message);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            Debug.WriteLine($"Request timeout: {ex}");
            return StatusCode(408, "Request Timeout");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Proxy error: {ex}");
            return StatusCode(500, "Internal Server Error: " + ex.Message);
        }
    }
}
