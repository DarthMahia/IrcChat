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
        Assert.Equal("custom-instance-id", result);
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
        Assert.NotEmpty(result);
        // Soit HOSTNAME env var, soit Environment.MachineName
        Assert.True(result == Environment.GetEnvironmentVariable("HOSTNAME") ||
            result == Environment.MachineName);
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
        Assert.NotEmpty(result);
    }
}