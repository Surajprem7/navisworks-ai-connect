# Navisworks AI Connect

Let an AI assistant (Claude, or any [MCP](https://modelcontextprotocol.io) client) work inside **Autodesk Navisworks
Manage 2027**: search and select model items, manage selection sets and viewpoints, and create, run and read clash tests.
You talk to the assistant; it drives Navisworks.

```
AI client --(MCP over stdio)--> mcp-server/server.js --(HTTP, 127.0.0.1:47800)--> NavisBridge plugin inside Navisworks
```

* `NavisBridge/` is a C# plugin (Navisworks .NET API, .NET Framework 4.8). It adds an **AI Connect** ribbon tab with an on/off button.
* `mcp-server/server.js` is a small Node MCP server (no `npm install` needed).
* Everything stays on your computer. The bridge only listens on `127.0.0.1`.

Things you can ask once it is connected:
> "How many models are loaded?" · "Select everything where Item > Name contains Pipe." · "Create a selection set called
> Mechanical from System Name = Chilled Water." · "Create a hard clash test between Mechanical and Structural, run it,
> and list the new clashes." · "Save the current view as 'Level 2 review'."

Tested on Navisworks Manage 2027 with Claude Desktop on Windows 11. Other Navisworks versions may work, see [Other versions](#other-navisworks-versions).

---

## Install (recommended): one download

1. Download **AI-Connect-Setup.exe** from the [latest release](https://github.com/Surajprem7/navisworks-ai-connect/releases/latest).
2. Close Navisworks and Claude Desktop, then run it. Windows may show "unknown publisher" (the file is not code-signed): click **More info > Run anyway**.
3. Tick the Navisworks versions to install into (2022-2027, Manage or Simulate; detected automatically) and finish.
4. Fully quit Claude (system tray > Quit) and reopen it. Open Navisworks and click **AI Connect > AI Connect** on the ribbon.

Needs [Node.js](https://nodejs.org) (`winget install OpenJS.NodeJS.LTS`) so Claude can run the connector.

**Updates:** AI Connect checks GitHub once a day when Navisworks starts and asks before updating. If you agree, it downloads the installer, verifies its SHA-256 checksum and installs when you close Navisworks (Windows asks for permission). You can tick "Update automatically in the future"; after a release is over a week old the reminder appears on every start. Turn the check off with the environment variable `AI_CONNECT_NO_UPDATE_CHECK=1`. You can also run the newer AI-Connect-Setup.exe by hand, or ask Claude "check for AI Connect updates".

Maintainers: publish each release with both `AI-Connect-Setup.exe` and `AI-Connect-Setup.exe.sha256` (build-installer.bat creates both).
**Report a bug:** Start menu > AI Connect > *Report a bug* (collects diagnostics, opens the GitHub form), or open an [issue](https://github.com/Surajprem7/navisworks-ai-connect/issues/new?template=bug_report.yml).
**Uninstall:** Windows Settings > Apps > AI Connect for Navisworks.

Builds for 2022-2026 are compiled against community API packages and are lightly tested; 2027 is fully tested. Please report problems.

## Install from source (developers)

Run `build-all.bat` (builds every version into `dist\`), then `installer\build-installer.bat` (needs Inno Setup, auto-installed via winget). For a single version use `go.bat`.

## 1. What you need

| Requirement | How to get it |
|---|---|
| Windows 10/11 and **Navisworks Manage 2027** | Clash tools need *Manage* (not Simulate/Freedom). |
| **.NET SDK 8** (or Visual Studio with ".NET desktop development") | `winget install Microsoft.DotNet.SDK.8` |
| **Node.js 18+** | `winget install OpenJS.NodeJS.LTS` |
| An MCP client, e.g. **Claude Desktop** or Claude Code | https://claude.ai/download |
| Administrator rights (once per install) | Plugin goes into Navisworks' `Plugins` folder. |

Open a **new** Command Prompt afterwards so `dotnet` and `node` are on the PATH. Then double-click **`check-setup.bat`** to see what is still missing.

## 2. Quick start (about 5 minutes)

1. **Download** this repo (Code > Download ZIP) and unzip it somewhere with no spaces needed, e.g. `C:\Tools\navisworks-ai-connect`.
2. **Close Navisworks.**
3. Double-click **`go.bat`**. It builds the plugin and installs it into
   `C:\Program Files\Autodesk\Navisworks Manage 2027\Plugins\NavisBridge\`. Click **Yes** on the Windows permission prompt.
   It must end with a file list, not `BUILD FAILED`.
4. Double-click **`add-to-claude-config.bat`** (close Claude Desktop first). It adds the `navisworks` server to
   Claude Desktop's config (with a backup). Then **fully quit and reopen Claude** (system tray > Quit, not just close the window).
   *Using another client? See [Connect other MCP clients](#connect-other-mcp-clients).*
5. Start **Navisworks**, open a model, go to the **AI Connect** tab and click **AI Connect**. A pop-up says
   "AI Connect is on". Click OK.
6. In Claude, ask: *"What is open in Navisworks?"* You should get the file name and the list of models.

If something fails, run **`check-setup.bat`** and read [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

## 3. Everyday use

* Every time you start Navisworks: open the **AI Connect** tab and click **AI Connect** (the button highlights when on).
  Click again to turn it off.
* Keep a model open and **no dialog box showing** in Navisworks, otherwise calls wait or time out.
* Claude Desktop starts the MCP server by itself. You never run `server.js` by hand.

## 4. Connect other MCP clients

The server speaks standard MCP over stdio. Point your client at `node <repo>\mcp-server\server.js`.

**Claude Desktop** (manual alternative to the script): edit `claude_desktop_config.json`
(Microsoft Store/MSIX installs keep it under `%LOCALAPPDATA%\Packages\Claude_*\LocalCache\Roaming\Claude\`,
classic installs under `%APPDATA%\Claude\`):

```json
{
  "mcpServers": {
    "navisworks": {
      "command": "node",
      "args": ["C:\\Tools\\navisworks-ai-connect\\mcp-server\\server.js"]
    }
  }
}
```

**Claude Code (CLI):**
```
claude mcp add navisworks -- node "C:\Tools\navisworks-ai-connect\mcp-server\server.js"
```

**Other clients** (Cursor, VS Code, etc.): use the same `command` and `args` in their MCP settings.

Optional environment variables for the server and plugin:

| Variable | Meaning |
|---|---|
| `NAVIS_BRIDGE_TOKEN` | Shared secret. Set it for **both** Navisworks and the MCP client; requests without the matching `X-Bridge-Token` header are refused. |
| `NAVIS_BRIDGE_URL` | Server side only. Default `http://127.0.0.1:47800/`. |

## 5. Tools

`get_model_info`, `search_items`, `select_items`, `list_selection_sets`, `create_selection_set`, `list_viewpoints`,
`save_viewpoint`, `list_clash_tests`, `create_clash_test`, `run_clash_test`, `get_clash_results`.
Arguments and examples: [docs/TOOLS.md](docs/TOOLS.md).

## 6. Update, rebuild, uninstall

* **Update / change code:** close Navisworks, run `go.bat` again.
* **Uninstall:** close Navisworks, delete
  `C:\Program Files\Autodesk\Navisworks Manage 2027\Plugins\NavisBridge`, and remove the `navisworks` block from
  Claude's config (a `.bak-before-navisworks` backup sits next to it).

## Other Navisworks versions

The project reads `Roamer.runtimeconfig.json` from the install folder to choose the right .NET target
(Navisworks 2027 uses .NET Framework 4.8, so no file means `net48`). For a different folder or version:

```
set NAVISDIR=D:\Autodesk\Navisworks Manage 2026
go.bat
```
or build by hand: `dotnet build -c Release -p:NavisDir="..." -p:NavisTfm=net8.0-windows`. Newer or older API versions may need small code changes.

## Security notes

* The bridge accepts connections from this computer only, but any local program could call it. Set `NAVIS_BRIDGE_TOKEN`
  if you share the machine.
* The assistant can change your model files (selection sets, viewpoints, clash tests). Save your `.nwf` before big sessions.
* Navisworks API calls run on the Navisworks UI thread, so large operations briefly freeze Navisworks.

## Repo layout

```
go.bat                      build + install in one click
install-plugin.bat          copy the built plugin into Navisworks (administrator)
check-setup.bat             health check
add-to-claude-config.bat/.ps1   register the MCP server in Claude Desktop
NavisBridge/                C# plugin (BridgePlugin.cs = HTTP bridge + ribbon, Tools.cs = Navisworks operations)
mcp-server/server.js        MCP server
docs/                       tools reference and troubleshooting
```

A debug log is written to `%TEMP%\navisbridge.log`. Ribbon icon glyph from [Lucide](https://lucide.dev) (ISC, see `NOTICE/`).

## Licence
MIT, see `LICENSE`.
