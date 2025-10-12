using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;

namespace ServiceBusQueueInspector;

internal enum ExitCode
{
    Success = 0,
    InvalidArgument = 1,
    ConfigurationError = 2,
    UnknownCommand = 3,
    ServiceBusError = 4
}

internal static class Program
{
    private static async Task Main(string[] args)
    {
        ExitCode exitCode;

        try
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            string? connectionString = configuration["ServiceBus:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.WriteLine("❌ ConnectionString not found in appsettings.json");
                Environment.ExitCode = (int)ExitCode.ConfigurationError;
                return;
            }

            CommandConfig? config = ParseArguments(args);
            if (config is null)
            {
                DisplayUsage();
                Environment.ExitCode = (int)ExitCode.InvalidArgument;
                return;
            }

            await using ServiceBusClient client = new(connectionString);
            exitCode = await ExecuteCommandAsync(client, config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex.Message}");
            exitCode = ExitCode.ServiceBusError;
        }

        Environment.ExitCode = (int)exitCode;
    }

    internal static CommandConfig? ParseArguments(string[] args)
    {
        if (args.Length < 2)
            return null;

        string command = args[0].ToLowerInvariant();
        string queueName = args[1];
        string? message = args.Length > 2 ? string.Join(' ', args.Skip(2)) : null;

        return new CommandConfig(command, queueName, message);
    }

    internal static void DisplayUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- delete <queueName>");
        Console.WriteLine("  dotnet run -- peek <queueName>");
        Console.WriteLine("  dotnet run -- send <queueName> <message>");
    }

    private static async Task<ExitCode> ExecuteCommandAsync(ServiceBusClient client, CommandConfig config)
    {
        switch (config.Command)
        {
            case "delete":
                return await DeleteMessagesAsync(client, config.QueueName);

            case "peek":
                return await PeekMessagesAsync(client, config.QueueName);

            case "send":
                if (!string.IsNullOrWhiteSpace(config.Message))
                {
                    return await SendMessageAsync(client, config.QueueName, config.Message);
                }

                Console.WriteLine("❌ Missing message text for 'send' command.");
                return ExitCode.InvalidArgument;

            default:
                Console.WriteLine($"❌ Unknown command: {config.Command}");
                Console.WriteLine("Valid commands: delete, peek, send");
                return ExitCode.UnknownCommand;
        }
    }
    
    private static async Task<ExitCode> DeleteMessagesAsync(ServiceBusClient client, string queueName)
    {
        try
        {
            await using ServiceBusReceiver? receiver = client.CreateReceiver(queueName);

            int deletedCount = 0;
            while (true)
            {
                ServiceBusReceivedMessage? message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
                if (message is null)
                    break;

                await receiver.CompleteMessageAsync(message);
                deletedCount++;
            }

            Console.WriteLine($"🗑️ Deleted {deletedCount} messages from queue '{queueName}'.");
            return ExitCode.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error deleting messages: {ex.Message}");
            return ExitCode.ServiceBusError;
        }
    }

    private static async Task<ExitCode> SendMessageAsync(ServiceBusClient client, string queueName, string messageText)
    {
        try
        {
            await using ServiceBusSender? sender = client.CreateSender(queueName);

            ServiceBusMessage message = new(messageText)
            {
                ApplicationProperties = { ["Source"] = "TestClient" }
            };

            await sender.SendMessageAsync(message);
            Console.WriteLine($"✅ Message sent to queue '{queueName}': \"{messageText}\"");
            return ExitCode.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error sending message: {ex.Message}");
            return ExitCode.ServiceBusError;
        }
    }

    private static async Task<ExitCode> PeekMessagesAsync(ServiceBusClient client, string queueName)
    {
        try
        {
            await using ServiceBusReceiver? receiver = client.CreateReceiver(queueName);
            IReadOnlyList<ServiceBusReceivedMessage>? messages = await receiver.PeekMessagesAsync(maxMessages: 10);

            Console.WriteLine($"\n📬 Peeked {messages.Count} messages from '{queueName}':");

            foreach (ServiceBusReceivedMessage msg in messages)
            {
                Console.WriteLine($"- Body: {msg.Body}");

                foreach ((string? key, object? value) in msg.ApplicationProperties)
                    Console.WriteLine($"  • {key}: {value}");
            }

            return ExitCode.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error peeking messages: {ex.Message}");
            return ExitCode.ServiceBusError;
        }
    }

    internal record CommandConfig(string Command, string QueueName, string? Message);
}
