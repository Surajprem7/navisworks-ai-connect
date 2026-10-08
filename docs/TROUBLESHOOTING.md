# Troubleshooting

Run **`check-setup.bat`** first. It tells you which piece is missing.

## The "AI Connect" tab is not in Navisworks
* The plugin must be in `...\Navisworks Manage 2027\Plugins\NavisBridge\` (that is what `go.bat` does). Folders under
  `%APPDATA%` and `ApplicationPlugins` bundles did **not** load in testing.
* Navisworks reads plugins at start-up: close it fully (check Task Manager for `Roamer.exe`) and start it again.
* The folder name must equal the DLL name (`NavisBridge`), and `ClaudeRibbon.xaml` must sit next to the DLL and in `en-US\`.
* Several Navisworks windows can be running at once. Close them all before running `go.bat`.

## Clicking the button shows "Default Command Handler"
The ribbon XAML is not wired to the plugin. Reinstall with `go.bat` so `ClaudeRibbon.xaml` is copied (both folders), and restart Navisworks.

## `go.bat`: BUILD FAILED
* `.NET SDK not found`: `winget install Microsoft.DotNet.SDK.8`, then open a new window.
* `Navisworks not found`: set `NAVISDIR` to your install folder (see README).
* Compile errors mentioning `Autodesk.Navisworks.*`: your Navisworks version has a different API. Send `build-log.txt` in an issue.
* `cannot access ... NavisBridge.dll` / file in use: Navisworks is still running.

## Install step does nothing or the permission prompt never appears
The installer needs administrator rights. If you cancelled the prompt, run `install-plugin.bat` again. A UAC prompt can hide behind other windows.

## Claude says it has no Navisworks tools
* Fully quit and reopen Claude (system tray > Quit). Closing the window is not enough.
* Check the config has a `navisworks` entry (`add-to-claude-config.bat`) and that `node` is installed (`node --version`).
* Claude Desktop: Settings > Developer shows the server and any error.

## "Cannot reach the Navisworks bridge" / calls time out
* Navisworks must be running, with **AI Connect switched on** (pop-up "AI Connect is on").
* A dialog box open in Navisworks blocks the UI thread. Close it and retry.
* Port 47800 in use by another program: `netstat -ano | findstr 47800`.
* Test directly: see "Calling the bridge directly" in [TOOLS.md](TOOLS.md).
* Read `%TEMP%\navisbridge.log`. `accepted POST` without `Handle start` means a library failed to load; reinstall so `Newtonsoft.Json.dll` is next to the plugin.

## Results look wrong
* `search_items` needs the exact category/property names from Navisworks' Properties window. Try `match: contains`.
* Clash tests compare **saved selection sets**; create them first with `create_selection_set`.

## Still stuck
Open an issue with: Navisworks version, `check-setup.bat` output, `build-log.txt` and `%TEMP%\navisbridge.log`.
