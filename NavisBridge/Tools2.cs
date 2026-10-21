using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.Navisworks.Api;
using Newtonsoft.Json.Linq;

namespace NavisBridge
{
    // Tools added in 1.6.0. Everything here runs on the Navisworks UI thread, like the other tools.
    // Calls whose exact shape differs between Navisworks versions are made by name (reflection) and report
    // what they found, so a missing call gives a clear message instead of a crash.
    public static partial class Tools
    {
        private static JToken RunExtra(string tool, JObject a)
        {
            switch (tool)
            {
                case "start_hidden_scan": return StartHiddenScan(a);
                case "hidden_scan_status": return HiddenScanStatus();
                case "create_hidden_items_set": return CreateHiddenItemsSet(a);
                case "bulk_viewpoints": return BulkViewpoints(a);
                case "rename_saved_items": return RenameSavedItems(a);
                case "delete_selection_sets": return DeleteSelectionSets(a);
                case "export_properties_csv": return ExportPropertiesCsv(a);
                case "color_by_property": return ColorByProperty(a);
                case "inspect_animation_api": return InspectAnimationApi(a);
                case "set_animation_duration": return SetAnimationDuration(a);
                case "list_timeliner_tasks": return ListTimelinerTasks(a);
                case "self_test": return SelfTest();
                default: return null;
            }
        }

        private static string SaveNote()
        {
            return "Selection sets and viewpoints exist only in the open file until it is saved. Press Ctrl+S in Navisworks to keep them.";
        }

        private static string PropValue(ModelItem it, string cat, string prop)
        {
            try
            {
                var p = it.PropertyCategories.FindPropertyByDisplayName(cat, prop);
                if (p == null) return null;
                return p.Value.ToDisplayString();
            }
            catch { return null; }
        }

        private static string SafeFile(string s)
        {
            return string.Concat((s ?? "").Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
        }

        private static string OutDir(string sub)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AI Connect", sub);
            Directory.CreateDirectory(dir);
            return dir;
        }

        // ---------- hidden items: background scan and one-call set ----------

        private static System.Windows.Forms.Timer _bgTimer;

        private static void StopBackgroundScan()
        {
            if (_bgTimer == null) return;
            try { _bgTimer.Stop(); _bgTimer.Dispose(); } catch { }
            _bgTimer = null;
        }

        // Scans a little (about 60 ms) every 120 ms on the UI thread, so Navisworks stays usable while the scan runs.
        private static void StartBackgroundScan()
        {
            if (_bgTimer != null) return;
            _bgTimer = new System.Windows.Forms.Timer { Interval = 120 };
            _bgTimer.Tick += (s, e) =>
            {
                try
                {
                    if (_scan == null || _scan.Done) { StopBackgroundScan(); return; }
                    ScanHidden(false, false, null, 0.06);
                }
                catch { StopBackgroundScan(); }
            };
            _bgTimer.Start();
        }

        private static JObject ScanState()
        {
            var sc = _scan;
            return new JObject
            {
                ["started"] = sc != null,
                ["done"] = sc != null && sc.Done,
                ["running"] = _bgTimer != null,
                ["itemsScanned"] = sc == null ? 0 : sc.Scanned,
                ["hiddenFound"] = sc == null ? 0 : sc.Found.Count,
                ["skippedBecauseInExcludedSets"] = sc == null ? 0 : sc.Skipped
            };
        }

        private static JToken StartHiddenScan(JObject a)
        {
            if (!((bool?)a["restart"] ?? true) && _scan != null && !_scan.Done) { StartBackgroundScan(); return ScanState(); }
            ScanHidden(true, false, a["excludeSets"] as JArray, 0.01);
            StartBackgroundScan();
            var st = ScanState();
            st["note"] = "Scan runs in the background in small steps. Keep working. Call hidden_scan_status to follow it, then create_hidden_items_set.";
            return st;
        }

        private static JToken HiddenScanStatus()
        {
            var st = ScanState();
            if (_scan != null && !_scan.Done && _bgTimer == null) st["note"] = "Scan is paused. Call start_hidden_scan with restart=false to continue it.";
            return st;
        }

        // One call: scan (if needed), make the set, check it, remind to save. Big models: returns "scanning" and finishes in the background.
        private static JToken CreateHiddenItemsSet(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            bool restart = (bool?)a["restart"] ?? false;
            if (_scan != null && _scan.Done && (DateTime.UtcNow - _scan.DoneAt).TotalMinutes > 10) restart = true; // old result: model may have changed
            if (restart || _scan == null) ScanHidden(true, false, a["excludeSets"] as JArray, 25);
            else if (!_scan.Done) ScanHidden(false, false, null, 25);
            var sc = _scan;
            if (!sc.Done)
            {
                StartBackgroundScan();
                var st = ScanState();
                st["status"] = "scanning";
                st["note"] = "Big model: the scan continues in the background. Call create_hidden_items_set again with the same name in a minute or two.";
                return st;
            }
            StopBackgroundScan();
            if (sc.Found.Count == 0) { _scan = null; return new JObject { ["status"] = "nothing hidden", ["itemsScanned"] = sc.Scanned }; }
            var items = new ModelItemCollection();
            foreach (var it in sc.Found) items.Add(it);
            var set = new SelectionSet(items) { DisplayName = name };
            Doc.SelectionSets.AddCopy(set);
            int verified = -1;
            try { var saved = FindSet(name); verified = saved.GetSelectedItems(Doc).Count; } catch { }
            int found = sc.Found.Count, scanned = sc.Scanned;
            _scan = null; // the next call starts a fresh scan
            return new JObject
            {
                ["status"] = "created", ["set"] = name, ["items"] = found, ["itemsInSetAfterCheck"] = verified < 0 ? null : (JToken)verified,
                ["itemsScanned"] = scanned, ["saveReminder"] = SaveNote()
            };
        }

        // ---------- viewpoints in bulk ----------

        private static JToken BulkViewpoints(JObject a)
        {
            var started = DateTime.UtcNow;
            string prefix = (string)a["prefix"] ?? "VP";
            bool perAll = string.Equals((string)a["perGroup"], "all", StringComparison.OrdinalIgnoreCase);
            bool skipExisting = (bool?)a["skipExisting"] ?? true;
            int maxPerCall = Math.Max(1, Math.Min((int?)a["maxPerCall"] ?? 12, 40));
            int settle = (int?)a["settleMs"] ?? 1200;
            double? margin = (double?)a["margin"];

            var groups = new List<KeyValuePair<string, ModelItemCollection>>();
            if (a["sets"] is JArray sets && sets.Count > 0)
            {
                foreach (var n in sets)
                {
                    var col = new ModelItemCollection();
                    col.CopyFrom(FindSet((string)n).GetSelectedItems(Doc));
                    groups.Add(new KeyValuePair<string, ModelItemCollection>((string)n, col));
                }
            }
            else
            {
                string gc = (string)a["groupCategory"], gp = (string)a["groupProperty"];
                if (gc == null || gp == null) throw new ArgumentException("Give either sets (a list of set names) or groupCategory + groupProperty (and conditions to choose the items).");
                var items = ResolveItems(a);
                if (items.Count == 0) throw new ArgumentException("No items found.");
                var map = new Dictionary<string, ModelItemCollection>();
                var order = new List<string>();
                foreach (var it in items)
                {
                    var v = PropValue(it, gc, gp);
                    if (string.IsNullOrEmpty(v)) v = "(none)";
                    if (!map.ContainsKey(v)) { map[v] = new ModelItemCollection(); order.Add(v); }
                    map[v].Add(it);
                }
                foreach (var k in order) groups.Add(new KeyValuePair<string, ModelItemCollection>(k, map[k]));
            }

            string original = null; try { original = GetClipJson(); } catch { }
            var made = new JArray(); var failed = new JArray();
            int skipped = 0, remaining = 0; JToken firstCheck = null; bool stopped = false;
            foreach (var g in groups)
            {
                string vpName = prefix + " - " + g.Key;
                if (skipExisting && FindViewpointByName(Doc.SavedViewpoints.Value, vpName) != null) { skipped++; continue; }
                if (stopped || made.Count >= maxPerCall || Expired(started, 32)) { stopped = true; remaining++; continue; }
                ModelItemCollection use = g.Value;
                if (!perAll) { use = new ModelItemCollection(); use.Add(g.Value[0]); }
                try
                {
                    var r = SaveItemVp(vpName, use, margin, settle, made.Count == 0 && ((bool?)a["check"] ?? true), false);
                    if (made.Count == 0) firstCheck = r["viewpointStoresSection"];
                    made.Add(vpName);
                }
                catch (Exception e) { failed.Add(new JObject { ["name"] = vpName, ["error"] = e.Message }); }
            }
            if (((bool?)a["restoreSection"] ?? false) && original != null) { try { SetClipJson(original); } catch { } }
            return new JObject
            {
                ["created"] = made, ["failed"] = failed, ["skippedBecauseAlreadyThere"] = skipped, ["remaining"] = remaining,
                ["done"] = remaining == 0, ["viewpointStoresSection"] = firstCheck,
                ["note"] = remaining > 0 ? "Not finished (time limit per call). Call again with the same arguments; viewpoints that already exist are skipped." : null,
                ["saveReminder"] = SaveNote()
            };
        }

        // ---------- rename and delete saved items ----------

        private static object SavedColl(bool viewpoint) { return viewpoint ? (object)Doc.SavedViewpoints : Doc.SelectionSets; }

        private static MethodInfo FindMethod(object target, string name, int argCount)
        {
            return target.GetType().GetMethods().FirstOrDefault(m => m.Name == name && m.GetParameters().Length == argCount);
        }

        private static List<SavedItem> LeafItems(bool viewpoint)
        {
            var list = new List<SavedItem>();
            Walk(viewpoint ? Doc.SavedViewpoints.Value : Doc.SelectionSets.Value, "", (s, p) => list.Add(s));
            return list;
        }

        private static JToken RenameSavedItems(JObject a)
        {
            string kind = ((string)a["kind"] ?? "").ToLowerInvariant();
            if (kind != "viewpoint" && kind != "set") throw new ArgumentException("kind must be viewpoint or set");
            bool vp = kind == "viewpoint";
            bool dry = (bool?)a["dryRun"] ?? false;
            var plan = new List<KeyValuePair<SavedItem, string>>();
            var all = LeafItems(vp);
            if (a["renames"] is JArray rn && rn.Count > 0)
            {
                foreach (var r in rn)
                {
                    string from = (string)r["from"], to = (string)r["to"];
                    var item = all.FirstOrDefault(x => string.Equals(x.DisplayName, from, StringComparison.OrdinalIgnoreCase));
                    if (item == null) throw new ArgumentException("Not found: " + from);
                    if (string.IsNullOrWhiteSpace(to)) throw new ArgumentException("Empty new name for " + from);
                    plan.Add(new KeyValuePair<SavedItem, string>(item, to));
                }
            }
            else
            {
                string find = (string)a["find"]; string repl = (string)a["replace"] ?? "";
                string onlyPrefix = (string)a["onlyPrefix"]; string addPrefix = (string)a["addPrefix"];
                if (string.IsNullOrEmpty(find) && string.IsNullOrEmpty(addPrefix)) throw new ArgumentException("Give renames (from/to list), or find + replace, or addPrefix.");
                foreach (var x in all)
                {
                    string n = x.DisplayName ?? "";
                    if (!string.IsNullOrEmpty(onlyPrefix) && !n.StartsWith(onlyPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    string nn = n;
                    if (!string.IsNullOrEmpty(find)) { int i = nn.IndexOf(find, StringComparison.OrdinalIgnoreCase); if (i < 0) continue; nn = nn.Substring(0, i) + repl + nn.Substring(i + find.Length); }
                    if (!string.IsNullOrEmpty(addPrefix)) nn = addPrefix + nn;
                    if (nn != n && nn.Trim().Length > 0) plan.Add(new KeyValuePair<SavedItem, string>(x, nn));
                }
            }
            if (plan.Count > 300) throw new ArgumentException("More than 300 renames in one call (" + plan.Count + "). Narrow it with onlyPrefix.");
            var done = new JArray();
            if (!dry && plan.Count > 0)
            {
                var coll = SavedColl(vp);
                var m = FindMethod(coll, "EditDisplayName", 2);
                if (m == null) throw new InvalidOperationException("This Navisworks version has no EditDisplayName call, so renaming from AI Connect is not possible here.");
                foreach (var kv in plan) m.Invoke(coll, new object[] { kv.Key, kv.Value });
            }
            foreach (var kv in plan) done.Add(new JObject { ["from"] = kv.Key.DisplayName, ["to"] = kv.Value });
            return new JObject { ["dryRun"] = dry, ["renamed"] = dry ? 0 : plan.Count, ["plan"] = done, ["saveReminder"] = SaveNote() };
        }

        private static JToken DeleteSelectionSets(JObject a)
        {
            var names = (a["names"] as JArray)?.Select(x => (string)x).Where(x => !string.IsNullOrEmpty(x)).ToList();
            string prefix = (string)a["prefix"];
            bool dry = (bool?)a["dryRun"] ?? !string.IsNullOrEmpty(prefix); // a prefix match is previewed first unless dryRun=false
            var all = LeafItems(false);
            var targets = new List<SavedItem>(); var missing = new JArray();
            if (names != null && names.Count > 0)
            {
                foreach (var n in names)
                {
                    var it = all.FirstOrDefault(x => string.Equals(x.DisplayName, n, StringComparison.OrdinalIgnoreCase));
                    if (it == null) missing.Add(n); else targets.Add(it);
                }
            }
            else if (!string.IsNullOrEmpty(prefix))
                targets.AddRange(all.Where(x => (x.DisplayName ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
            else throw new ArgumentException("names (exact set names) or prefix is required");
            var listed = new JArray(targets.Select(t => (JToken)t.DisplayName));
            if (dry) return new JObject { ["dryRun"] = true, ["wouldDelete"] = listed, ["notFound"] = missing, ["note"] = "Nothing deleted. Ask the person, then call again with dryRun=false." };
            var coll = SavedColl(false);
            var m = FindMethod(coll, "Remove", 1);
            if (m == null) throw new InvalidOperationException("This Navisworks version has no Remove call for selection sets.");
            foreach (var t in targets) m.Invoke(coll, new object[] { t });
            return new JObject { ["removed"] = listed, ["notFound"] = missing, ["saveReminder"] = SaveNote() };
        }

        // ---------- properties to CSV, colour by property ----------

        private static JToken ExportPropertiesCsv(JObject a)
        {
            var started = DateTime.UtcNow;
            var items = ResolveItems(a);
            if (items.Count == 0) throw new ArgumentException("No items found.");
            int limit = Math.Max(1, Math.Min((int?)a["limit"] ?? 5000, 50000));
            var cols = new List<string[]>();
            if (a["properties"] is JArray pa && pa.Count > 0)
                foreach (var p in pa) cols.Add(new[] { (string)p["category"], (string)p["property"] });
            else
            {
                var seen = new HashSet<string>();
                foreach (var it in items.Take(100))
                    foreach (PropertyCategory pc in it.PropertyCategories)
                        foreach (DataProperty p in pc.Properties)
                        {
                            if (cols.Count >= 60) break;
                            if (seen.Add(pc.DisplayName + "\u0001" + p.DisplayName)) cols.Add(new[] { pc.DisplayName, p.DisplayName });
                        }
            }
            var sb = new StringBuilder();
            sb.Append("Name,Class,GUID,Path,Hidden");
            foreach (var c in cols) sb.Append(',').Append(Csv(c[0] + ": " + c[1]));
            sb.AppendLine();
            int rows = 0; bool truncated = false;
            foreach (var it in items)
            {
                if (rows >= limit) { truncated = true; break; }
                if ((rows & 31) == 0 && Expired(started, 35)) { truncated = true; break; }
                sb.Append(Csv(it.DisplayName)).Append(',').Append(Csv(it.ClassDisplayName)).Append(',').Append(Csv(it.InstanceGuid.ToString()))
                  .Append(',').Append(Csv(PathOf(it))).Append(',').Append(it.IsHidden ? "yes" : "no");
                foreach (var c in cols) sb.Append(',').Append(Csv(PropValue(it, c[0], c[1])));
                sb.AppendLine();
                rows++;
            }
            string file = Path.Combine(OutDir("exports"), SafeFile((string)a["name"] ?? "properties") + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
            return new JObject { ["file"] = file, ["rows"] = rows, ["columns"] = cols.Count + 5, ["itemsMatched"] = items.Count, ["truncated"] = truncated };
        }

        private static readonly string[] Palette = { "#E53935", "#1E88E5", "#43A047", "#FB8C00", "#8E24AA", "#00ACC1", "#FDD835", "#EC407A", "#6D4C41", "#7CB342", "#3949AB", "#9E9E9E" };

        private static JToken ColorByProperty(JObject a)
        {
            string gc = (string)a["groupCategory"], gp = (string)a["groupProperty"];
            if (gc == null || gp == null) throw new ArgumentException("groupCategory and groupProperty are required (the property to colour by). Use conditions to choose which items.");
            var items = ResolveItems(a);
            if (items.Count == 0) throw new ArgumentException("No items found.");
            int maxGroups = Math.Max(1, Math.Min((int?)a["maxGroups"] ?? 12, Palette.Length));
            var map = new Dictionary<string, ModelItemCollection>();
            foreach (var it in items)
            {
                var v = PropValue(it, gc, gp);
                if (string.IsNullOrEmpty(v)) v = "(none)";
                if (!map.ContainsKey(v)) map[v] = new ModelItemCollection();
                map[v].Add(it);
            }
            var ordered = map.OrderByDescending(kv => kv.Value.Count).ToList();
            var legend = new JArray(); int idx = 0, left = 0;
            double? tr = (double?)a["transparency"];
            foreach (var kv in ordered)
            {
                if (idx >= maxGroups) { left += kv.Value.Count; continue; }
                string hex = Palette[idx++];
                Doc.Models.OverrideTemporaryColor(kv.Value, ParseColor(hex));
                if (tr.HasValue) Doc.Models.OverrideTemporaryTransparency(kv.Value, Math.Max(0, Math.Min(1, tr.Value)));
                legend.Add(new JObject { ["value"] = kv.Key, ["color"] = hex, ["items"] = kv.Value.Count });
            }
            return new JObject { ["legend"] = legend, ["groups"] = ordered.Count, ["itemsNotColoured"] = left, ["note"] = "View only. Use reset_colors to undo." };
        }

        // ---------- animation speed (diagnostic first: the exact API differs by version) ----------

        private static SavedViewpointAnimation FindAnim(string name)
        {
            var found = new List<KeyValuePair<string, SavedViewpointAnimation>>();
            FindAnims(Doc.SavedViewpoints.Value, "", found);
            foreach (var kv in found) if (string.Equals(kv.Value.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            throw new ArgumentException("Animation not found: " + name);
        }

        private static JArray PublicProps(object o)
        {
            var arr = new JArray();
            foreach (var p in o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                string v = null;
                try { var x = p.GetValue(o, null); v = x == null ? null : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture); if (v != null && v.Length > 80) v = v.Substring(0, 80); } catch { v = "(unreadable)"; }
                arr.Add(new JObject { ["name"] = p.Name, ["type"] = p.PropertyType.Name, ["canWrite"] = p.CanWrite && p.GetSetMethod() != null, ["value"] = v });
            }
            return arr;
        }

        private static JToken InspectAnimationApi(JObject a)
        {
            var an = FindAnim((string)a["name"] ?? throw new ArgumentException("name is required"));
            var o = new JObject { ["animation"] = PublicProps(an) };
            foreach (SavedItem ch in an.Children)
            {
                if (ch is SavedViewpointAnimationCut) { o["cut"] = PublicProps(ch); break; }
            }
            var m = an.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(x => !x.IsSpecialName).Select(x => (JToken)x.Name).Distinct().ToList();
            o["methods"] = new JArray(m);
            o["collectionMethods"] = new JArray(Doc.SavedViewpoints.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(x => !x.IsSpecialName).Select(x => (JToken)x.Name).Distinct().ToList());
            return o;
        }

        private static JToken SetAnimationDuration(JObject a)
        {
            var an = FindAnim((string)a["name"] ?? throw new ArgumentException("name is required"));
            double? seconds = (double?)a["seconds"];
            if (!seconds.HasValue || seconds.Value <= 0) throw new ArgumentException("seconds (new total length, more than 0) is required");
            string[] candidates = { "Duration", "AnimationDuration", "Length", "TotalTime", "TotalDuration", "Time" };
            // work on a copy and put it back, because saved items are normally not edited in place
            SavedViewpointAnimation copy = null;
            try { copy = an.CreateCopy() as SavedViewpointAnimation; } catch { }
            var target = copy ?? an;
            PropertyInfo prop = null;
            foreach (var c in candidates)
            {
                var p = target.GetType().GetProperty(c, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanWrite && p.GetSetMethod() != null) { prop = p; break; }
            }
            if (prop == null)
                throw new InvalidOperationException("This Navisworks version does not let AI Connect change the animation length (no writable duration found). Run inspect_animation_api to see what exists. Manual way: Saved Viewpoints, right-click the animation > Edit, then set Duration.");
            object val;
            if (prop.PropertyType == typeof(TimeSpan)) val = TimeSpan.FromSeconds(seconds.Value);
            else val = Convert.ChangeType(seconds.Value, prop.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
            string before = null; try { before = Convert.ToString(prop.GetValue(target, null), System.Globalization.CultureInfo.InvariantCulture); } catch { }
            prop.SetValue(target, val, null);
            bool replaced = false;
            if (copy != null)
            {
                var coll = SavedColl(true);
                var m = FindMethod(coll, "Replace", 2);
                if (m == null) throw new InvalidOperationException("Duration was set on a copy but this version has no Replace call to put it back.");
                m.Invoke(coll, new object[] { an, copy });
                replaced = true;
            }
            return new JObject { ["animation"] = an.DisplayName, ["property"] = prop.Name, ["before"] = before, ["after"] = seconds.Value, ["replacedInFile"] = replaced, ["saveReminder"] = SaveNote() };
        }

        // ---------- Timeliner (read only, by name because it lives in a separate Navisworks library) ----------

        private static object GetTimelinerObject(out string how)
        {
            Assembly asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "Autodesk.Navisworks.Timeliner");
            if (asm == null) { try { asm = Assembly.Load("Autodesk.Navisworks.Timeliner"); } catch { } }
            if (asm == null) throw new InvalidOperationException("The Navisworks Timeliner library was not found. Timeliner may not be available in this edition (Simulate or Manage is needed).");
            Type[] types; try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            foreach (var t in types)
            {
                if (!(t.IsAbstract && t.IsSealed)) continue;
                var m = t.GetMethod("GetTimeliner", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Document) }, null);
                if (m != null) { how = t.FullName + ".GetTimeliner"; return m.Invoke(null, new object[] { Doc }); }
            }
            var dt = types.FirstOrDefault(t => t.Name == "DocumentTimeliner");
            if (dt != null)
            {
                var ctor = dt.GetConstructor(new[] { typeof(Document) });
                if (ctor != null) { how = "new DocumentTimeliner"; return ctor.Invoke(new object[] { Doc }); }
            }
            throw new InvalidOperationException("Timeliner library found, but no way to open it from the document. Types: " + string.Join(", ", types.Where(t => t.IsPublic).Select(t => t.Name).Take(40)));
        }

        private static string DateText(object v)
        {
            return v == null ? null : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void ReadTasks(object node, int depth, int maxDepth, JArray into, ref int count, int max)
        {
            var children = node.GetType().GetProperty("Children", BindingFlags.Public | BindingFlags.Instance);
            var en = children == null ? null : children.GetValue(node, null) as System.Collections.IEnumerable;
            if (en == null) return;
            foreach (var ch in en)
            {
                if (count >= max) return;
                count++;
                var o = new JObject { ["depth"] = depth };
                foreach (var pn in new[] { "DisplayName", "PlannedStartDate", "PlannedEndDate", "ActualStartDate", "ActualEndDate", "SimulationTaskTypeName", "Status", "Progress", "Name" })
                {
                    var p = ch.GetType().GetProperty(pn, BindingFlags.Public | BindingFlags.Instance);
                    if (p == null) continue;
                    try { o[pn] = DateText(p.GetValue(ch, null)); } catch { }
                }
                into.Add(o);
                if (depth < maxDepth) ReadTasks(ch, depth + 1, maxDepth, into, ref count, max);
            }
        }

        private static JToken ListTimelinerTasks(JObject a)
        {
            string how;
            var tl = GetTimelinerObject(out how);
            var rootProp = tl.GetType().GetProperty("TasksRoot", BindingFlags.Public | BindingFlags.Instance);
            if (rootProp == null)
                throw new InvalidOperationException("Timeliner opened (" + how + ") but has no TasksRoot. Properties: " + string.Join(", ", tl.GetType().GetProperties().Select(p => p.Name)));
            var root = rootProp.GetValue(tl, null);
            var arr = new JArray(); int count = 0; int max = Math.Max(1, Math.Min((int?)a["limit"] ?? 300, 2000));
            ReadTasks(root, 0, (int?)a["maxDepth"] ?? 6, arr, ref count, max);
            return new JObject { ["opened"] = how, ["tasks"] = arr, ["returned"] = arr.Count, ["note"] = "Read only. Experimental: depends on the Timeliner library of your Navisworks version." };
        }

        // ---------- self test ----------

        private static void Check(JArray into, string name, Func<string> f)
        {
            try { var r = f(); into.Add(new JObject { ["check"] = name, ["ok"] = true, ["detail"] = r }); }
            catch (Exception e) { into.Add(new JObject { ["check"] = name, ["ok"] = false, ["detail"] = e.Message }); }
        }

        private static JToken SelfTest()
        {
            var r = new JArray();
            Check(r, "AI Connect version", () => BridgePlugin.Ver);
            Check(r, "document open", () => Doc.Title + " (" + Doc.Models.Count + " models, units " + Doc.Units + ")");
            Check(r, "search works", () => { var s = new Search(); s.Selection.SelectAll(); return "ok"; });
            Check(r, "selection sets readable", () => CountSaved(Doc.SelectionSets.Value) + " sets");
            Check(r, "viewpoints readable", () => CountSaved(Doc.SavedViewpoints.Value) + " viewpoints");
            Check(r, "clash module", () => AllTests().Count + " clash tests");
            Check(r, "section box: GetClippingPlanes", () => { var s = GetClipJson(); return string.IsNullOrWhiteSpace(s) ? "no section is active (enable Sectioning > Box once)" : "section JSON length " + s.Length; });
            Check(r, "section box: TrySetClippingPlanes", () => { if (Doc.ActiveView.GetType().GetMethod("TrySetClippingPlanes") == null) throw new Exception("missing"); return "present"; });
            Check(r, "rename calls", () => (FindMethod(Doc.SavedViewpoints, "EditDisplayName", 2) != null ? "viewpoints yes" : "viewpoints NO") + ", " + (FindMethod(Doc.SelectionSets, "EditDisplayName", 2) != null ? "sets yes" : "sets NO"));
            Check(r, "remove / replace calls", () => (FindMethod(Doc.SelectionSets, "Remove", 1) != null ? "set remove yes" : "set remove NO") + ", " + (FindMethod(Doc.SavedViewpoints, "Replace", 2) != null ? "viewpoint replace yes" : "viewpoint replace NO"));
            Check(r, "animation length property", () =>
            {
                var t = typeof(SavedViewpointAnimation);
                var names = new[] { "Duration", "AnimationDuration", "Length", "TotalTime", "TotalDuration", "Time" }.Where(n => t.GetProperty(n) != null).ToArray();
                return names.Length == 0 ? "none found (set_animation_duration will say so)" : string.Join(", ", names);
            });
            Check(r, "Timeliner library", () => { string how; GetTimelinerObject(out how); return how; });
            Check(r, "reports folder writable", () => { var d = OutDir("exports"); var f = Path.Combine(d, ".write-test"); File.WriteAllText(f, "x"); File.Delete(f); return d; });
            int bad = r.Count(x => !(bool)x["ok"]);
            return new JObject { ["failedChecks"] = bad, ["checks"] = r, ["note"] = "A failed check only means that one tool may not work on this Navisworks version or file; the rest still do." };
        }
    }
}
