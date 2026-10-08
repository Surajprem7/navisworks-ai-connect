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

Tested live: model info, search, viewpoints, selection-set and clash-test listing. Creating and running clash tests is
implemented against the documented API but has had less field testing, so verify the first results in Clash Detective.

## Calling the bridge directly (debugging)

With AI Connect switched on:

```
curl -X POST http://127.0.0.1:47800/ -H "Content-Type: application/json" -d "{\"tool\":\"get_model_info\",\"args\":{}}"
```
Reply: `{"ok":true,"result":{...}}` or `{"ok":false,"error":"..."}`.
