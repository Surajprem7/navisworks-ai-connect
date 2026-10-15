# Changelog

## 1.4.1
- Bigger, easier-to-read update window. The "update automatically / stop asking" option is removed: the prompt always appears when an update is available.

## 1.4.0
- New: `list_hidden_items` shows what is hidden in the view. `create_selection_set_from_items` makes one saved selection set from exact items: all hidden items, the current selection, or a list of GUIDs.

## 1.3.1
- The update window now appears every time Navisworks starts while a newer version exists. The button "Remind me later" is now "Ask me next time" and no longer silences the prompt for a day. (With "Update automatically" ticked, the update is still downloaded quietly.)

## 1.3.0
- New: `list_animations` and `get_animation` read saved viewpoint animations (frames, camera position, how far the camera moves and turns between frames, cuts, smoothing, loops). `go_to_viewpoint` switches the view to a saved viewpoint or animation frame. Navisworks does not expose per-frame times, so pacing is judged from travel and turn.

## 1.2.0
New tools for reviewing overlapping elements (all view-only; the model file is never changed):
- `inspect_clash_result`: both elements of one clash result with name, class, path, source file and all properties.
- `focus_clash_result`: select and zoom to both elements.
- `set_clash_item_visibility`, `set_visibility`, `reset_visibility`: hide or show elements so you can decide which one stays visible.
- `set_clash_result_status`: mark a result New, Active, Reviewed, Approved or Resolved.
- Search tools accept several conditions at once (`conditions`, all must match).
- `get_clash_results` now includes the GUID of each element.

## 1.1.4
- Maintenance release. Added a one-click `release.bat` for building and publishing releases. No change for users.

## 1.1.3
- Update check now runs every time Navisworks starts (it was once a day).

## 1.1.2
- Update check now writes clear lines to the log (`%TEMP%\navisbridge.log`): installed version, latest version, and whether the prompt was shown or snoozed. Helps find why an update prompt does not appear.

## 1.1.1
Security hardening (thanks to the reviewer who suggested these).
- The local bridge now always requires a random token, created on first start in `%APPDATA%\AI Connect\bridge.token` and read automatically by the MCP server. Requests from web pages (any `Origin` header), wrong `Host` headers (DNS rebinding) and non-JSON content types are rejected.
- Updater: the installer is hashed again right before it is started with admin rights.
- If port 47800 is already in use, AI Connect now shows a clear message instead of failing silently.
- A request that timed out before Navisworks started it is cancelled, so a retry cannot create duplicates.
- Tools are marked read-only or not, so Claude can run read tools freely and ask before changing anything.
- README: states 2022 to 2027.

## 1.1.0
- Update check on Navisworks start (once a day): asks before updating, verifies the installer SHA-256, installs when Navisworks closes. Optional "update automatically" setting; stronger reminder when a release is over 7 days old. Disable with environment variable AI_CONNECT_NO_UPDATE_CHECK=1.
- Installer waits for Navisworks to close instead of failing; upgrades only the versions already installed.

## 1.0.0
- First release: AI Connect ribbon button, local bridge, 11 tools, MCP server.
- Installer for Navisworks 2022-2027 with in-place upgrade, `check_for_updates` tool, bug report helper.
