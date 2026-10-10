# Changelog

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
