#!/usr/bin/env node
// Minimal zero-dependency MCP server (stdio) that forwards tool calls to the NavisBridge plugin
// running inside Navisworks (http://127.0.0.1:47800). Requires Node 18+.

const BRIDGE = process.env.NAVIS_BRIDGE_URL || 'http://127.0.0.1:47800/';
const TOKEN = process.env.NAVIS_BRIDGE_TOKEN || '';
const VERSION = '1.2.0';
const REPO = 'Surajprem7/navisworks-ai-connect';

const str = (description) => ({ type: 'string', description });
const num = (description) => ({ type: 'number', description });
const searchProps = {
  category: str('Property tab/category display name, e.g. "Item" or "Element"'),
  property: str('Property display name, e.g. "Name" or "System Name"'),
  value: str('Value to match'),
  match: { type: 'string', enum: ['equals', 'contains'], description: 'Match mode (default equals)' },
};
const filterProps = {
  model: str('Only items from appended models whose file name contains this text, e.g. "ARC" or "MEC"'),
  guid: str('Only the item with this instance GUID (from search_items). Can be used on its own.'),
};
const cameraProps = {
  direction: { type: 'string', enum: ['iso', 'iso_se', 'iso_sw', 'iso_ne', 'iso_nw', 'top', 'front', 'back', 'left', 'right', 'current'], description: 'Side the camera looks from (default iso = from south-east, above)' },
  padding: num('Extra space around the items as a fraction of their largest size (default 0.3)'),
  unhide: { type: 'boolean', description: 'Make the items visible first (default true)' },
  isolate: { type: 'boolean', description: 'Hide everything else (default false)' },
};

const TOOLS = [
  { name: 'check_for_updates', description: 'Check GitHub for a newer AI Connect release than the one installed. Sends no personal data; only asks GitHub for the latest version number.', inputSchema: { type: 'object', properties: {} } },
  { name: 'get_model_info', description: 'Active Navisworks document: file, appended models, selection count, number of selection sets and clash tests.', inputSchema: { type: 'object', properties: {} } },
  { name: 'search_items', description: 'Find model items by a property condition. Returns total count and up to `limit` items.', inputSchema: { type: 'object', properties: { ...searchProps, ...filterProps, limit: num('Max items returned (default 50)') }, required: ['category', 'property', 'value'] } },
  { name: 'select_items', description: 'Select (and optionally zoom to) the items matching a property condition in the Navisworks view.', inputSchema: { type: 'object', properties: { ...searchProps, ...filterProps, zoom: { type: 'boolean', description: 'Zoom to selection (default true)' }, direction: { type: 'string', description: 'Camera side when zooming (default current)' } }, required: ['category', 'property', 'value'] } },
  { name: 'list_selection_sets', description: 'List saved Selection/Search Sets.', inputSchema: { type: 'object', properties: {} } },
  { name: 'create_selection_set', description: 'Create a saved Search Set from a property condition.', inputSchema: { type: 'object', properties: { name: str('Name of the new set'), ...searchProps }, required: ['name', 'category', 'property', 'value'] } },
  { name: 'list_viewpoints', description: 'List saved viewpoints.', inputSchema: { type: 'object', properties: {} } },
  { name: 'save_viewpoint', description: 'Save the current view as a named viewpoint.', inputSchema: { type: 'object', properties: { name: str('Viewpoint name') }, required: ['name'] } },
  { name: 'list_clash_tests', description: 'List Clash Detective tests with type, tolerance, status and result count.', inputSchema: { type: 'object', properties: {} } },
  { name: 'create_clash_test', description: 'Create a clash test between two saved selection sets.', inputSchema: { type: 'object', properties: { name: str('Test name'), selection_set_a: str('Name of selection set A'), selection_set_b: str('Name of selection set B'), tolerance: num('Tolerance in model units (default 0)'), type: { type: 'string', enum: ['Hard', 'HardConservative', 'Clearance', 'Duplicate'], description: 'Clash type (default Hard)' } }, required: ['name', 'selection_set_a', 'selection_set_b'] } },
  { name: 'run_clash_test', description: 'Run one clash test by name, or all tests if name is omitted or "*".', inputSchema: { type: 'object', properties: { name: str('Test name, or "*" for all') } } },
  // ---- 1.2.0: camera, visibility, viewpoints, selection ----
  { name: 'zoom_to_items', description: 'Point the camera at the items matching a condition and fit them in view. Makes them visible (unhide) by default; optional isolate (hide everything else) and section box.', inputSchema: { type: 'object', properties: { ...searchProps, ...filterProps, ...cameraProps, section_box: { type: 'boolean', description: 'Put a section box around the items (default false)' }, select: { type: 'boolean', description: 'Also select them (default true)' } } } },
  { name: 'save_item_viewpoint', description: 'One step: zoom to the matching item(s), make them visible, put a section box around them (default) and save the view as a named viewpoint. Use guid to target one item from search_items. Replaces an existing viewpoint of the same name by default.', inputSchema: { type: 'object', properties: { name: str('Viewpoint name'), ...searchProps, ...filterProps, ...cameraProps, section_box: { type: 'boolean', description: 'Section box around the items so walls/roofs do not block the view (default true)' }, replace: { type: 'boolean', description: 'Delete existing viewpoints with the same name first (default true)' } }, required: ['name'] } },
  { name: 'apply_viewpoint', description: 'Go to (apply) a saved viewpoint.', inputSchema: { type: 'object', properties: { name: str('Viewpoint name') }, required: ['name'] } },
  { name: 'delete_viewpoint', description: 'Delete saved viewpoints by exact name (case-insensitive). Folders are never deleted.', inputSchema: { type: 'object', properties: { name: str('Viewpoint name'), names: { type: 'array', items: { type: 'string' }, description: 'Several viewpoint names' } } } },
  { name: 'hide_items', description: 'Hide the items matching a condition.', inputSchema: { type: 'object', properties: { ...searchProps, ...filterProps } } },
  { name: 'unhide_items', description: 'Unhide the items matching a condition (and their parents/children).', inputSchema: { type: 'object', properties: { ...searchProps, ...filterProps } } },
  { name: 'isolate_items', description: 'Show only the matching items (like Hide Unselected). Undo with unhide_all.', inputSchema: { type: 'object', properties: { ...searchProps, ...filterProps } } },
  { name: 'unhide_all', description: 'Unhide everything (like Unhide All).', inputSchema: { type: 'object', properties: {} } },
  { name: 'set_section_box', description: 'Section box around matching items (with padding) or an explicit min/max box in model units.', inputSchema: { type: 'object', properties: { ...searchProps, ...filterProps, padding: num('Extra space around the items as a fraction of their largest size (default 0.3)'), min: { type: 'array', items: { type: 'number' }, description: '[x,y,z]' }, max: { type: 'array', items: { type: 'number' }, description: '[x,y,z]' } } } },
  { name: 'clear_section_box', description: 'Turn sectioning off.', inputSchema: { type: 'object', properties: {} } },
  { name: 'get_selection', description: 'List the currently selected items with the model each comes from, plus a count per model.', inputSchema: { type: 'object', properties: { limit: num('Max items listed (default 200)') } } },
  { name: 'save_selection_as_set', description: 'Save the current selection as a fixed Selection Set, optionally keeping only items from models whose file name contains `model` (e.g. "ARC").', inputSchema: { type: 'object', properties: { name: str('Set name'), model: str('Only items from models whose file name contains this text') }, required: ['name'] } },
  { name: 'capture_view', description: 'Render the current view, or a saved viewpoint (applied first), to an image so you can check what it shows.', inputSchema: { type: 'object', properties: { viewpoint: str('Saved viewpoint to apply first (optional)'), width: num('Pixels (default 960)'), height: num('Pixels (default 600)') } } },
  { name: 'get_clash_results', description: 'Get results of a clash test (optionally filtered by status such as New, Active, Reviewed, Approved, Resolved).', inputSchema: { type: 'object', properties: { name: str('Test name'), status: str('Status filter'), limit: num('Max results (default 100)') }, required: ['name'] } },
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
  if (TOKEN) headers['X-Bridge-Token'] = TOKEN;
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
        if (out && typeof out.imageBase64 === 'string') {
          const { imageBase64, mimeType, ...meta } = out;
          return send({ jsonrpc: '2.0', id, result: { content: [{ type: 'image', data: imageBase64, mimeType: mimeType || 'image/png' }, { type: 'text', text: JSON.stringify(meta) }] } });
        }
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
