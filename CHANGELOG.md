# Changelog

## 1.7.1
- New: `add_animation_cuts` adds pauses (cuts) to an animation, so the camera can hold still. It reads the pauses back to confirm.
- New: `set_animation_options` sets smoothing (None / Sync) and loop on an animation and reads the values back.
- New: `delete_animations` deletes animations by exact name (dry run first).
- `create_walkthrough` has `pauseInsideSeconds` to pause once inside each door.
- `self_test` now checks the pause and smoothing calls and no longer says "viewpoint replace NO" by mistake.

## 1.7.0
- New: `set_camera` puts the camera at an exact point and aims it, and returns where it ended up.
- New: `create_walkthrough` builds an animation that walks through door openings at eye height (or along your own waypoints). Between stops the camera moves in a straight line.

## 1.6.4
- Easier install: the installer installs Node.js LTS automatically if it is missing (winget, else a checked download from nodejs.org), checks .NET Framework 4.8, registers the connector in Claude Desktop for the normal user, and tells you what to do if something cannot be set up.
- Section box now follows the equipment: it is turned to the item's own angle and sized to the item's real sides (`align` item/world, `rotationDeg`). `inspect_item_geometry` shows an item's rotation.
- Section-box viewpoints: the camera is now fitted to the item (it stayed where it was before, so the item was out of view). The result reports the camera position.
- Section box: the box Navisworks stores has a rotation; AI Connect now resets it so the box lines up with the item.
- New: `create_animation` (from saved viewpoints or saved sets).
- `rename_saved_items` report shows the old name correctly.

## 1.6.0
- Hidden items: `start_hidden_scan` / `hidden_scan_status` scan in the background in small steps (Navisworks stays usable). `create_hidden_items_set` does everything in one call (scan, set, item-count check, save reminder) and finishes in the background on huge models.
- Save reminder: tools that create sets or viewpoints now remind the person to save the file (Ctrl+S).
- `bulk_viewpoints`: many section-box viewpoints in one go, from sets or from a property (for example one per family), in batches.
- `rename_saved_items`, `delete_selection_sets`, `delete_viewpoints` by prefix (preview first).
- `export_properties_csv`, `color_by_property` (with legend).
- `set_animation_duration` and `inspect_animation_api` (works only if the Navisworks version exposes a writable duration; otherwise says so).
- `list_timeliner_tasks` (read only, experimental).
- `self_test`: health check inside Navisworks.
- Includes the 1.5.1 section-box tools.

## 1.5.1
- New: `save_item_viewpoint` makes a viewpoint that points exactly at an item: it selects it, cuts a section box around it, zooms in, waits for the view to settle, saves the viewpoint and checks that the saved viewpoint brings the section box back. `set_section_box` and `get_section` set and read the section box. `delete_viewpoints` removes viewpoints by exact name.
- Needs a Box section to be enabled once in Navisworks (Viewpoint > Enable Sectioning > Box) so the box format can be copied.

## 1.5.0
- New tools: `get_item_properties` (read all properties of selected items, set items or search results), `color_items` and `reset_colors` (view-only colour and transparency), `list_models`, `export_clash_report` (CSV file).
- README: fixed a duplicated line in the tool list.

## 1.4.7
- `list_hidden_items` can leave out items already stored in saved sets (`excludeSets`, use "*" for all sets), which makes scans of big models shorter and the new set contain only new items.

## 1.4.6
- The version number is shown on the AI Connect ribbon panel and button tooltip.

## 1.4.5
- Update popup appears ~8 s after Navisworks starts (was 25 s).
- Selection set from GUIDs now looks level by level, so it works on huge models. `allowPartial` lets the hidden-items set use what the scan found so far.

## 1.4.4
- Fix: the hidden-items scan could skip one item at the point where a slice paused. Found in audit.

## 1.4.3
- `list_hidden_items` scans big models in slices (call again until `done`), so it never times out. The set tool reuses the finished scan.

## 1.4.2
- `list_hidden_items` now searches level by level, so hidden items near the top of big models are found in seconds instead of timing out.

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
