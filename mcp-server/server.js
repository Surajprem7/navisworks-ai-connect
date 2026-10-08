#!/usr/bin/env node
// Minimal zero-dependency MCP server (stdio) that forwards tool calls to the NavisBridge plugin
// running inside Navisworks (http://127.0.0.1:47800). Requires Node 18+.

const BRIDGE = process.env.NAVIS_BRIDGE_URL || 'http://127.0.0.1:47800/';
const TOKEN = process.env.NAVIS_BRIDGE_TOKEN || '';
const VERSION = '1.0.0';
const REPO = 'Surajprem7/navisworks-ai-connect';

const str = (description) => ({ type: 'string', description });
const num = (description) => ({ type: 'number', description });
const searchProps = {
  category: str('Property tab/category display name, e.g. "Item" or "Element"'),
  property: str('Property display name, e.g. "Name" or "System Name"'),
  value: str('Value to match'),
  match: { type: 'string', enum: ['equals', 'contains'], description: 'Match mode (default equals)' },
};

const TOOLS = [
  { name: 'check_for_updates', description: 'Check GitHub for a newer AI Connect release than the one installed. Sends no personal data; only asks GitHub for the latest version number.', inputSchema: { type: 'object', properties: {} } },
  { name: 'get_model_info', description: 'Active Navisworks document: file, appended models, selection count, number of selection sets and clash tests.', inputSchema: { type: 'object', properties: {} } },
  { name: 'search_items', description: 'Find model items by a property condition. Returns total count and up to `limit` items.', inputSchema: { type: 'object', properties: { ...searchProps, limit: num('Max items returned (default 50)') }, required: ['category', 'property', 'value'] } },
  { name: 'select_items', description: 'Select (and optionally zoom to) the items matching a property condition in the Navisworks view.', inputSchema: { type: 'object', properties: { ...searchProps, zoom: { type: 'boolean', description: 'Zoom to selection (default true)' } }, required: ['category', 'property', 'value'] } },
  { name: 'list_selection_sets', description: 'List saved Selection/Search Sets.', inputSchema: { type: 'object', properties: {} } },
  { name: 'create_selection_set', description: 'Create a saved Search Set from a property condition.', inputSchema: { type: 'object', properties: { name: str('Name of the new set'), ...searchProps }, required: ['name', 'category', 'property', 'value'] } },
  { name: 'list_viewpoints', description: 'List saved viewpoints.', inputSchema: { type: 'object', properties: {} } },
  { name: 'save_viewpoint', description: 'Save the current view as a named viewpoint.', inputSchema: { type: 'object', properties: { name: str('Viewpoint name') }, required: ['name'] } },
  { name: 'list_clash_tests', description: 'List Clash Detective tests with type, tolerance, status and result count.', inputSchema: { type: 'object', properties: {} } },
  { name: 'create_clash_test', description: 'Create a clash test between two saved selection sets.', inputSchema: { type: 'object', properties: { name: str('Test name'), selection_set_a: str('Name of selection set A'), selection_set_b: str('Name of selection set B'), tolerance: num('Tolerance in model units (default 0)'), type: { type: 'string', enum: ['Hard', 'HardConservative', 'Clearance', 'Duplicate'], description: 'Clash type (default Hard)' } }, required: ['name', 'selection_set_a', 'selection_set_b'] } },
  { name: 'run_clash_test', description: 'Run one clash test by name, or all tests if name is omitted or "*".', inputSchema: { type: 'object', properties: { name: str('Test name, or "*" for all') } } },
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
