using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Clash;
using Newtonsoft.Json.Linq;

namespace NavisBridge
{
    /// <summary>All tools run on the Navisworks UI thread.</summary>
    public static class Tools
    {
        public static JToken Run(string tool, JObject a)
        {
            switch (tool)
            {
                case "get_model_info": return GetModelInfo();
                case "search_items": return SearchItems(a);
                case "select_items": return SelectItems(a);
                case "list_selection_sets": return ListSelectionSets();
                case "create_selection_set": return CreateSelectionSet(a);
                case "list_viewpoints": return ListViewpoints();
                case "save_viewpoint": return SaveViewpoint(a);
                case "list_clash_tests": return ListClashTests();
                case "create_clash_test": return CreateClashTest(a);
                case "run_clash_test": return RunClashTest(a);
                case "get_clash_results": return GetClashResults(a);
                case "inspect_clash_result": return InspectClashResult(a);
                case "focus_clash_result": return FocusClashResult(a);
                case "set_clash_item_visibility": return SetClashItemVisibility(a);
                case "set_visibility": return SetVisibility(a);
                case "reset_visibility": return ResetVisibility();
                case "list_hidden_items": return ListHiddenItems(a);
                case "create_selection_set_from_items": return CreateSetFromItems(a);
                case "list_animations": return ListAnimations();
                case "get_animation": return GetAnimation(a);
                case "go_to_viewpoint": return GoToViewpoint(a);
                case "set_clash_result_status": return SetClashResultStatus(a);
                case "get_item_properties": return GetItemProperties(a);
                case "color_items": return ColorItems(a);
                case "reset_colors": return ResetColors();
                case "list_models": return ListModels();
                case "export_clash_report": return ExportClashReport(a);
                default: throw new ArgumentException("Unknown tool: " + tool);
            }
        }

        private static Document Doc
        {
            get
            {
                var d = Application.ActiveDocument;
                if (d == null) throw new InvalidOperationException("No active Navisworks document.");
                return d;
            }
        }

        // ---------- model / search ----------

        private static JToken GetModelInfo()
        {
            var d = Doc;
            var models = new JArray();
            foreach (var m in d.Models)
                models.Add(new JObject
                {
                    ["file"] = m.FileName,
                    ["rootName"] = m.RootItem.DisplayName,
                    ["topLevelChildren"] = m.RootItem.Children.Count()
                });
            return new JObject
            {
                ["title"] = d.Title,
                ["fileName"] = d.FileName,
                ["modelCount"] = d.Models.Count,
                ["models"] = models,
                ["currentSelectionCount"] = d.CurrentSelection.SelectedItems.Count,
                ["selectionSetCount"] = CountSaved(d.SelectionSets.Value),
                ["clashTestCount"] = AllTests().Count
            };
        }

        private static ModelItemCollection FindItems(JObject a, out Search search)
        {
            // Either one condition (category/property/value/match) or several in "conditions" (all must match).
            var conds = new List<JObject>();
            if (a["conditions"] is JArray ca && ca.Count > 0) foreach (var c in ca) conds.Add((JObject)c);
            else conds.Add(a);

            search = new Search();
            search.Selection.SelectAll();
            foreach (var c in conds)
            {
                string cat = (string)c["category"], prop = (string)c["property"], val = (string)c["value"];
                string mode = ((string)c["match"] ?? "equals").ToLowerInvariant();
                if (cat == null || prop == null || val == null)
                    throw new ArgumentException("category, property and value are required");
                var hasProp = SearchCondition.HasPropertyByDisplayName(cat, prop);
                search.SearchConditions.Add(mode == "contains"
                    ? hasProp.DisplayStringContains(val)
                    : hasProp.EqualValue(VariantData.FromDisplayString(val)));
            }
            return search.FindAll(Doc, false);
        }

        private static JToken SearchItems(JObject a)
        {
            var items = FindItems(a, out _);
            int limit = (int?)a["limit"] ?? 50;
            var arr = new JArray();
            foreach (var it in items.Take(limit))
                arr.Add(new JObject
                {
                    ["name"] = it.DisplayName,
                    ["class"] = it.ClassDisplayName,
                    ["guid"] = it.InstanceGuid.ToString()
                });
            return new JObject { ["totalFound"] = items.Count, ["returned"] = arr.Count, ["items"] = arr };
        }

        private static JToken SelectItems(JObject a)
        {
            var items = FindItems(a, out _);
            var d = Doc;
            d.CurrentSelection.Clear();
            d.CurrentSelection.CopyFrom(items);
            if ((bool?)a["zoom"] ?? true) d.ActiveView.FocusOnCurrentSelection();
            return new JObject { ["selected"] = items.Count };
        }

        // ---------- selection sets ----------

        private static int CountSaved(SavedItemCollection c)
        {
            int n = 0;
            foreach (SavedItem s in c) n += s.IsGroup ? CountSaved(((GroupItem)s).Children) : 1;
            return n;
        }

        private static void Walk(SavedItemCollection c, string path, Action<SavedItem, string> f)
        {
            foreach (SavedItem s in c)
            {
                if (s.IsGroup) Walk(((GroupItem)s).Children, path + s.DisplayName + "/", f);
                else f(s, path);
            }
        }

        private static JToken ListSelectionSets()
        {
            var arr = new JArray();
            Walk(Doc.SelectionSets.Value, "", (s, p) => arr.Add(new JObject { ["name"] = s.DisplayName, ["folder"] = p }));
            return arr;
        }

        private static JToken CreateSelectionSet(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            var items = FindItems(a, out var search);
            var set = new SelectionSet(search) { DisplayName = name };
            Doc.SelectionSets.AddCopy(set);
            return new JObject { ["created"] = name, ["itemsMatched"] = items.Count };
        }

        private static SelectionSet FindSet(string name)
        {
            SelectionSet found = null;
            Walk(Doc.SelectionSets.Value, "", (s, p) =>
            {
                if (found == null && s is SelectionSet ss && string.Equals(ss.DisplayName, name, StringComparison.OrdinalIgnoreCase)) found = ss;
            });
            return found ?? throw new ArgumentException("Selection set not found: " + name);
        }

        // ---------- viewpoints ----------

        private static JToken ListViewpoints()
        {
            var arr = new JArray();
            Walk(Doc.SavedViewpoints.Value, "", (s, p) => arr.Add(new JObject { ["name"] = s.DisplayName, ["folder"] = p }));
            return arr;
        }

        private static JToken SaveViewpoint(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            var vp = new SavedViewpoint(Doc.CurrentViewpoint.CreateCopy()) { DisplayName = name };
            Doc.SavedViewpoints.AddCopy(vp);
            return new JObject { ["saved"] = name };
        }

        // ---------- clash ----------

        private static DocumentClashTests ClashTests { get { return Doc.GetClash().TestsData; } }

        // Tests live under ClashTestsData.TestsRoot (a folder); tests can also be nested in folders.
        private static void CollectTests(GroupItem folder, List<ClashTest> into)
        {
            foreach (SavedItem s in folder.Children)
            {
                if (s is ClashTest t) into.Add(t);
                else if (s is GroupItem g) CollectTests(g, into);
            }
        }

        private static List<ClashTest> AllTests()
        {
            var list = new List<ClashTest>();
            CollectTests(ClashTests.Value.TestsRoot, list);
            return list;
        }

        private static JToken ListClashTests()
        {
            var arr = new JArray();
            foreach (var t in AllTests())
                arr.Add(new JObject
                {
                    ["name"] = t.DisplayName,
                    ["type"] = t.TestType.ToString(),
                    ["tolerance"] = t.Tolerance,
                    ["status"] = t.Status.ToString(),
                    ["resultCount"] = CountResults(t)
                });
            return arr;
        }

        private static int CountResults(SavedItem parent)
        {
            int n = 0;
            var children = (parent as GroupItem)?.Children;
            if (children == null) return 0;
            foreach (SavedItem c in children) n += c is ClashResult ? 1 : CountResults(c);
            return n;
        }

        private static ClashTest FindTest(string name)
        {
            foreach (var t in AllTests())
                if (string.Equals(t.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return t;
            throw new ArgumentException("Clash test not found: " + name);
        }

        private static JToken CreateClashTest(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            var setA = FindSet((string)a["selection_set_a"] ?? throw new ArgumentException("selection_set_a is required"));
            var setB = FindSet((string)a["selection_set_b"] ?? throw new ArgumentException("selection_set_b is required"));
            double tol = (double?)a["tolerance"] ?? 0.0;
            var type = (ClashTestType)Enum.Parse(typeof(ClashTestType), (string)a["type"] ?? "Hard", true);

            var t = new ClashTest { DisplayName = name, TestType = type, Tolerance = tol };
            t.SelectionA.Selection.CopyFrom(setA.GetSelectedItems(Doc));
            t.SelectionB.Selection.CopyFrom(setB.GetSelectedItems(Doc));
            ClashTests.TestsAddCopy(ClashTests.Value.TestsRoot, t);
            return new JObject { ["created"] = name, ["type"] = type.ToString(), ["tolerance"] = tol };
        }

        private static JToken RunClashTest(JObject a)
        {
            string name = (string)a["name"];
            if (string.IsNullOrEmpty(name) || name == "*") ClashTests.TestsRunAllTests();
            else ClashTests.TestsRunTest(FindTest(name));
            return ListClashTests();
        }

        private static void CollectResults(SavedItem parent, string group, JArray into, int limit, string statusFilter)
        {
            var children = (parent as GroupItem)?.Children;
            if (children == null) return;
            foreach (SavedItem c in children)
            {
                if (into.Count >= limit) return;
                if (c is ClashResult r)
                {
                    if (statusFilter != null && !string.Equals(r.Status.ToString(), statusFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    into.Add(new JObject
                    {
                        ["name"] = r.DisplayName,
                        ["group"] = group,
                        ["status"] = r.Status.ToString(),
                        ["distance"] = r.Distance,
                        ["item1"] = r.Item1 != null ? r.Item1.DisplayName : null,
                        ["item2"] = r.Item2 != null ? r.Item2.DisplayName : null,
                        ["item1Guid"] = r.Item1 != null ? r.Item1.InstanceGuid.ToString() : null,
                        ["item2Guid"] = r.Item2 != null ? r.Item2.InstanceGuid.ToString() : null,
                        ["center"] = r.Center == null ? null : new JArray(r.Center.X, r.Center.Y, r.Center.Z)
                    });
                }
                else CollectResults(c, c.DisplayName, into, limit, statusFilter);
            }
        }

        private static JToken GetClashResults(JObject a)
        {
            var t = FindTest((string)a["name"] ?? throw new ArgumentException("name is required"));
            var arr = new JArray();
            CollectResults(t, "", arr, (int?)a["limit"] ?? 100, (string)a["status"]);
            return new JObject { ["test"] = t.DisplayName, ["totalResults"] = CountResults(t), ["returned"] = arr.Count, ["results"] = arr };
        }

        // ---------- overlap review (clash results) ----------

        private static ClashResult FindResult(SavedItem parent, string name)
        {
            var children = (parent as GroupItem)?.Children;
            if (children == null) return null;
            foreach (SavedItem c in children)
            {
                if (c is ClashResult r) { if (string.Equals(r.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return r; }
                else { var f = FindResult(c, name); if (f != null) return f; }
            }
            return null;
        }

        private static ClashResult GetResult(JObject a, out ClashTest test)
        {
            test = FindTest((string)a["test"] ?? throw new ArgumentException("test is required"));
            string rn = (string)a["result"] ?? throw new ArgumentException("result is required (use the name from get_clash_results, e.g. Clash1)");
            return FindResult(test, rn) ?? throw new ArgumentException("Clash result not found: " + rn);
        }

        private static JObject Describe(ModelItem it, int maxProps)
        {
            var path = new List<string>();
            foreach (var x in it.AncestorsAndSelf) path.Add(x.DisplayName);
            path.Reverse();
            var props = new JObject();
            int n = 0;
            foreach (PropertyCategory pc in it.PropertyCategories)
            {
                var cat = new JObject();
                foreach (DataProperty p in pc.Properties)
                {
                    if (n++ >= maxProps) break;
                    string v;
                    try { v = p.Value.ToDisplayString(); } catch { v = null; }
                    cat[p.DisplayName] = v;
                }
                if (cat.Count > 0) props[pc.DisplayName] = cat;
            }
            return new JObject
            {
                ["name"] = it.DisplayName,
                ["class"] = it.ClassDisplayName,
                ["guid"] = it.InstanceGuid.ToString(),
                ["path"] = string.Join(" > ", path),
                ["sourceFile"] = it.Model != null ? it.Model.FileName : null,
                ["hidden"] = it.IsHidden,
                ["properties"] = props
            };
        }

        private static JToken InspectClashResult(JObject a)
        {
            var r = GetResult(a, out var t);
            int max = (int?)a["max_properties"] ?? 120;
            return new JObject
            {
                ["test"] = t.DisplayName,
                ["result"] = r.DisplayName,
                ["status"] = r.Status.ToString(),
                ["distance"] = r.Distance,
                ["item1"] = r.Item1 != null ? Describe(r.Item1, max) : null,
                ["item2"] = r.Item2 != null ? Describe(r.Item2, max) : null
            };
        }

        private static JToken FocusClashResult(JObject a)
        {
            var r = GetResult(a, out _);
            var d = Doc;
            var items = new ModelItemCollection();
            if (r.Item1 != null) items.Add(r.Item1);
            if (r.Item2 != null) items.Add(r.Item2);
            d.CurrentSelection.Clear();
            d.CurrentSelection.CopyFrom(items);
            d.ActiveView.FocusOnCurrentSelection();
            return new JObject { ["focused"] = r.DisplayName, ["selected"] = items.Count };
        }

        private static JToken SetClashItemVisibility(JObject a)
        {
            var r = GetResult(a, out _);
            string which = ((string)a["item"] ?? "both").ToLowerInvariant();
            bool hidden = (bool?)a["hidden"] ?? true;
            var items = new ModelItemCollection();
            if ((which == "item1" || which == "both") && r.Item1 != null) items.Add(r.Item1);
            if ((which == "item2" || which == "both") && r.Item2 != null) items.Add(r.Item2);
            if (items.Count == 0) throw new ArgumentException("item must be item1, item2 or both");
            Doc.Models.SetHidden(items, hidden);
            return new JObject { ["result"] = r.DisplayName, ["item"] = which, ["hidden"] = hidden };
        }

        private static JToken SetVisibility(JObject a)
        {
            var items = FindItems(a, out _);
            bool hidden = (bool?)a["hidden"] ?? true;
            Doc.Models.SetHidden(items, hidden);
            return new JObject { ["items"] = items.Count, ["hidden"] = hidden };
        }

        private static JToken ResetVisibility()
        {
            Doc.Models.ResetAllHidden();
            return new JObject { ["reset"] = true };
        }

        private static JToken SetClashResultStatus(JObject a)
        {
            var r = GetResult(a, out _);
            var status = (ClashResultStatus)Enum.Parse(typeof(ClashResultStatus), (string)a["status"] ?? throw new ArgumentException("status is required"), true);
            // The signature differs between Navisworks versions (newer ones add an Assignee argument), so call it by reflection.
            var tests = ClashTests;
            System.Reflection.MethodInfo best = null;
            foreach (var m in tests.GetType().GetMethods())
            {
                if (m.Name != "TestsEditResultStatus") continue;
                if (best == null || m.GetParameters().Length < best.GetParameters().Length) best = m;
            }
            if (best == null) throw new InvalidOperationException("This Navisworks version cannot change clash result status.");
            var ps = best.GetParameters();
            var args = new object[ps.Length];
            args[0] = r;
            if (ps.Length > 1) args[1] = status;
            try { best.Invoke(tests, args); }
            catch (System.Reflection.TargetInvocationException ex) { throw ex.InnerException ?? ex; }
            return new JObject { ["result"] = r.DisplayName, ["status"] = status.ToString() };
        }

        // ---------- viewpoint animations ----------

        // Smoothing, Loops and Delay are not public in every Navisworks version, so read them by reflection (null if unavailable).
        private static string Prop(object o, string name)
        {
            try
            {
                var f = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var p = o.GetType().GetProperty(name, f);
                object v = p != null ? p.GetValue(o, null) : null;
                if (v == null) { var m = o.GetType().GetMethod("Get" + name, f, null, Type.EmptyTypes, null); if (m != null) v = m.Invoke(o, null); }
                return v == null ? null : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }

        private static void FindAnims(SavedItemCollection c, string path, List<KeyValuePair<string, SavedViewpointAnimation>> into)
        {
            foreach (SavedItem s in c)
            {
                if (s is SavedViewpointAnimation an) into.Add(new KeyValuePair<string, SavedViewpointAnimation>(path, an));
                else if (s is GroupItem g) FindAnims(g.Children, path + s.DisplayName + "/", into);
            }
        }

        private static JToken ListAnimations()
        {
            var found = new List<KeyValuePair<string, SavedViewpointAnimation>>();
            FindAnims(Doc.SavedViewpoints.Value, "", found);
            var arr = new JArray();
            foreach (var kv in found)
            {
                int frames = 0, cuts = 0;
                foreach (SavedItem ch in kv.Value.Children) { if (ch is SavedViewpoint) frames++; else if (ch is SavedViewpointAnimationCut) cuts++; }
                arr.Add(new JObject
                {
                    ["name"] = kv.Value.DisplayName,
                    ["folder"] = kv.Key,
                    ["frames"] = frames,
                    ["cuts"] = cuts,
                    ["smoothing"] = Prop(kv.Value, "Smoothing"),
                    ["loops"] = Prop(kv.Value, "Loops")
                });
            }
            return arr;
        }

        private static JToken GetAnimation(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            var found = new List<KeyValuePair<string, SavedViewpointAnimation>>();
            FindAnims(Doc.SavedViewpoints.Value, "", found);
            SavedViewpointAnimation an = null;
            foreach (var kv in found) if (string.Equals(kv.Value.DisplayName, name, StringComparison.OrdinalIgnoreCase)) { an = kv.Value; break; }
            if (an == null) throw new ArgumentException("Animation not found: " + name);

            var items = new JArray();
            Point3D prevPos = null; Rotation3D prevRot = null;
            double totalDist = 0, totalAngle = 0, totalDelay = 0;
            int frameNo = 0;
            foreach (SavedItem ch in an.Children)
            {
                if (ch is SavedViewpointAnimationCut cut)
                {
                    double delay = 0; double.TryParse(Prop(cut, "Delay"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out delay);
                    totalDelay += delay;
                    items.Add(new JObject { ["type"] = "cut", ["name"] = cut.DisplayName, ["delaySeconds"] = delay });
                }
                else if (ch is SavedViewpoint sv)
                {
                    frameNo++;
                    var vp = sv.Viewpoint;
                    var pos = vp.Position; var rot = vp.Rotation;
                    var o = new JObject
                    {
                        ["type"] = "frame",
                        ["frame"] = frameNo,
                        ["name"] = sv.DisplayName,
                        ["position"] = new JArray(Math.Round(pos.X, 3), Math.Round(pos.Y, 3), Math.Round(pos.Z, 3))
                    };
                    if (prevPos != null)
                    {
                        double dx = pos.X - prevPos.X, dy = pos.Y - prevPos.Y, dz = pos.Z - prevPos.Z;
                        double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        double dot = Math.Abs(rot.A * prevRot.A + rot.B * prevRot.B + rot.C * prevRot.C + rot.D * prevRot.D);
                        if (dot > 1) dot = 1;
                        double ang = 2 * Math.Acos(dot) * 180.0 / Math.PI;
                        totalDist += dist; totalAngle += ang;
                        o["moveFromPrevious"] = Math.Round(dist, 3);
                        o["turnFromPreviousDegrees"] = Math.Round(ang, 2);
                    }
                    prevPos = pos; prevRot = rot;
                    items.Add(o);
                }
            }
            return new JObject
            {
                ["name"] = an.DisplayName,
                ["units"] = Doc.Units.ToString(),
                ["smoothing"] = Prop(an, "Smoothing"),
                ["loops"] = Prop(an, "Loops"),
                ["frames"] = frameNo,
                ["totalCameraTravel"] = Math.Round(totalDist, 3),
                ["totalTurnDegrees"] = Math.Round(totalAngle, 1),
                ["totalCutDelaySeconds"] = totalDelay,
                ["note"] = "Navisworks does not expose per-frame times. Playback length is set by Navisworks; use travel and turn per frame to judge pacing and flow.",
                ["items"] = items
            };
        }

        private static SavedViewpoint FindViewpointByName(SavedItemCollection c, string name)
        {
            foreach (SavedItem s in c)
            {
                if (s is SavedViewpoint v && string.Equals(v.DisplayName, name, StringComparison.OrdinalIgnoreCase)) return v;
                if (s is GroupItem g) { var f = FindViewpointByName(g.Children, name); if (f != null) return f; }
            }
            return null;
        }

        private static JToken GoToViewpoint(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            var v = FindViewpointByName(Doc.SavedViewpoints.Value, name) ?? throw new ArgumentException("Viewpoint not found: " + name);
            Doc.SavedViewpoints.CurrentSavedViewpoint = v;
            return new JObject { ["viewpoint"] = v.DisplayName };
        }

        // ---------- hidden items and exact sets ----------

        private static bool Expired(DateTime started, double seconds) { return (DateTime.UtcNow - started).TotalSeconds > seconds; }

        // Walks the model tree level by level (breadth-first). Big models are scanned in slices of ~30 s:
        // each call continues where the last one stopped until Done is true.
        private class HiddenScan { public List<ModelItem> Level = new List<ModelItem>(), Next = new List<ModelItem>(), Found = new List<ModelItem>(); public int Pos, Scanned, Skipped; public bool Done; public HashSet<Guid> Exclude; }
        private static HiddenScan _scan;

        private static HiddenScan ScanHidden(bool restart, bool reuseDone = false, JArray excludeSets = null)
        {
            if (reuseDone && !restart && _scan != null && _scan.Done) return _scan;
            if (restart || _scan == null || _scan.Done)
            {
                _scan = new HiddenScan();
                if (excludeSets != null && excludeSets.Count > 0) _scan.Exclude = ItemsOfSets(excludeSets);
                foreach (var m in Doc.Models) _scan.Level.Add(m.RootItem);
            }
            var sc = _scan;
            var started = DateTime.UtcNow;
            while (!sc.Done)
            {
                if (sc.Pos >= sc.Level.Count)
                {
                    if (sc.Next.Count == 0) { sc.Done = true; break; }
                    sc.Level = sc.Next; sc.Next = new List<ModelItem>(); sc.Pos = 0;
                }
                if ((sc.Scanned & 127) == 0 && Expired(started, 30)) break;
                var item = sc.Level[sc.Pos++];
                sc.Scanned++;
                if (sc.Exclude != null && sc.Exclude.Contains(item.InstanceGuid)) { sc.Skipped++; continue; }
                if (item.IsHidden) { sc.Found.Add(item); continue; }
                foreach (var c in item.Children) sc.Next.Add(c);
            }
            return sc;
        }

        // Items already stored in saved sets ("*" = every set), so they can be left out of a hidden-items scan.
        private static HashSet<Guid> ItemsOfSets(JArray names)
        {
            var all = false; var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in names) { var t = (string)n; if (t == "*") all = true; else if (t != null) wanted.Add(t); }
            var guids = new HashSet<Guid>();
            Walk(Doc.SelectionSets.Value, "", (si, path) =>
            {
                var ss = si as SelectionSet;
                if (ss == null || !(all || wanted.Contains(ss.DisplayName))) return;
                try { foreach (var it in ss.GetSelectedItems(Doc)) guids.Add(it.InstanceGuid); } catch { }
            });
            return guids;
        }

        private static string PathOf(ModelItem it)
        {
            var path = new List<string>();
            foreach (var x in it.AncestorsAndSelf) path.Add(x.DisplayName);
            path.Reverse();
            return string.Join(" > ", path);
        }

        private static JToken ListHiddenItems(JObject a)
        {
            int limit = (int?)a["limit"] ?? 200;
            var sc = ScanHidden((bool?)a["restart"] ?? false, false, a["excludeSets"] as JArray);
            var arr = new JArray();
            foreach (var it in sc.Found.GetRange(0, Math.Min(limit, sc.Found.Count)))
                arr.Add(new JObject
                {
                    ["name"] = it.DisplayName,
                    ["class"] = it.ClassDisplayName,
                    ["guid"] = it.InstanceGuid.ToString(),
                    ["path"] = PathOf(it),
                    ["sourceFile"] = it.Model != null ? it.Model.FileName : null
                });
            return new JObject
            {
                ["hiddenFound"] = arr.Count,
                ["done"] = sc.Done,
                ["itemsScanned"] = sc.Scanned,
                ["skippedBecauseInExcludedSets"] = sc.Skipped,
                ["note"] = sc.Done ? "Top-level hidden items only; children of a hidden item are hidden with it." : "Scan not finished (big model). Call list_hidden_items again to continue; hidden items found so far are listed.",
                ["items"] = arr
            };
        }

        // Looks the items up level by level (breadth-first), so items near the top are found in seconds.
        private static void CollectByGuid(HashSet<Guid> wanted, ModelItemCollection into)
        {
            var started = DateTime.UtcNow;
            var level = new List<ModelItem>();
            foreach (var m in Doc.Models) level.Add(m.RootItem);
            int count = 0;
            while (level.Count > 0 && wanted.Count > 0)
            {
                var next = new List<ModelItem>();
                foreach (var item in level)
                {
                    if ((++count & 127) == 0 && Expired(started, 35)) return;
                    if (wanted.Remove(item.InstanceGuid)) { into.Add(item); if (wanted.Count == 0) return; }
                    foreach (var c in item.Children) next.Add(c);
                }
                level = next;
            }
        }

        private static JToken CreateSetFromItems(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            string source = ((string)a["source"] ?? "").ToLowerInvariant();
            var items = new ModelItemCollection();
            if (source == "hidden")
            {
                var sc = ScanHidden(false, true);
                if (!sc.Done && !((bool?)a["allowPartial"] ?? false)) throw new ArgumentException("Hidden-item scan is not finished yet (" + sc.Scanned + " items scanned). Call list_hidden_items until done is true, then create the set.");
                foreach (var it in sc.Found) items.Add(it);
            }
            else if (source == "selected")
            {
                items.CopyFrom(Doc.CurrentSelection.SelectedItems);
            }
            else if (source == "guids")
            {
                var wanted = new HashSet<Guid>();
                if (a["guids"] is JArray ga) foreach (var g in ga) { Guid gg; if (Guid.TryParse((string)g, out gg)) wanted.Add(gg); }
                if (wanted.Count == 0) throw new ArgumentException("guids is required when source is guids");
                CollectByGuid(wanted, items);
            }
            else throw new ArgumentException("source must be hidden, selected or guids");
            if (items.Count == 0) throw new ArgumentException("No items found for source: " + source);
            var set = new SelectionSet(items) { DisplayName = name };
            Doc.SelectionSets.AddCopy(set);
            return new JObject { ["created"] = name, ["source"] = source, ["items"] = items.Count };
        }

        // ---------- properties, colours, models, reports (1.5.0) ----------

        // Items from the current selection ("selected"), a saved set ("set" + name), or a property search.
        private static ModelItemCollection ResolveItems(JObject a)
        {
            string source = ((string)a["source"] ?? "").ToLowerInvariant();
            if (source == "selected")
            {
                var sel = new ModelItemCollection();
                sel.CopyFrom(Doc.CurrentSelection.SelectedItems);
                return sel;
            }
            if (source == "set")
            {
                var ss = FindSet((string)a["set"] ?? throw new ArgumentException("set (the set name) is required when source is set"));
                var col = new ModelItemCollection();
                col.CopyFrom(ss.GetSelectedItems(Doc));
                return col;
            }
            return FindItems(a, out _);
        }

        private static JToken GetItemProperties(JObject a)
        {
            var items = ResolveItems(a);
            int limit = Math.Max(1, Math.Min((int?)a["limit"] ?? 5, 25));
            int maxProps = Math.Max(1, Math.Min((int?)a["maxProperties"] ?? 80, 400));
            var arr = new JArray();
            foreach (var it in items.Take(limit)) arr.Add(Describe(it, maxProps));
            return new JObject { ["totalFound"] = items.Count, ["returned"] = arr.Count, ["items"] = arr };
        }

        private static Color ParseColor(string text)
        {
            string t = (text ?? "").Trim().ToLowerInvariant();
            switch (t)
            {
                case "red": t = "#E53935"; break;
                case "green": t = "#43A047"; break;
                case "blue": t = "#1E88E5"; break;
                case "yellow": t = "#FDD835"; break;
                case "orange": t = "#FB8C00"; break;
                case "purple": t = "#8E24AA"; break;
                case "cyan": t = "#00ACC1"; break;
                case "pink": t = "#EC407A"; break;
                case "grey": case "gray": t = "#9E9E9E"; break;
                case "white": t = "#FFFFFF"; break;
                case "black": t = "#000000"; break;
            }
            if (t.StartsWith("#")) t = t.Substring(1);
            if (t.Length != 6) throw new ArgumentException("color must be a name (red, green, blue, yellow, orange, purple, cyan, pink, grey, white, black) or #RRGGBB");
            return Color.FromByteRGB(Convert.ToByte(t.Substring(0, 2), 16), Convert.ToByte(t.Substring(2, 2), 16), Convert.ToByte(t.Substring(4, 2), 16));
        }

        // View-only: temporary overrides are not saved into the model.
        private static JToken ColorItems(JObject a)
        {
            var items = ResolveItems(a);
            if (items.Count == 0) throw new ArgumentException("No items matched");
            var color = ParseColor((string)a["color"] ?? throw new ArgumentException("color is required"));
            Doc.Models.OverrideTemporaryColor(items, color);
            double? tr = (double?)a["transparency"];
            if (tr.HasValue) Doc.Models.OverrideTemporaryTransparency(items, Math.Max(0, Math.Min(1, tr.Value)));
            return new JObject { ["colored"] = items.Count, ["color"] = (string)a["color"], ["note"] = "View-only. Use reset_colors to undo." };
        }

        private static JToken ResetColors()
        {
            Doc.Models.ResetAllTemporaryMaterials();
            return new JObject { ["reset"] = true };
        }

        private static JToken ListModels()
        {
            var arr = new JArray();
            foreach (var m in Doc.Models)
            {
                int children = 0;
                try { children = m.RootItem.Children.Count(); } catch { }
                arr.Add(new JObject { ["file"] = m.FileName, ["root"] = m.RootItem.DisplayName, ["topLevelChildren"] = children });
            }
            return new JObject { ["fileName"] = Doc.FileName, ["title"] = Doc.Title, ["models"] = arr };
        }

        private static string Csv(string v)
        {
            if (v == null) return "";
            if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        // Writes a CSV report to Documents\AI Connect\reports (a new file every time, nothing is overwritten).
        private static JToken ExportClashReport(JObject a)
        {
            var t = FindTest((string)a["name"] ?? throw new ArgumentException("name is required"));
            var arr = new JArray();
            CollectResults(t, "", arr, int.MaxValue, (string)a["status"]);
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AI Connect", "reports");
            Directory.CreateDirectory(dir);
            string safe = string.Concat(t.DisplayName.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            var file = Path.Combine(dir, safe + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
            var sb = new StringBuilder();
            sb.AppendLine("Name,Group,Status,Distance,Item 1,Item 2,Item 1 GUID,Item 2 GUID,Center X,Center Y,Center Z");
            foreach (JObject r in arr)
            {
                var c = r["center"] as JArray;
                sb.AppendLine(string.Join(",", new[] {
                    Csv((string)r["name"]), Csv((string)r["group"]), Csv((string)r["status"]), Csv((string)r["distance"]),
                    Csv((string)r["item1"]), Csv((string)r["item2"]), Csv((string)r["item1Guid"]), Csv((string)r["item2Guid"]),
                    c != null ? Csv((string)c[0]) : "", c != null ? Csv((string)c[1]) : "", c != null ? Csv((string)c[2]) : "" }));
            }
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
            return new JObject { ["test"] = t.DisplayName, ["results"] = arr.Count, ["file"] = file };
        }
    }
}
