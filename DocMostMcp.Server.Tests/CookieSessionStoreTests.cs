using System.Net;
using DocMostMcp.Server.Client;
using FluentAssertions;
using Xunit;

namespace DocMostMcp.Server.Tests;

public class CookieSessionStoreTests
{
    [Fact]
    public async Task EnsureFreshSessionAsync_SuccessfulLogin_ReturnsCookie()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(async request =>
        {
            request.RequestUri!.AbsolutePath.Should().Be("/api/auth/login");

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Set-Cookie", "docmost.sid=abc123; Path=/; HttpOnly");
            response.Content = new StringContent(
                """{"success":true,"status":200,"data":null}""");
            return response;
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3000") };
        var store = new CookieSessionStore(httpClient);

        // Act
        var cookie = await store.EnsureFreshSessionAsync("user@test.com", "pass");

        // Assert
        cookie.Should().NotBeNull();
        cookie.Name.Should().Be("docmost.sid");
        cookie.Value.Should().Be("abc123");
        store.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureFreshSessionAsync_InvalidCredentials_ThrowsAndMarksTerminal()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    """{"statusCode":401,"message":"Invalid email or password","error":"Unauthorized"}""")
            });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3000") };
        var store = new CookieSessionStore(httpClient);

        // Act
        var act = () => store.EnsureFreshSessionAsync("bad@user.com", "wrong");

        // Assert
        await act.Should().ThrowAsync<DocmostAuthException>();
        store.IsAuthenticated.Should().BeFalse();

        // Second call should also fail without hitting the network (terminal state)
        var act2 = () => store.EnsureFreshSessionAsync("bad@user.com", "wrong");
        await act2.Should().ThrowAsync<DocmostAuthException>();
    }

    [Fact]
    public async Task EnsureFreshSessionAsync_ConcurrentLogins_OnlyOneHttpCall()
    {
        // Arrange
        var callCount = 0;
        var handler = new MockHttpMessageHandler(async request =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(100); // Simulate network latency

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Set-Cookie", "docmost.sid=abc123; Path=/; HttpOnly");
            response.Content = new StringContent(
                """{"success":true,"status":200,"data":null}""");
            return response;
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3000") };
        var store = new CookieSessionStore(httpClient);

        // Act: fire 5 concurrent login attempts
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => store.EnsureFreshSessionAsync("user@test.com", "pass"))
            .ToArray();

        await Task.WhenAll(tasks);

        // Assert: only one HTTP call should have been made
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task EnsureFreshSessionAsync_AfterSuccessfulLogin_UsesCachedSession()
    {
        // Arrange
        var callCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref callCount);
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Set-Cookie", "docmost.sid=abc123; Path=/; HttpOnly");
            response.Content = new StringContent(
                """{"success":true,"status":200,"data":null}""");
            return response;
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:3000") };
        var store = new CookieSessionStore(httpClient);

        // Act
        await store.EnsureFreshSessionAsync("user@test.com", "pass");
        await store.EnsureFreshSessionAsync("user@test.com", "pass");
        await store.EnsureFreshSessionAsync("user@test.com", "pass");

        // Assert: only one HTTP call
        callCount.Should().Be(1);
    }
}

/// <summary>
/// Helper that implements <see cref="HttpMessageHandler"/> with a configurable callback.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = req => Task.FromResult(handler(req));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _handler(request);
    }
}
