# Changelog

## 1.1.0
- Update check on Navisworks start (once a day): asks before updating, verifies the installer SHA-256, installs when Navisworks closes. Optional "update automatically" setting; stronger reminder when a release is over 7 days old. Disable with environment variable AI_CONNECT_NO_UPDATE_CHECK=1.
- Installer waits for Navisworks to close instead of failing; upgrades only the versions already installed.

## 1.0.0
- First release: AI Connect ribbon button, local bridge, 11 tools, MCP server.
- Installer for Navisworks 2022-2027 with in-place upgrade, `check_for_updates` tool, bug report helper.
