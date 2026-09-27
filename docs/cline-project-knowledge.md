# CBMR Web Admin: Cline Project Knowledge

This document is the working context for future Cline sessions in this repository.
Update it when the architecture or communication contract changes.

## Project Overview

- Repository: `CBMR-Web-Admin`
- Solution: `CBMR-Web-Admin.sln`
- Runtime: `.NET 10`, SDK version `10.0.0` from `global.json`
- Main language: C# with nullable reference types and implicit usings enabled
- Web UI: ASP.NET Core Blazor Server, interactive server components
- Database: SQLite with ASP.NET Core Identity and Entity Framework Core
- Game/server integration: `ServerBinding` communicates with `WebPortal` over a framed named pipe
- Shared protocol and DTOs: `Shared`

## Projects

### `WebPortal`

ASP.NET Core web application and Blazor UI.

Important files:

- `Program.cs`: dependency injection and application startup
- `PipeBackgroundService.cs`: named-pipe client lifecycle and request processing loop
- `PipeGateway.cs`: public request API used by Razor components
- `PipeMessageQueue.cs`: single-reader channel and queued request completion sources
- `Components/_Imports.razor`: shared Razor usings and injected services
- `Components/Pages/*`: application pages
- `Components/Dialogs/*`: command dialogs/buttons

### `ServerBinding`

Plugin/integration code loaded by the CBMR server. It hosts the named-pipe server.

Important files:

- `EntryPoint.cs`: plugin load/unload lifecycle and pipe listener thread
- `Listener.cs`: dispatches `PipeMessageType` requests and creates responses
- `Config.cs`: server-side configuration model

### `Shared`

Referenced by both sides of the pipe.

Important files:

- `PipeEnvelope.cs`: envelope kind, message type, request/response factories, MessagePack payload handling
- `PipeProtocol.cs`: 4-byte little-endian frame length plus MessagePack body
- `ServerLocation.cs`: shared DTO containing the server executable `Path`
- `Requests/*`: request DTOs used by commands such as kick and broadcast

## Named Pipe Protocol

### Transport

`PipeProtocol` sends each message as:

1. A 4-byte little-endian signed integer containing body length.
2. A MessagePack-serialized `PipeEnvelope` body.

Maximum body size is `16 * 1024 * 1024` bytes. The envelope protocol version is `1`.

Every response must preserve the request `RequestId`. The WebPortal validates this before completing a queued request.

### Envelope

`PipeEnvelope` fields:

- `Version`
- `RequestId`
- `Kind`: `Request`, `Response`, or `Error`
- `MessageType`
- optional MessagePack `Payload`
- optional `Error`

When adding a payload type, add a MessagePack-compatible DTO in `Shared` and keep stable `[Key(n)]` values.

### Message Types

Current values in `Shared/PipeEnvelope.cs`:

- `WhereAreYou`
- `FixElevator`
- `Players`
- `KickPlayer`
- `Broadcast`
- `Chats`
- `ClearItems`

When adding a message type, update the switch in `ServerBinding/Listener.cs` and add the corresponding WebPortal call where needed.

## WhereAreYou and Server Path Flow

The connection handshake is implemented as follows:

1. `WebPortal/PipeBackgroundService.cs` creates a `NamedPipeClientStream` and calls `ConnectAsync`.
2. After connection succeeds, it sends a `WhereAreYou` request directly on that pipe before consuming normal queued requests.
3. `ServerBinding/Listener.cs` handles `WhereAreYou` and returns `ServerLocation`.
4. The server path is read from `ServerLocation.Path`.
5. The same `ServerLocation` instance is registered as a WebPortal singleton, so other WebPortal services/components can inject it and read the latest path.
6. On a later reconnect, the path is refreshed. Before the first successful response, `Path` is the default empty string.

Use this pattern in WebPortal code when the server executable path is needed:

```csharp
public class SomeService(ServerLocation serverLocation)
{
    public string GetServerPath() => serverLocation.Path;
}
```

The current server response is `Process.GetCurrentProcess().MainModule!.FileName`, wrapped in `new ServerLocation { Path = ... }`.

## Dependency Injection Lifetimes

Current registrations in `WebPortal/Program.cs` include:

- `PipeMessageQueue`: singleton
- `ServerLocation`: singleton
- `PipeBackgroundService`: hosted service
- `PipeGateway`: singleton
- Identity redirect/authentication services: scoped

Do not register a second `ServerLocation` instance with a different lifetime. The background service and consumers must observe the same object.

## Request Usage From Razor Components

`Components/_Imports.razor` injects:

```razor
@inject PipeGateway ServerBinding
```

Typical calls:

```csharp
await ServerBinding.SendAsync(PipeMessageType.FixElevator);
List<WebPlayer> players = await ServerBinding.SendAsync<List<WebPlayer>>(PipeMessageType.Players);
await ServerBinding.SendAsync(PipeMessageType.Broadcast, request, cancellationToken);
```

The queue is single-reader. `PipeBackgroundService` writes one request, waits for one matching response, then completes that request's `TaskCompletionSource`.

## Pipe Lifecycle and Failure Behavior

- Pipe name on the WebPortal defaults to `CbmrWebAdmin` and can be overridden by `PipeName` configuration.
- `ServerBinding/EntryPoint.cs` currently creates the server pipe with the literal name `CbmrWebAdmin`.
- The WebPortal retries after a configurable delay from `PipeReconnectDelaySeconds`; default is 5 seconds and values below 1 second are clamped to 1 second.
- A failed handshake is treated like a failed connection. The client logs a warning and retries.
- A failed in-flight queued request receives the exception before the connection loop retries.
- `PipeBackgroundService.ServerBindingPipe` is used by `MainLayout.razor` to display connection status. Be careful when changing its lifecycle or nullability.

## Coding and Change Guidelines

- Read the relevant files and `git status` before editing; the worktree may contain user changes.
- Do not revert or overwrite unrelated existing changes.
- Keep protocol DTOs in `Shared`; do not duplicate the same DTO in `WebPortal` or `ServerBinding`.
- Preserve MessagePack keys and envelope version compatibility unless a protocol migration is intentional.
- Validate response request IDs and message types for new handshake-like operations.
- Prefer existing `PipeGateway` APIs for normal UI requests. Direct pipe writes belong in `PipeBackgroundService` only when they are part of connection lifecycle.
- Use `apply_patch` for manual edits.
- Keep comments short and explain only non-obvious protocol or lifecycle behavior.

## Validation Commands

Run from the repository root:

```bash
dotnet build CBMR-Web-Admin.sln --no-restore
git diff --check
git status --short
```

There is currently no test project visible in the solution tree. If tests are added, follow the existing .NET solution conventions and test protocol serialization, request ID matching, handshake payload validation, and reconnect behavior.

## Current Worktree Context

The repository may contain uncommitted changes from the user. In particular, changes around `WhereAreYou`, `ServerLocation`, UI dialogs/pages, localization, and `ServerBinding/Listener.cs` may already be present when a future Cline session starts. Inspect the diff before changing those files.

This knowledge file describes the intended current architecture; source code remains authoritative if it differs from this document.