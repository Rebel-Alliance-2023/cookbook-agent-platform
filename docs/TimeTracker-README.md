# Time-Tracker-MCP-Tool

**Time Tracker MCP (Model Context Protocol) Tool** designed to provide AI coding assistants (such as GitHub Copilot) with the ability to query the current system time and track execution time for Milestone/Task workflows. This enables accurate execution reports with real wall-clock timestamps and durations.

---

## Problem Statement

AI coding assistants currently lack the ability to:

1. **Query the current system time** — Cannot determine when tasks start or end
2. **Track elapsed time** — Cannot measure how long operations take
3. **Generate accurate execution reports** — Must rely on user-provided timestamps or generate simulated/estimated durations

---

## Features

### V1: Basic Time Query (Stateless)
- `time_get_current` — Query current system time with format and timezone options

### V2: Session-Based Tracking (Stateful)
- `time_session_start` — Initialize milestone tracking session
- `time_task_start` — Mark task start (idempotent)
- `time_task_end` — Mark task completion with duration
- `time_session_end` — End session and return summary
- `time_session_summary` — Get status without ending session

---

## Technology Stack

- **.NET 10** — Latest LTS runtime
- **ModelContextProtocol.Server** — Official C# MCP SDK from Microsoft
- **In-memory storage** — ConcurrentDictionary for session state
- **Streamable HTTP Transport** — Standard MCP server hosting

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build and Run

```bash
# Clone the repository
git clone https://github.com/Rebel-Alliance-2023/Time-Tracker-MCP-Tool.git
cd Time-Tracker-MCP-Tool

# Build
dotnet build

# Run tests
dotnet test

# Run the MCP server
dotnet run --project src/TimeTrackerMcp/TimeTrackerMcp.csproj
```

---

## Registration

### Visual Studio 2026

Visual Studio 2026 with GitHub Copilot supports MCP server registration. Follow these steps:

#### Option 1: Via Visual Studio Options (Recommended)

1. Open Visual Studio 2026 (Preview 3 or later)
2. Go to **Tools** > **Options** > **GitHub Copilot** > **MCP Servers**
3. Click **Add Server**
4. Configure the server:
   - **Name:** `time-tracker`
   - **Command:** `dotnet`
   - **Arguments:** `run --project "C:\path\to\Time-Tracker-MCP-Tool\src\TimeTrackerMcp\TimeTrackerMcp.csproj"`
   - **Working Directory:** `C:\path\to\Time-Tracker-MCP-Tool`
5. Click **OK** to save
6. Restart Visual Studio to apply changes

**Using Published Executable (Recommended for Production):**

For faster startup, use the published single-file executable instead:

1. Publish the project:
   ```bash
   dotnet publish src/TimeTrackerMcp/TimeTrackerMcp.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
   ```

2. Configure in Visual Studio:
   - **Name:** `time-tracker`
   - **Command:** `C:\path\to\publish\win-x64\TimeTrackerMcp.exe`
   - **Arguments:** (leave empty)
   - **Working Directory:** `C:\path\to\publish\win-x64`

#### Option 2: Via mcp.json in Solution Directory

Create an `mcp.json` file in your solution directory (next to your `.sln` or `.slnx` file):

```json
{
  "servers": {
    "time-tracker": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/path/to/Time-Tracker-MCP-Tool/src/TimeTrackerMcp/TimeTrackerMcp.csproj"
      ],
      "env": {}
    }
  }
}
```

**For published executable:**

```json
{
  "servers": {
    "time-tracker": {
      "type": "stdio",
      "command": "C:/path/to/publish/win-x64/TimeTrackerMcp.exe",
      "args": [],
      "env": {}
    }
  }
}
```

Visual Studio will automatically detect and load MCP servers defined in `mcp.json` when you open the solution.

#### Option 3: Via User-Level Configuration

For global availability across all solutions, add to your user profile:

1. Open `%APPDATA%\Microsoft\VisualStudio\17.0_<hash>\Settings\MCPServers.json`
2. Add the server configuration (create file if it doesn't exist):

```json
{
  "servers": {
    "time-tracker": {
      "type": "stdio",
      "command": "C:/path/to/publish/win-x64/TimeTrackerMcp.exe",
      "args": [],
      "env": {}
    }
  }
}
```

#### Verifying Registration

After registration, you can verify the tool is available by asking Copilot:
- "What time is it?" (should invoke `time_get_current`)
- "Start a session for milestone M1" (should invoke `time_session_start`)

**Testing Tool Invocation:**

1. Open the Copilot Chat panel in Visual Studio 2026
2. Type: "What is the current time?"
3. Copilot should invoke the `time_get_current` tool and display the result

**Expected Response Example:**
```json
{
  "timestamp": "2025-12-14T13:00:00-05:00",
  "timezone": "Eastern Standard Time",
  "utc_offset": "-05:00",
  "format": "iso8601"
}
```

**Testing Session Workflow:**
1. "Start a timing session for milestone M4 with tasks M4-001, M4-002, M4-003"
2. "Start task M4-001"
3. "End task M4-001"
4. "End the session and show the summary"

**Troubleshooting:**

| Issue | Solution |
|-------|----------|
| Server not found | Restart Visual Studio after adding configuration |
| Tool not invoked | Verify the path in mcp.json is correct |
| Connection timeout | Ensure .NET 10 SDK is installed |
| Permission denied | Run Visual Studio as administrator (first time only) |

### VS Code

VS Code with GitHub Copilot Chat supports MCP server registration. Follow these steps:

#### Option 1: Workspace Configuration (Recommended)

Add a `.vscode/mcp.json` file to your workspace (already included in this repository):

```json
{
  "$schema": "https://json.schemastore.org/mcp.json",
  "servers": {
    "time-tracker": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/TimeTrackerMcp/TimeTrackerMcp.csproj",
        "--no-build"
      ],
      "env": {},
      "description": "Time Tracker MCP Tool"
    }
  }
}
```

**Using Published Executable (Recommended for Production):**

```json
{
  "$schema": "https://json.schemastore.org/mcp.json",
  "servers": {
    "time-tracker": {
      "type": "stdio",
      "command": "${workspaceFolder}/publish/win-x64/TimeTrackerMcp.exe",
      "args": [],
      "env": {}
    }
  }
}
```

#### Option 2: User Settings (Global)

For global availability across all workspaces:

1. Open Command Palette (`Ctrl+Shift+P` / `Cmd+Shift+P`)
2. Run **Preferences: Open User Settings (JSON)**
3. Add the MCP server configuration:

```json
{
  "github.copilot.chat.mcpServers": {
    "time-tracker": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/path/to/Time-Tracker-MCP-Tool/src/TimeTrackerMcp/TimeTrackerMcp.csproj"
      ],
      "env": {}
    }
  }
}
```

#### Option 3: CLI-Based Registration

Use the VS Code CLI to add MCP servers to your user profile:

```bash
# Add the MCP server to user profile
code --add-mcp '{"name":"time-tracker","type":"stdio","command":"dotnet","args":["run","--project","C:/path/to/TimeTrackerMcp.csproj"]}'

# Or using the published executable
code --add-mcp '{"name":"time-tracker","type":"stdio","command":"C:/path/to/publish/win-x64/TimeTrackerMcp.exe"}'
```

To remove:
```bash
code --remove-mcp time-tracker
```

To list registered servers:
```bash
code --list-mcp
```

#### Verifying Registration in VS Code

1. Open the Copilot Chat panel
2. Type: "What is the current time?"
3. Copilot should invoke the `time_get_current` tool

**Expected Response:**
```json
{
  "timestamp": "2025-12-14T13:00:00-05:00",
  "timezone": "Eastern Standard Time",
  "utc_offset": "-05:00"
}
```

**Testing Session Workflow in VS Code:**
1. "Start a timing session for milestone M4 with tasks M4-009, M4-010, M4-011"
2. "Start task M4-009"
3. "End task M4-009 as completed"
4. "Show the session summary"
5. "End the session"

**VS Code Troubleshooting:**

| Issue | Solution |
|-------|----------|
| Server not detected | Reload VS Code window (`Ctrl+Shift+P` → "Reload Window") |
| MCP not available | Ensure GitHub Copilot Chat extension is installed and updated |
| Path not found | Use absolute paths or `${workspaceFolder}` variable |
| Build errors | Run `dotnet build` first, then use `--no-build` flag |
| Permission denied | On Linux/macOS, ensure executable has +x permission |

---

## Usage Example

```
AI: [calls time_session_start]
    milestone_id: "M2"
    task_ids: ["M2-001", "M2-002", "M2-003"]
    => session_id: "abc123..."

    [calls time_task_start] task_id: "M2-001"
    ... implements task ...
    [calls time_task_end] task_id: "M2-001"
    => Duration: 1 minute 13 seconds

    [calls time_session_end]
    => Total Duration: 5 minutes 42 seconds
    => Tasks Completed: 3/3
```

---

## Project Structure

```
Time-Tracker-MCP-Tool/
|-- src/
|   +-- TimeTrackerMcp/
|       |-- TimeTrackerMcp.csproj
|       |-- Program.cs
|       |-- Tools/
|       |   +-- TimeTools.cs
|       |-- Services/
|       |   |-- ISessionService.cs
|       |   |-- InMemorySessionService.cs
|       |   +-- ITimeProvider.cs
|       +-- Models/
|           |-- Session.cs
|           |-- TaskRecord.cs
|           +-- TimeResult.cs
|-- tests/
|   +-- TimeTrackerMcp.Tests/
|       |-- TimeTrackerMcp.Tests.csproj
|       +-- TimeToolsTests.cs
|-- docs/
|   |-- Time Tracker MCP Tool Specification.md
|   |-- Implementation Plan - Time Tracker MCP Tool.md
|   +-- Time Tracker MCP Tool - Task List.md
|-- .vscode/
|   +-- mcp.json
|-- .github/
|   +-- workflows/
|       +-- build.yml
|-- README.md
+-- LICENSE
```

---

## Documentation

- [Specification](docs/Time%20Tracker%20MCP%20Tool%20Specification.md) — Detailed requirements and API schemas
- [Implementation Plan](docs/Implementation%20Plan%20-%20Time%20Tracker%20MCP%20Tool.md) — Architecture and milestones
- [Task List](docs/Time%20Tracker%20MCP%20Tool%20-%20Task%20List.md) — Granular work breakdown

---

## Security & Permissions

### Tool Permissions

The Time Tracker MCP Tool is designed with a **minimal permission model**:

| Permission | Status | Description |
|------------|--------|-------------|
| **Filesystem Access** | ❌ None | Does not read, write, or access any files |
| **Network Access** | ❌ None | Does not make outbound network connections |
| **Environment Variables** | ❌ None | Does not read sensitive environment variables |
| **Process Spawning** | ❌ None | Does not spawn child processes |
| **System Modification** | ❌ None | Does not modify system settings |

### Data Handling

| Data Type | Handling |
|-----------|----------|
| **Session Data** | Stored in-memory only, never persisted to disk |
| **Task Metadata** | User-provided, kept in memory during session |
| **Timestamps** | System clock queries only |
| **PII** | No collection or logging of personally identifiable information |

### Security Posture

**Minimal Risk Classification:**

1. **Sandboxed Execution** — The tool runs as an MCP server with no elevated privileges
2. **No External Dependencies at Runtime** — Self-contained executable with no network calls
3. **No Persistence** — All data is ephemeral and lost on server restart
4. **Read-Only System Access** — Only queries current time from system clock
5. **No Code Execution** — Does not evaluate or execute user-provided code
6. **Stateless Between Sessions** — Sessions are isolated and expire automatically

**Automatic Cleanup:**
- Sessions expire after **24 hours** of age
- Sessions expire after **4 hours** of inactivity
- Maximum **100 concurrent sessions** enforced
- Maximum **500 tasks per session** enforced

### Trust Boundaries

```
┌─────────────────────────────────────────────────────────┐
│  AI Assistant (Copilot)                                 │
│  ┌───────────────────────────────────────────────────┐  │
│  │  MCP Client                                       │  │
│  └───────────────────┬───────────────────────────────┘  │
└──────────────────────│──────────────────────────────────┘
                       │ stdio/HTTP
┌──────────────────────│──────────────────────────────────┐
│  Time Tracker MCP Server                                │
│  ┌───────────────────▼───────────────────────────────┐  │
│  │  Tool Methods (read-only time queries)            │  │
│  ├───────────────────────────────────────────────────┤  │
│  │  In-Memory Session Store (no disk I/O)            │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Server not found | Configuration not loaded | Restart IDE after adding mcp.json |
| Tool not invoked | Path incorrect | Use absolute paths or verify relative path |
| Connection timeout | SDK not installed | Install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Permission denied | Executable permissions | Run `chmod +x TimeTrackerMcp` on Linux/macOS |
| Build errors | Dependencies missing | Run `dotnet restore` then `dotnet build` |
| Session not found | Session expired | Sessions expire after 4h inactivity or 24h age |
| Max sessions reached | Too many active sessions | End unused sessions or wait for cleanup |

### Visual Studio 2026 Issues

| Issue | Solution |
|-------|----------|
| MCP Servers option missing | Update to VS 2026 Preview 3+ |
| Server fails to start | Check Output window for error details |
| Tools not appearing | Verify Copilot extension is enabled |

### VS Code Issues

| Issue | Solution |
|-------|----------|
| MCP not available | Install/update GitHub Copilot Chat extension |
| ${workspaceFolder} not resolved | Use absolute path instead |
| Server crashes on start | Check terminal for .NET runtime errors |

### Debugging

**Enable verbose logging:**

The server logs to stderr. To capture logs:

```bash
# Windows
dotnet run --project src/TimeTrackerMcp/TimeTrackerMcp.csproj 2> server.log

# Linux/macOS
dotnet run --project src/TimeTrackerMcp/TimeTrackerMcp.csproj 2>&1 | tee server.log
```

**Check server health:**

```bash
# Verify server starts successfully
dotnet run --project src/TimeTrackerMcp/TimeTrackerMcp.csproj

# Expected output:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://localhost:5000
```

**Verify tool registration:**

Ask the AI assistant: "List all available MCP tools" — the time tracker tools should appear.

---

## References

- [MCP Specification](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- [Official C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [VS Code MCP Servers](https://code.visualstudio.com/docs/copilot/customization/mcp-servers)

---

## License

MIT License — See [LICENSE](LICENSE) for details.
