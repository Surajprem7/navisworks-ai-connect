#!/usr/bin/env node
// Minimal zero-dependency MCP server (stdio) that forwards tool calls to the NavisBridge plugin
// running inside Navisworks (http://127.0.0.1:47800). Requires Node 18+.

const BRIDGE = process.env.NAVIS_BRIDGE_URL || 'http://127.0.0.1:47800/';
const fs = require('fs');
const path = require('path');
const VERSION = '1.6.1';

// Shared secret: NAVIS_BRIDGE_TOKEN if set, otherwise the file the Navisworks plugin creates in the user's profile.
function readToken() {
  if (process.env.NAVIS_BRIDGE_TOKEN) return process.env.NAVIS_BRIDGE_TOKEN;
  try {
    const base = process.env.APPDATA || path.join(process.env.HOME || '', '.config');
    return fs.readFileSync(path.join(base, 'AI Connect', 'bridge.token'), 'utf8').trim();
  } catch (e) { return ''; }
}
const REPO = 'Surajprem7/navisworks-ai-connect';

const str = (description) => ({ type: 'string', description });
const num = (description) => ({ type: 'number', description });
const searchProps = {
  category: str('Property tab/category display name, e.g. "Item" or "Element"'),
  property: str('Property display name, e.g. "Name" or "System Name"'),
  value: str('Value to match'),
  match: { type: 'string', enum: ['equals', 'contains'], description: 'Match mode (default equals)' },
};

const conditionsProp = {
  type: 'array',
  description: 'Optional: several conditions that must ALL match (instead of category/property/value). Each item has category, property, value and optional match (equals|contains).',
  items: { type: 'object', properties: searchProps, required: ['category', 'property', 'value'] },
};
const clashRef = { test: str('Clash test name'), result: str('Result name from get_clash_results, e.g. "Clash1"') };

const TOOLS = [
  { name: 'check_for_updates', annotations: { readOnlyHint: true }, description: 'Check GitHub for a newer AI Connect release than the one installed. Sends no personal data; only asks GitHub for the latest version number.', inputSchema: { type: 'object', properties: {} } },
  { name: 'get_model_info', annotations: { readOnlyHint: true }, description: 'Active Navisworks document: file, appended models, selection count, number of selection sets and clash tests.', inputSchema: { type: 'object', properties: {} } },
  { name: 'search_items', annotations: { readOnlyHint: true }, description: 'Find model items by a property condition. Returns total count and up to `limit` items.', inputSchema: { type: 'object', properties: { ...searchProps, conditions: conditionsProp, limit: num('Max items returned (default 50)') }, required: [] } },
  { name: 'select_items', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Select (and optionally zoom to) the items matching a property condition in the Navisworks view.', inputSchema: { type: 'object', properties: { ...searchProps, conditions: conditionsProp, zoom: { type: 'boolean', description: 'Zoom to selection (default true)' } }, required: [] } },
  { name: 'list_selection_sets', annotations: { readOnlyHint: true }, description: 'List saved Selection/Search Sets.', inputSchema: { type: 'object', properties: {} } },
  { name: 'create_selection_set', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Create a saved Search Set from a property condition.', inputSchema: { type: 'object', properties: { name: str('Name of the new set'), ...searchProps, conditions: conditionsProp }, required: ['name'] } },
  { name: 'list_viewpoints', annotations: { readOnlyHint: true }, description: 'List saved viewpoints.', inputSchema: { type: 'object', properties: {} } },
  { name: 'save_viewpoint', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Save the current view as a named viewpoint.', inputSchema: { type: 'object', properties: { name: str('Viewpoint name') }, required: ['name'] } },
  { name: 'list_clash_tests', annotations: { readOnlyHint: true }, description: 'List Clash Detective tests with type, tolerance, status and result count.', inputSchema: { type: 'object', properties: {} } },
  { name: 'create_clash_test', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Create a clash test between two saved selection sets.', inputSchema: { type: 'object', properties: { name: str('Test name'), selection_set_a: str('Name of selection set A'), selection_set_b: str('Name of selection set B'), tolerance: num('Tolerance in model units (default 0)'), type: { type: 'string', enum: ['Hard', 'HardConservative', 'Clearance', 'Duplicate'], description: 'Clash type (default Hard)' } }, required: ['name', 'selection_set_a', 'selection_set_b'] } },
  { name: 'run_clash_test', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Run one clash test by name, or all tests if name is omitted or "*".', inputSchema: { type: 'object', properties: { name: str('Test name, or "*" for all') } } },
  { name: 'get_clash_results', annotations: { readOnlyHint: true }, description: 'Get results of a clash test (optionally filtered by status such as New, Active, Reviewed, Approved, Resolved).', inputSchema: { type: 'object', properties: { name: str('Test name'), status: str('Status filter'), limit: num('Max results (default 100)') }, required: ['name'] } },
  { name: 'inspect_clash_result', annotations: { readOnlyHint: true }, description: 'Show both elements of one clash/overlap result with full details (name, class, path, source file, hidden state, all properties) so the user can decide which one to keep or hide.', inputSchema: { type: 'object', properties: { ...clashRef, max_properties: num('Max properties per element (default 120)') }, required: ['test', 'result'] } },
  { name: 'focus_clash_result', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Select both elements of one clash result in Navisworks and zoom to them.', inputSchema: { type: 'object', properties: clashRef, required: ['test', 'result'] } },
  { name: 'set_clash_item_visibility', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Hide or show one or both elements of a clash result in the view (view only, the model file is not changed). Ask the user which element to hide before calling.', inputSchema: { type: 'object', properties: { ...clashRef, item: { type: 'string', enum: ['item1', 'item2', 'both'], description: 'Which element (default both)' }, hidden: { type: 'boolean', description: 'true to hide (default), false to show' } }, required: ['test', 'result'] } },
  { name: 'set_visibility', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Hide or show all items matching a property condition (view only).', inputSchema: { type: 'object', properties: { ...searchProps, conditions: conditionsProp, hidden: { type: 'boolean', description: 'true to hide (default), false to show' } }, required: [] } },
  { name: 'reset_visibility', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Unhide everything that was hidden in the view.', inputSchema: { type: 'object', properties: {} } },
  { name: 'set_clash_result_status', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Set the status of one clash result: New, Active, Reviewed, Approved or Resolved.', inputSchema: { type: 'object', properties: { ...clashRef, status: { type: 'string', enum: ['New', 'Active', 'Reviewed', 'Approved', 'Resolved'] } }, required: ['test', 'result', 'status'] } },
  { name: 'list_animations', annotations: { readOnlyHint: true }, description: 'List saved viewpoint animations (name, number of frames, cuts, smoothing, loops).', inputSchema: { type: 'object', properties: {} } },
  { name: 'get_animation', annotations: { readOnlyHint: true }, description: 'Read one viewpoint animation: every frame with camera position, and how far the camera moves and turns between frames, plus cuts (pauses) and totals. Navisworks does not expose per-frame times, so use travel and turn to judge pacing and flow.', inputSchema: { type: 'object', properties: { name: str('Animation name') }, required: ['name'] } },
  { name: 'go_to_viewpoint', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Switch the Navisworks view to a saved viewpoint (or animation frame) by name.', inputSchema: { type: 'object', properties: { name: str('Viewpoint name') }, required: ['name'] } },
  { name: 'get_item_properties', annotations: { readOnlyHint: true }, description: 'Read the full properties of model items (selected, from a saved set, or found by a property search): name, class, path, source file, hidden state and every property tab. Returns up to `limit` items.', inputSchema: { type: 'object', properties: { source: { type: 'string', enum: ['selected', 'set', 'search'], description: 'Where the items come from: the current selection, a saved set (give set), or a property search (default)' }, set: str('Set name when source is set'), ...searchProps, conditions: conditionsProp, limit: num('Max items (default 5, max 25)'), maxProperties: num('Max properties per item (default 80)') } } },
  { name: 'color_items', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Colour items in the view (selected, from a saved set, or found by a property search), optionally with transparency 0 to 1. View only: the model file is not changed. Use reset_colors to undo.', inputSchema: { type: 'object', properties: { source: { type: 'string', enum: ['selected', 'set', 'search'], description: 'Where the items come from: the current selection, a saved set (give set), or a property search (default)' }, set: str('Set name when source is set'), ...searchProps, conditions: conditionsProp, color: str('Name (red, green, blue, yellow, orange, purple, cyan, pink, grey, white, black) or #RRGGBB'), transparency: num('0 = solid, 1 = invisible (optional)') }, required: ['color'] } },
  { name: 'reset_colors', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Remove all colour and transparency overrides made in the view.', inputSchema: { type: 'object', properties: {} } },
  { name: 'list_models', annotations: { readOnlyHint: true }, description: 'List the files loaded in the Navisworks project (path, root name, top-level children) and the project title.', inputSchema: { type: 'object', properties: {} } },
  { name: 'get_section', annotations: { readOnlyHint: true }, description: 'Read the section box / section planes of the active view as Navisworks stores them (JSON), the model extents and the units. Use it to check what a section looks like. If it says no section is set, enable Viewpoint > Enable Sectioning > Box in Navisworks first.', inputSchema: { type: 'object', properties: {} } },
  { name: 'set_section_box', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Cut a section box tightly around the chosen items (selected, from a saved set, or found by a property search). View only. Needs a Box section to be enabled once in Navisworks (Viewpoint > Enable Sectioning > Box) so the box format can be copied. Returns what was applied.', inputSchema: { type: 'object', properties: { source: { type: 'string', enum: ['selected', 'set', 'search'], description: 'Where the items come from: the current selection, a saved set (give set), or a property search (default)' }, set: str('Set name when source is set'), ...searchProps, conditions: conditionsProp, margin: num('Extra space around the items in model units (default 15% of the largest size)') } } },
  { name: 'save_item_viewpoint', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Make a saved viewpoint that points exactly at the chosen item(s): select them, cut a section box around them, zoom in, wait for the view to settle, save the viewpoint, then check that the saved viewpoint brings the section box back. Needs a Box section enabled once in Navisworks.', inputSchema: { type: 'object', properties: { name: str('Viewpoint name'), source: { type: 'string', enum: ['selected', 'set', 'search'], description: 'Where the items come from: the current selection, a saved set (give set), or a property search (default)' }, set: str('Set name when source is set'), ...searchProps, conditions: conditionsProp, margin: num('Extra space around the items in model units (default 15% of the largest size)'), settleMs: num('Wait after zooming, in milliseconds (default 1500)'), restoreSection: { type: 'boolean', description: 'Put the section back to how it was after saving (default false: the saved viewpoint stays applied)' } }, required: ['name'] } },
  { name: 'delete_viewpoints', annotations: { readOnlyHint: false, destructiveHint: true }, description: 'Delete saved viewpoints by exact name, or by prefix (a prefix is previewed first; delete only after the person agrees, with dryRun=false). Only the matches are removed. Ask the person before using this.', inputSchema: { type: 'object', properties: { names: { type: 'array', items: { type: 'string' }, description: 'Exact viewpoint names to delete' }, prefix: str('Names starting with this'), dryRun: { type: 'boolean' } } } },
  { name: 'export_clash_report', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Save the results of a clash test as a CSV file in Documents\\AI Connect\\reports (a new file each time, nothing is overwritten). Optionally filter by status.', inputSchema: { type: 'object', properties: { name: str('Clash test name'), status: str('Optional status filter: New, Active, Reviewed, Approved, Resolved') }, required: ['name'] } },
  { name: 'list_hidden_items', annotations: { readOnlyHint: true }, description: 'List the items currently hidden in the Navisworks view (top-level hidden items, with name, class, GUID, path and source file).', inputSchema: { type: 'object', properties: { limit: num('Max items (default 200)'), restart: { type: 'boolean', description: 'Start a new scan instead of continuing' }, excludeSets: { type: 'array', items: { type: 'string' }, description: 'With restart: leave out items already in these saved sets (names, or "*" for all sets)' } } } },
  { name: 'create_selection_set_from_items', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Create one saved selection set from exact items: all currently hidden items (source "hidden"), the current selection in Navisworks (source "selected"), or a list of item GUIDs (source "guids").', inputSchema: { type: 'object', properties: { name: str('Name of the new set'), allowPartial: { type: 'boolean', description: 'With source hidden: use the hidden items found so far even if the scan of a huge model is not finished' }, source: { type: 'string', enum: ['hidden', 'selected', 'guids'], description: 'Where the items come from' }, guids: { type: 'array', items: { type: 'string' }, description: 'Item GUIDs, when source is "guids"' }, limit: num('Max items when source is hidden (default 5000)') }, required: ['name', 'source'] } },
  { name: 'start_hidden_scan', annotations: { readOnlyHint: true }, description: 'Start scanning the model for hidden items in the BACKGROUND (small steps, Navisworks stays usable). Follow it with hidden_scan_status, then call create_hidden_items_set.', inputSchema: { type: 'object', properties: { restart: { type: 'boolean', description: 'Start over (default true). false = continue a paused scan' }, excludeSets: { type: 'array', items: { type: 'string' }, description: 'Leave out items already in these saved sets (names, or "*" for all)' } } } },
  { name: 'hidden_scan_status', annotations: { readOnlyHint: true }, description: 'Progress of the background hidden-items scan: running, done, items scanned, hidden items found.', inputSchema: { type: 'object', properties: {} } },
  { name: 'create_hidden_items_set', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'ONE call that makes a saved set of all hidden items: scans (continues in the background on huge models: call again with the same name until status is "created"), builds the set, checks the item count, and reminds the person to save the file (Ctrl+S). A finished scan older than 10 minutes is repeated.', inputSchema: { type: 'object', properties: { name: str('Name of the new set'), restart: { type: 'boolean', description: 'Force a fresh scan' }, excludeSets: { type: 'array', items: { type: 'string' }, description: 'Leave out items already in these sets (names, or "*")' } }, required: ['name'] } },
  { name: 'bulk_viewpoints', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Make many section-box viewpoints in one go (each points exactly at its item with a section box). Groups come from saved sets (sets) OR from a property (groupCategory + groupProperty) over the items chosen by conditions/source. perGroup "first" = one viewpoint on the first item of each group (default), "all" = box around the whole group. Works in batches (time limit per call): call again with the same arguments to continue; existing viewpoints are skipped. Needs a Box section enabled once in Navisworks.', inputSchema: { type: 'object', properties: { prefix: str('Name prefix, viewpoints are named "<prefix> - <group>"'), sets: { type: 'array', items: { type: 'string' }, description: 'Saved set names, one viewpoint each' }, groupCategory: str('Property category to group by, e.g. "Item"'), groupProperty: str('Property to group by, e.g. "Name"'), source: { type: 'string', enum: ['selected', 'set', 'search'], description: 'Where the items come from (default search)' }, set: str('Set name when source is set'), ...searchProps, conditions: conditionsProp, perGroup: { type: 'string', enum: ['first', 'all'] }, skipExisting: { type: 'boolean', description: 'Skip names that already exist (default true)' }, maxPerCall: num('Viewpoints per call (default 12, max 40)'), margin: num('Extra space around the items in model units'), settleMs: num('Wait after each zoom (default 1200)'), check: { type: 'boolean', description: 'Check the first viewpoint of the call stores the section (default true)' }, restoreSection: { type: 'boolean', description: 'Put the section back afterwards (default false)' } } } },
  { name: 'rename_saved_items', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Rename saved viewpoints or selection sets. Give renames [{from,to}], or find + replace (optionally onlyPrefix), or addPrefix. dryRun true shows the plan only.', inputSchema: { type: 'object', properties: { kind: { type: 'string', enum: ['viewpoint', 'set'] }, renames: { type: 'array', items: { type: 'object', properties: { from: str('Current name'), to: str('New name') }, required: ['from', 'to'] } }, find: str('Text to find in names'), replace: str('Replacement text'), onlyPrefix: str('Only names starting with this'), addPrefix: str('Text to put in front of the names'), dryRun: { type: 'boolean' } }, required: ['kind'] } },
  { name: 'delete_selection_sets', annotations: { readOnlyHint: false, destructiveHint: true }, description: 'Delete saved selection sets by exact name, or by prefix (a prefix is previewed first; delete only after the person agrees, with dryRun=false). Ask the person before using this.', inputSchema: { type: 'object', properties: { names: { type: 'array', items: { type: 'string' } }, prefix: str('Names starting with this'), dryRun: { type: 'boolean' } } } },
  { name: 'export_properties_csv', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Save properties of items (selected, from a saved set, or found by search) as a CSV file in Documents\\AI Connect\\exports. Without `properties` it uses the first 60 properties found.', inputSchema: { type: 'object', properties: { name: str('File name start'), source: { type: 'string', enum: ['selected', 'set', 'search'] }, set: str('Set name when source is set'), ...searchProps, conditions: conditionsProp, properties: { type: 'array', items: { type: 'object', properties: { category: str('Property tab'), property: str('Property name') }, required: ['category', 'property'] }, description: 'Columns to export' }, limit: num('Max rows (default 5000, max 50000)') } } },
  { name: 'color_by_property', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Colour items by the value of a property (for example by System Name or Level) and return a legend. View only; use reset_colors to undo.', inputSchema: { type: 'object', properties: { groupCategory: str('Property tab to colour by'), groupProperty: str('Property to colour by'), source: { type: 'string', enum: ['selected', 'set', 'search'] }, set: str('Set name when source is set'), ...searchProps, conditions: conditionsProp, maxGroups: num('Colours to use, largest groups first (default 12)'), transparency: num('0 = solid, 1 = invisible (optional)') }, required: ['groupCategory', 'groupProperty'] } },
  { name: 'inspect_animation_api', annotations: { readOnlyHint: true }, description: 'Diagnostic: list the properties and methods Navisworks offers for one viewpoint animation (shows whether the length can be changed).', inputSchema: { type: 'object', properties: { name: str('Animation name') }, required: ['name'] } },
  { name: 'set_animation_duration', annotations: { readOnlyHint: false, destructiveHint: false }, description: 'Set the total length of a viewpoint animation in seconds (a longer length plays slower). Works only if this Navisworks version exposes a writable duration; otherwise says so.', inputSchema: { type: 'object', properties: { name: str('Animation name'), seconds: num('New total length in seconds') }, required: ['name', 'seconds'] } },
  { name: 'list_timeliner_tasks', annotations: { readOnlyHint: true }, description: 'Read the Timeliner task tree (names, planned and actual dates). Read only and experimental: depends on the Timeliner library of the installed Navisworks.', inputSchema: { type: 'object', properties: { limit: num('Max tasks (default 300)'), maxDepth: num('Tree depth (default 6)') } } },
  { name: 'self_test', annotations: { readOnlyHint: true }, description: 'Run a quick health check of AI Connect inside Navisworks: version, document, section box calls, rename/delete calls, animation and Timeliner support. Changes nothing.', inputSchema: { type: 'object', properties: {} } },
];

async function checkForUpdates() {
  const res = await fetch('https://api.github.com/repos/' + REPO + '/releases/latest', { headers: { 'User-Agent': 'ai-connect', Accept: 'application/vnd.github+json' }, signal: AbortSignal.timeout(15000) });
  if (res.status === 404) return { installed: VERSION, latest: null, message: 'No release published yet.' };
  if (!res.ok) throw new Error('GitHub returned HTTP ' + res.status);
  const rel = await res.json();
  const latest = String(rel.tag_name || '').replace(/^v/i, '');
  const n = (v) => v.split('.').map((x) => parseInt(x, 10) || 0);
  const a = n(latest), b = n(VERSION);
  let newer = false;
  for (let i = 0; i < 3; i++) { if ((a[i] || 0) !== (b[i] || 0)) { newer = (a[i] || 0) > (b[i] || 0); break; } }
  return { installed: VERSION, latest, update_available: newer, download: rel.html_url, message: newer ? 'A newer version is available. Download AI-Connect-Setup.exe from the release page and run it; it upgrades in place.' : 'You are up to date.' };
}

async function callBridge(tool, args) {
  if (tool === 'check_for_updates') return checkForUpdates();
  const headers = { 'Content-Type': 'application/json' };
  const token = readToken();
  if (token) headers['X-Bridge-Token'] = token;
  let res;
  try {
    res = await fetch(BRIDGE, { method: 'POST', headers, body: JSON.stringify({ tool, args }), signal: AbortSignal.timeout(55000) });
  } catch (e) {
    throw new Error('Cannot reach the Navisworks bridge at ' + BRIDGE + '. In Navisworks open the "AI Connect" ribbon tab and click "AI Connect" (see docs/TROUBLESHOOTING.md). (' + e.message + ')');
  }
  const data = await res.json();
  if (!data.ok) throw new Error(data.error || 'Bridge error');
  return data.result;
}

function send(msg) { process.stdout.write(JSON.stringify(msg) + '\n'); }

async function handle(msg) {
  const { id, method, params } = msg;
  if (id === undefined) return; // notification (e.g. notifications/initialized)
  try {
    if (method === 'initialize') {
      return send({ jsonrpc: '2.0', id, result: { protocolVersion: (params && params.protocolVersion) || '2024-11-05', capabilities: { tools: {} }, serverInfo: { name: 'navisworks', version: VERSION } } });
    }
    if (method === 'ping') return send({ jsonrpc: '2.0', id, result: {} });
    if (method === 'tools/list') return send({ jsonrpc: '2.0', id, result: { tools: TOOLS } });
    if (method === 'tools/call') {
      try {
        const out = await callBridge(params.name, params.arguments || {});
        return send({ jsonrpc: '2.0', id, result: { content: [{ type: 'text', text: JSON.stringify(out, null, 2) }] } });
      } catch (e) {
        return send({ jsonrpc: '2.0', id, result: { isError: true, content: [{ type: 'text', text: String(e.message || e) }] } });
      }
    }
    send({ jsonrpc: '2.0', id, error: { code: -32601, message: 'Method not found: ' + method } });
  } catch (e) {
    send({ jsonrpc: '2.0', id, error: { code: -32603, message: String(e.message || e) } });
  }
}

let buf = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (chunk) => {
  buf += chunk;
  let i;
  while ((i = buf.indexOf('\n')) >= 0) {
    const line = buf.slice(0, i).trim();
    buf = buf.slice(i + 1);
    if (line) { try { handle(JSON.parse(line)); } catch (e) { /* ignore malformed */ } }
  }
});
