namespace ServiceBusQueueInspector.Tests;

public class ProgramTests
{
    [Fact]
    public void ParseArguments_WithValidDeleteCommand_ReturnsCommandConfig()
    {
        // Arrange
        string[] args = ["delete", "test-queue"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("delete", result.Command);
        Assert.Equal("test-queue", result.QueueName);
        Assert.Null(result.Message);
    }

    [Fact]
    public void ParseArguments_WithValidPeekCommand_ReturnsCommandConfig()
    {
        // Arrange
        string[] args = ["peek", "test-queue"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("peek", result.Command);
        Assert.Equal("test-queue", result.QueueName);
        Assert.Null(result.Message);
    }

    [Fact]
    public void ParseArguments_WithValidSendCommand_ReturnsCommandConfigWithMessage()
    {
        // Arrange
        string[] args = ["send", "test-queue", "Hello", "World"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("send", result.Command);
        Assert.Equal("test-queue", result.QueueName);
        Assert.Equal("Hello World", result.Message);
    }

    [Fact]
    public void ParseArguments_WithSingleWordMessage_ReturnsCommandConfigWithMessage()
    {
        // Arrange
        string[] args = ["send", "test-queue", "TestMessage"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("send", result.Command);
        Assert.Equal("test-queue", result.QueueName);
        Assert.Equal("TestMessage", result.Message);
    }

    [Fact]
    public void ParseArguments_WithUpperCaseCommand_ReturnsLowerCaseCommand()
    {
        // Arrange
        string[] args = ["DELETE", "test-queue"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("delete", result.Command);
    }

    [Fact]
    public void ParseArguments_WithMixedCaseCommand_ReturnsLowerCaseCommand()
    {
        // Arrange
        string[] args = ["PeEk", "test-queue"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("peek", result.Command);
    }

    [Fact]
    public void ParseArguments_WithNoArguments_ReturnsNull()
    {
        // Arrange
        string[] args = [];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseArguments_WithOnlyCommand_ReturnsNull()
    {
        // Arrange
        string[] args = ["delete"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseArguments_WithInvalidCommand_StillReturnsCommandConfig()
    {
        // Arrange
        string[] args = ["invalid", "test-queue"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("invalid", result.Command);
        Assert.Equal("test-queue", result.QueueName);
    }

    [Fact]
    public void ParseArguments_WithSpecialCharactersInQueueName_ReturnsCommandConfig()
    {
        // Arrange
        string[] args = ["delete", "test-queue-with-dashes"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("delete", result.Command);
        Assert.Equal("test-queue-with-dashes", result.QueueName);
    }

    [Fact]
    public void ParseArguments_WithLongMessage_JoinsAllMessageParts()
    {
        // Arrange
        string[] args = ["send", "test-queue", "This", "is", "a", "very", "long", "message"];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("send", result.Command);
        Assert.Equal("test-queue", result.QueueName);
        Assert.Equal("This is a very long message", result.Message);
    }

    [Fact]
    public void ParseArguments_WithEmptyStringMessage_ReturnsEmptyMessage()
    {
        // Arrange
        string[] args = ["send", "test-queue", ""];

        // Act
        Program.CommandConfig? result = Program.ParseArguments(args);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("send", result.Command);
        Assert.Equal("test-queue", result.QueueName);
        Assert.Equal("", result.Message);
    }

    [Fact]
    public void DisplayUsage_DoesNotThrowException()
    {
        // Act & Assert
        Exception? exception = Record.Exception(Program.DisplayUsage);
        Assert.Null(exception);
    }
}
