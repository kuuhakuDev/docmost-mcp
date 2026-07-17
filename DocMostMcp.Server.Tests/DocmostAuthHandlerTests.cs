using System.Net;
using System.Text.Json;
using DocMostMcp.Server.Client;
using DocMostMcp.Server.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DocMostMcp.Server.Tests;

public class DocmostAuthHandlerTests
{
    private static IOptions<DocmostOptions> CreateOptions() =>
        Options.Create(new DocmostOptions
        {
            Url = new Uri("http://localhost:3000"),
            Email = "admin@test.com",
            Password = "pass123",
        });

    /// <summary>
    /// Creates a handler pipeline where the inner handler is a mock, and
    /// the login client (used by CookieSessionStore) is a separate mock.
    /// </summary>
    private static (DocmostAuthHandler Handler, MockHttpMessageHandler InnerHandler) CreatePipeline(
        MockHttpMessageHandler loginHandler,
        MockHttpMessageHandler innerHandler)
    {
        var loginClient = new HttpClient(loginHandler) { BaseAddress = new Uri("http://localhost:3000") };
        var sessionStore = new CookieSessionStore(loginClient);
        var options = CreateOptions();
        var authHandler = new DocmostAuthHandler(sessionStore, options)
        {
            InnerHandler = innerHandler,
        };
        return (authHandler, innerHandler);
    }

    [Fact]
    public async Task SendAsync_SuccessfulRequest_PassesThrough()
    {
        // Arrange
        var loginHandler = new MockHttpMessageHandler(_ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Headers.Add("Set-Cookie", "docmost.sid=abc; Path=/; HttpOnly");
            resp.Content = new StringContent("""{"success":true,"status":200}""");
            return resp;
        });

        var innerHandler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":"ok"}"""),
            });

        var (handler, _) = CreatePipeline(loginHandler, innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:3000/api/spaces");
        var response = await invoker.SendAsync(request, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }

    [Fact]
    public async Task SendAsync_401ThenSuccess_RetriesOnce()
    {
        // Arrange
        var loginCallCount = 0;
        var loginHandler = new MockHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref loginCallCount);
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Headers.Add("Set-Cookie", "docmost.sid=abc; Path=/; HttpOnly");
            resp.Content = new StringContent("""{"success":true,"status":200}""");
            return resp;
        });

        var apiCallCount = 0;
        var innerHandler = new MockHttpMessageHandler(_ =>
        {
            var count = Interlocked.Increment(ref apiCallCount);
            // First call returns 401, second returns 200
            if (count == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("""{"statusCode":401,"error":"Unauthorized"}"""),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true,"status":200,"data":"ok"}"""),
            };
        });

        var (handler, _) = CreatePipeline(loginHandler, innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        // Act
        // First call - needs to trigger login, then 401, then re-login, then retry
        var request1 = new HttpRequestMessage(HttpMethod.Post, "http://localhost:3000/api/spaces");
        var response1 = await invoker.SendAsync(request1, CancellationToken.None);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        apiCallCount.Should().Be(2); // original + retry
    }

    [Fact]
    public async Task SendAsync_401Then401_ThrowsDocmostAuthException()
    {
        // Arrange
        var loginCallCount = 0;
        var loginHandler = new MockHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref loginCallCount);
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            resp.Headers.Add("Set-Cookie", "docmost.sid=abc; Path=/; HttpOnly");
            resp.Content = new StringContent("""{"success":true,"status":200}""");
            return resp;
        });

        var innerHandler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"statusCode":401,"error":"Unauthorized"}"""),
            });

        var (handler, _) = CreatePipeline(loginHandler, innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:3000/api/spaces");
        var act = () => invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var ex = await Record.ExceptionAsync(() => act());
        ex.Should().BeOfType<DocmostAuthException>();
    }

    [Fact]
    public async Task SendAsync_LoginInvalidCredentials_ThrowsDocmostAuthException()
    {
        // Arrange
        var loginHandler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    """{"statusCode":401,"message":"Invalid email or password","error":"Unauthorized"}"""),
            });

        var innerHandler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK));

        var (handler, _) = CreatePipeline(loginHandler, innerHandler);
        var invoker = new HttpMessageInvoker(handler);

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:3000/api/spaces");
        var act = () => invoker.SendAsync(request, CancellationToken.None);

        // Assert
        var ex = await Record.ExceptionAsync(() => act());
        ex.Should().BeOfType<DocmostAuthException>();
    }
}
