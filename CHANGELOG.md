# Changelog

## 1.2.0
- Fix: `select_items` with zoom now moves the camera (it only set the orbit focus point before).
- New tools: `zoom_to_items`, `save_item_viewpoint`, `apply_viewpoint`, `delete_viewpoint`, `hide_items`, `unhide_items`,
  `isolate_items`, `unhide_all`, `set_section_box`, `clear_section_box`, `get_selection`, `save_selection_as_set`, `capture_view`.
- Property conditions accept `model` (filter by source model file) and `guid` (one item). `search_items` returns `model` and `hidden`.
- `capture_view` returns an image to the assistant so it can check a view before saving it.

## 1.1.0
- Update check on Navisworks start (once a day): asks before updating, verifies the installer SHA-256, installs when Navisworks closes. Optional "update automatically" setting; stronger reminder when a release is over 7 days old. Disable with environment variable AI_CONNECT_NO_UPDATE_CHECK=1.
- Installer waits for Navisworks to close instead of failing; upgrades only the versions already installed.

## 1.0.0
- First release: AI Connect ribbon button, local bridge, 11 tools, MCP server.
- Installer for Navisworks 2022-2027 with in-place upgrade, `check_for_updates` tool, bug report helper.
