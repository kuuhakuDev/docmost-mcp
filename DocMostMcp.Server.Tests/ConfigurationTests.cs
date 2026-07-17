using DocMostMcp.Server.Configuration;
using FluentAssertions;

namespace DocMostMcp.Server.Tests;

public class ConfigurationTests
{
    private static DocmostOptions ValidOptions() => new()
    {
        Url = new Uri("http://localhost:3000"),
        Email = "admin@example.com",
        Password = "secret123",
        Port = 3001,
        Transport = TransportMode.Stdio,
        TransportExplicitlySet = true,
    };

    [Fact]
    public void Validate_AllValid_ReturnsSuccess()
    {
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, ValidOptions());
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingUrl_ReturnsFailure()
    {
        var options = ValidOptions();
        options.Url = null;
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("DOCMOST_URL"));
    }

    [Fact]
    public void Validate_InvalidUrl_ReturnsFailure()
    {
        var options = ValidOptions();
        options.Url = new Uri("/relative", UriKind.Relative);
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("DOCMOST_URL"));
    }

    [Fact]
    public void Validate_MissingEmail_ReturnsFailure()
    {
        var options = ValidOptions();
        options.Email = null;
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("DOCMOST_EMAIL"));
    }

    [Fact]
    public void Validate_EmptyEmail_ReturnsFailure()
    {
        var options = ValidOptions();
        options.Email = "";
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingPassword_ReturnsFailure()
    {
        var options = ValidOptions();
        options.Password = null;
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("DOCMOST_PASSWORD"));
    }

    [Fact]
    public void Validate_PortOutOfRange_ReturnsFailure()
    {
        var options = ValidOptions();
        options.Port = 0;
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("DOCMOST_MCP_PORT"));
    }

    [Fact]
    public void Validate_PortTooHigh_ReturnsFailure()
    {
        var options = ValidOptions();
        options.Port = 65536;
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("DOCMOST_MCP_PORT"));
    }

    [Fact]
    public void Validate_InvalidTransport_ReturnsFailure()
    {
        var options = ValidOptions();
        options.Transport = (TransportMode)999;
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain(f => f.Contains("DOCMOST_MCP_TRANSPORT"));
    }

    [Fact]
    public void Validate_AutoDetectedTransport_SkipsValidation()
    {
        var options = ValidOptions();
        options.Transport = (TransportMode)999;
        options.TransportExplicitlySet = false;
        var validator = new DocmostOptionsValidator();
        var result = validator.Validate(null, options);
        // Transport validation only applies when explicitly set
        result.Failures.Should().NotContain(f => f.Contains("DOCMOST_MCP_TRANSPORT"));
    }
}
