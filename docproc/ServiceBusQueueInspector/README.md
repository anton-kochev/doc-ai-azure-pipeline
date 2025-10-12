# ServiceBusQueueInspector

A command-line utility for managing and inspecting Azure Service Bus queues during development and testing.

## Overview

ServiceBusQueueInspector provides a simple interface to interact with Azure Service Bus queues, allowing you to send, peek, and delete messages. It's particularly useful for local development with the Azure Service Bus emulator.

## Features

- **Delete Messages**: Remove all messages from a queue
- **Peek Messages**: View up to 10 messages without removing them from the queue
- **Send Messages**: Send test messages to a queue

## Prerequisites

- .NET 8.0 SDK
- Azure Service Bus connection string (supports local emulator)

## Configuration

Configure your Service Bus connection string in `appsettings.json`:

```json
{
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
  }
}
```

## Usage

### Delete all messages from a queue

```bash
dotnet run -- delete <queueName>
```

Example:
```bash
dotnet run -- delete document-processing
```

### Peek messages in a queue

View up to 10 messages without removing them:

```bash
dotnet run -- peek <queueName>
```

Example:
```bash
dotnet run -- peek document-processing
```

### Send a test message

```bash
dotnet run -- send <queueName> <message>
```

Example:
```bash
dotnet run -- send document-processing "Test document payload"
```

## Exit Codes

The application returns the following exit codes:

- `0` - Success
- `1` - Invalid argument
- `2` - Configuration error (missing connection string)
- `3` - Unknown command
- `4` - Service Bus error

## Dependencies

- Azure.Messaging.ServiceBus (7.20.1)
- Microsoft.Extensions.Configuration (8.0.0)
- Microsoft.Extensions.Configuration.Json (8.0.0)
- Microsoft.Extensions.Configuration.Binder (8.0.0)

## Development

Build the project:

```bash
dotnet build
```

Run tests:

```bash
dotnet test
```

The test suite includes:
- **Argument parsing tests** - Validates command-line argument handling for all commands (delete, peek, send)
- **Exit code tests** - Ensures proper exit codes are defined
