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

### Added in 1.2.0

Every tool that takes a property condition also accepts `model?` (only items from appended models whose file name
contains this text, e.g. `ARC`) and `guid?` (one item by instance GUID from `search_items`; can be used on its own).
`search_items` now also returns each item's `model` and `hidden` state.

| Tool | Arguments | What it does |
|---|---|---|
| `zoom_to_items` | condition, `direction?` (`iso`), `padding?` (0.3), `unhide?` (true), `isolate?` (false), `section_box?` (false), `select?` (true) | Moves the camera to the items and fits them in view. |
| `save_item_viewpoint` | `name`, condition, same camera options, `section_box?` (true), `replace?` (true) | Zoom + make visible + section box + save, in one step. Replaces a viewpoint with the same name. |
| `apply_viewpoint` | `name` | Goes to a saved viewpoint. |
| `delete_viewpoint` | `name` or `names[]` | Deletes saved viewpoints by exact name. Never deletes folders. |
| `hide_items` / `unhide_items` | condition | Hide or unhide matching items. |
| `isolate_items` | condition | Show only the matching items (Hide Unselected). |
| `unhide_all` | none | Unhide everything. |
| `set_section_box` | condition + `padding?`, or `min[]`/`max[]` | Section box around items or a given box. |
| `clear_section_box` | none | Sectioning off. |
| `get_selection` | `limit?` (200) | Current selection with the source model of each item and a count per model. |
| `save_selection_as_set` | `name`, `model?` | Saves the current selection as a Selection Set, optionally only items from one model. |
| `capture_view` | `viewpoint?`, `width?`, `height?` | Renders the view (or a saved viewpoint) to a PNG so the assistant can check it. |

`direction` is the side the camera looks from: `iso` (= `iso_se`), `iso_sw`, `iso_ne`, `iso_nw`, `top`, `front`, `back`,
`left`, `right` or `current`. `select_items` now really zooms: older versions called `FocusOnCurrentSelection()`, which
only moves the orbit point, not the camera.

Section box, `capture_view`, `apply_viewpoint` and `delete_viewpoint` use APIs that differ between Navisworks versions and
are called by name; if a version lacks one, the tool returns a clear "not available in this Navisworks version" error.

Tested live: model info, search, viewpoints, selection-set and clash-test listing. Creating and running clash tests is
implemented against the documented API but has had less field testing, so verify the first results in Clash Detective.

## Calling the bridge directly (debugging)

With AI Connect switched on:

```
curl -X POST http://127.0.0.1:47800/ -H "Content-Type: application/json" -d "{\"tool\":\"get_model_info\",\"args\":{}}"
```
Reply: `{"ok":true,"result":{...}}` or `{"ok":false,"error":"..."}`.
