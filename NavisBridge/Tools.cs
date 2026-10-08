using System;
using System.Collections.Generic;
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
            string cat = (string)a["category"], prop = (string)a["property"], val = (string)a["value"];
            string mode = ((string)a["match"] ?? "equals").ToLowerInvariant();
            if (cat == null || prop == null || val == null)
                throw new ArgumentException("category, property and value are required");

            search = new Search();
            search.Selection.SelectAll();
            var hasProp = SearchCondition.HasPropertyByDisplayName(cat, prop);
            search.SearchConditions.Add(mode == "contains"
                ? hasProp.DisplayStringContains(val)
                : hasProp.EqualValue(VariantData.FromDisplayString(val)));
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
    }
}
