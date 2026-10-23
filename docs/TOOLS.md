# Tools reference

Property conditions use the names you see in Navisworks' **Find Items** / **Properties** windows:
`category` = the tab (e.g. `Item`, `Element`), `property` = the property (e.g. `Name`, `System Name`), `value` = text to match.
`match` is `equals` (default) or `contains`.

| Tool | Arguments | What it does |
|---|---|---|
| `get_model_info` | none | File name, appended models, selection count, number of selection sets and clash tests. |
| `search_items` | `category`, `property`, `value`, `match?`, `limit?` (50) | Counts and lists matching items. Does not change the selection. |
| `select_items` | `category`, `property`, `value`, `match?`, `zoom?` (true) | Selects (and zooms to) matching items. |
| `list_selection_sets` | none | Saved Selection/Search sets. |
| `create_selection_set` | `name`, `category`, `property`, `value`, `match?` | Creates a saved Search Set. |
| `list_viewpoints` | none | Saved viewpoints with folder. |
| `save_viewpoint` | `name` | Saves the current view. |
| `list_clash_tests` | none | Clash Detective tests: type, tolerance, status, result count. |
| `create_clash_test` | `name`, `selection_set_a`, `selection_set_b`, `tolerance?` (0), `type?` (`Hard`, `HardConservative`, `Clearance`, `Duplicate`) | Creates a test between two **saved selection sets** (create them first). |
| `run_clash_test` | `name?` (omit or `*` = all) | Runs one or all tests. |
| `get_clash_results` | `name`, `status?`, `limit?` (100) | Results, optionally filtered (New, Active, Reviewed, Approved, Resolved). |
| `get_item_properties` | `source?` (`selected`, `set`, or search by default), `set?`, search conditions, `limit?` (5), `maxProperties?` (80) | Full properties of items: name, class, path, source file, hidden state, every property tab. |
| `color_items` | `color`, `transparency?` (0 to 1), `source?`, `set?`, search conditions | Colours items in the view. View only, the model is not changed. |
| `reset_colors` | none | Removes all colour and transparency overrides. |
| `list_models` | none | Files loaded in the project, with root name and top-level child count. |
| `export_clash_report` | `name`, `status?` | Saves clash results as a CSV in `Documents\AI Connect\reports` (new file each time). |
| `get_section` | none | Reads the section box/planes of the active view (JSON), the model extents and units. |
| `set_section_box` | `source?` (`selected`/`set`/`search`), `set?`, search fields, `margin?` | Cuts a section box around the items. View only. Needs a Box section enabled once in Navisworks. |
| `save_item_viewpoint` | `name`, `source?`, `set?`, search fields, `margin?`, `settleMs?` (1500), `restoreSection?` | Selects the item(s), cuts a section box, zooms in, saves the viewpoint, then checks the saved viewpoint brings the section back. |
| `delete_viewpoints` | `names` (exact names) or `prefix` (previewed first), `dryRun?` | Deletes the listed saved viewpoints only. |
| `start_hidden_scan` | `restart?`, `excludeSets?` | Starts a background scan for hidden items (small steps; Navisworks stays usable). |
| `hidden_scan_status` | none | Progress of the background scan. |
| `create_hidden_items_set` | `name`, `restart?`, `excludeSets?` | One call: scan, make the set of all hidden items, check the count, remind to save. Call again until `status` is `created`. |
| `bulk_viewpoints` | `prefix?`, `sets?` or `groupCategory`+`groupProperty` (+ search/`conditions`), `perGroup?` (`first`/`all`), `maxPerCall?`, `margin?` | Many section-box viewpoints, in batches; existing names are skipped. |
| `rename_saved_items` | `kind` (`viewpoint`/`set`), `renames?` or `find`+`replace`/`addPrefix`, `onlyPrefix?`, `dryRun?` | Renames saved viewpoints or sets. |
| `delete_selection_sets` | `names?` or `prefix?`, `dryRun?` | Deletes sets (prefix is previewed first). |
| `export_properties_csv` | source/search fields, `properties?`, `limit?`, `name?` | CSV of item properties in Documents\AI Connect\exports. |
| `color_by_property` | `groupCategory`, `groupProperty`, source/search fields, `maxGroups?`, `transparency?` | Colours items by a property value and returns a legend (view only). |
| `inspect_animation_api` | `name` | Diagnostic: what Navisworks exposes for one animation. |
| `create_animation` | `name`, `viewpoints?` or `sets?`, `settleMs?` | Creates a viewpoint animation from 2+ saved viewpoints, or by zooming to 2+ saved sets. |
| `set_camera` | `position`, `lookAt?` or `direction?`, `save?` | Puts the camera at an exact point (model units), vertical axis up, and returns where it ended up. Optionally saves the view. |
| `create_walkthrough` | `name`, door search/set/selection or `waypoints?`, `entry?`, `order?`, `maxDoors?`, `eyeHeightM?`, `approachM?`, `insideM?`, `includeCurrentView?`, `keepSection?`, `pauseInsideSeconds?`, `level?`, `levelToleranceM?` | Animation that walks through door openings at eye height (or along your waypoints). Straight lines between stops. Switches the section box off unless `keepSection`. `pauseInsideSeconds` adds a pause once inside each door. Walks one floor at a time: the one with most doors, a floor you name in `level`, or `all`. Floors come from the door's Level name. |
| `add_animation_cuts` | `name`, `cuts?` [{afterFrame, seconds}] or `everyFrameSeconds?` | Adds pauses (cuts) to an animation. Reads the pauses back. |
| `set_animation_options` | `name`, `smoothing?` (None / Sync), `loops?` | Sets smoothing and loop. Reads the values back. Length (Duration) cannot be set by the API. |
| `delete_animations` | `names`, `dryRun?` | Deletes animations by exact name. Preview first. |
| `set_animation_duration` | `name`, `seconds` | Sets the animation length if the version allows it. |
| `list_timeliner_tasks` | `limit?`, `maxDepth?` | Reads the Timeliner task tree (read only, experimental). |
| `self_test` | none | Health check inside Navisworks. |

Tested live: model info, search, viewpoints, selection-set and clash-test listing. Creating and running clash tests is
implemented against the documented API but has had less field testing, so verify the first results in Clash Detective.

## Calling the bridge directly (debugging)

With AI Connect switched on:

```
curl -X POST http://127.0.0.1:47800/ -H "Content-Type: application/json" -d "{\"tool\":\"get_model_info\",\"args\":{}}"
```
Reply: `{"ok":true,"result":{...}}` or `{"ok":false,"error":"..."}`.
