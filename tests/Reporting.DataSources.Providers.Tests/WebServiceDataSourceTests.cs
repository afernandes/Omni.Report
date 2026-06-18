using System.Net;
using FluentAssertions;
using Reporting.DataSources.WebService;
using Xunit;

namespace Reporting.DataSources.Providers.Tests;

/// <summary>
/// Tests for <see cref="WebServiceDataSource"/>. Uses a stub <see cref="HttpMessageHandler"/>
/// so the tests run offline and deterministic — no real HTTP traffic. Covers (a) JSON
/// response with auto content-type detection, (b) XML response, (c) the format-forcing
/// override, (d) custom headers (Authorization), and (e) POST with body content.
/// </summary>
public class WebServiceDataSourceTests
{
    [Fact]
    public async Task Json_response_auto_detected_via_content_type()
    {
        var client = StubClient("""[{"a":1},{"a":2}]""", "application/json");
        var ds = new WebServiceDataSource("Test",
            new WebServiceDataSourceOptions { Url = "https://example.com/api" }, client);
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
        rows[0]["a"].Should().Be(1);
    }

    [Fact]
    public async Task Xml_response_auto_detected_via_content_type()
    {
        var xml = "<rows><row><id>1</id></row><row><id>2</id></row></rows>";
        var client = StubClient(xml, "application/xml");
        var ds = new WebServiceDataSource("Test",
            new WebServiceDataSourceOptions
            {
                Url = "https://example.com/api",
                XmlRowsXPath = "/rows/row",
            }, client);
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().HaveCount(2);
        rows[0]["id"].Should().Be(1);
    }

    [Fact]
    public async Task Forced_format_overrides_content_type_sniffing()
    {
        // Server lies about content type (returns text/html) — caller forces JSON.
        var client = StubClient("""[{"k":"v"}]""", "text/html");
        var ds = new WebServiceDataSource("Test",
            new WebServiceDataSourceOptions
            {
                Url = "https://example.com/api",
                Format = WebServiceResponseFormat.Json,
            }, client);
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().ContainSingle();
        rows[0]["k"].Should().Be("v");
    }

    [Fact]
    public async Task Custom_headers_are_sent_with_the_request()
    {
        HttpRequestMessage? capturedRequest = null;
        var client = new HttpClient(new CapturingHandler("""[]""", "application/json", r => capturedRequest = r));
        var ds = new WebServiceDataSource("Test",
            new WebServiceDataSourceOptions
            {
                Url = "https://example.com/api",
                Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer abc" },
            }, client);
        _ = await ds.ReadAsync().ToListAsync();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization!.ToString().Should().Be("Bearer abc");
    }

    [Fact]
    public async Task Post_with_body_sends_method_and_payload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var client = new HttpClient(new CapturingHandler("""[]""", "application/json",
            async r =>
            {
                capturedRequest = r;
                if (r.Content is not null) capturedBody = await r.Content.ReadAsStringAsync();
            }));
        var ds = new WebServiceDataSource("Test",
            new WebServiceDataSourceOptions
            {
                Url = "https://example.com/api",
                Method = "POST",
                BodyContent = """{"query":"x"}""",
                BodyContentType = "application/json",
            }, client);
        _ = await ds.ReadAsync().ToListAsync();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedBody.Should().Be("""{"query":"x"}""");
    }

    [Fact]
    public async Task Json_root_path_resolves_nested_array_in_response()
    {
        var client = StubClient("""{"items":[{"x":7}]}""", "application/json");
        var ds = new WebServiceDataSource("Test",
            new WebServiceDataSourceOptions
            {
                Url = "https://example.com/api",
                JsonRootPath = "items",
            }, client);
        var rows = await ds.ReadAsync().ToListAsync();
        rows.Should().ContainSingle();
        rows[0]["x"].Should().Be(7);
    }

    [Fact]
    public async Task Non_success_status_throws()
    {
        var client = new HttpClient(new StubHandler("error", "text/plain", HttpStatusCode.InternalServerError));
        var ds = new WebServiceDataSource("Test",
            new WebServiceDataSourceOptions { Url = "https://example.com/api" }, client);
        var act = async () => await ds.ReadAsync().ToListAsync();
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ── Test helpers (deterministic HTTP) ────────────────────────────────────────

    private static HttpClient StubClient(string body, string contentType)
        => new(new StubHandler(body, contentType));

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly string _contentType;
        private readonly HttpStatusCode _status;

        public StubHandler(string body, string contentType, HttpStatusCode status = HttpStatusCode.OK)
        {
            _body = body;
            _contentType = contentType;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var resp = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, _contentType),
            };
            return Task.FromResult(resp);
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly string _contentType;
        private readonly Func<HttpRequestMessage, Task>? _captureAsync;
        private readonly Action<HttpRequestMessage>? _capture;

        public CapturingHandler(string body, string contentType, Action<HttpRequestMessage> capture)
        {
            _body = body;
            _contentType = contentType;
            _capture = capture;
        }

        public CapturingHandler(string body, string contentType, Func<HttpRequestMessage, Task> captureAsync)
        {
            _body = body;
            _contentType = contentType;
            _captureAsync = captureAsync;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_captureAsync is not null) await _captureAsync(request);
            else _capture?.Invoke(request);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, _contentType),
            };
        }
    }
}
