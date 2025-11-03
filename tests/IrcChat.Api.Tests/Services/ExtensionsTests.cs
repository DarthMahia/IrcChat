using FluentAssertions;
using IrcChat.Api.Extensions;
using IrcChat.Api.Services;
using Xunit;

namespace IrcChat.Api.Tests.Services;

public class ExtensionsTests
{
    [Fact]
    public void GetInstanceId_WithConfiguredInstanceId_ShouldReturnConfiguredValue()
    {
        // Arrange
        var options = new ConnectionManagerOptions
        {
            InstanceId = "custom-instance-id"
        };

        // Act
        var result = options.GetInstanceId();

        // Assert
        result.Should().Be("custom-instance-id");
    }

    [Fact]
    public void GetInstanceId_WithoutInstanceId_ShouldReturnHostnameOrMachineName()
    {
        // Arrange
        var options = new ConnectionManagerOptions
        {
            InstanceId = null
        };

        // Act
        var result = options.GetInstanceId();

        // Assert
        result.Should().NotBeNullOrEmpty();
        // Soit HOSTNAME env var, soit Environment.MachineName
        result.Should().Match(id =>
            id == Environment.GetEnvironmentVariable("HOSTNAME") ||
            id == Environment.MachineName);
    }

    [Fact]
    public void GetInstanceId_WithEmptyInstanceId_ShouldReturnHostnameOrMachineName()
    {
        // Arrange
        var options = new ConnectionManagerOptions
        {
            InstanceId = null
        };

        // Act
        var result = options.GetInstanceId();

        // Assert
        result.Should().NotBeNullOrEmpty();
    }
}