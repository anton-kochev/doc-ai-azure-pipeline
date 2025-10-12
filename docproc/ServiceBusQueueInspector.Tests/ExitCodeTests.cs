namespace ServiceBusQueueInspector.Tests;

public class ExitCodeTests
{
    [Fact]
    public void ExitCode_Success_HasValueZero()
    {
        // Assert
        Assert.Equal(0, (int)ExitCode.Success);
    }

    [Fact]
    public void ExitCode_InvalidArgument_HasValueOne()
    {
        // Assert
        Assert.Equal(1, (int)ExitCode.InvalidArgument);
    }

    [Fact]
    public void ExitCode_ConfigurationError_HasValueTwo()
    {
        // Assert
        Assert.Equal(2, (int)ExitCode.ConfigurationError);
    }

    [Fact]
    public void ExitCode_UnknownCommand_HasValueThree()
    {
        // Assert
        Assert.Equal(3, (int)ExitCode.UnknownCommand);
    }

    [Fact]
    public void ExitCode_ServiceBusError_HasValueFour()
    {
        // Assert
        Assert.Equal(4, (int)ExitCode.ServiceBusError);
    }
}
