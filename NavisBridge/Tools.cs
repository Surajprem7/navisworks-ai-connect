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
                // 1.2.0
                case "zoom_to_items": return ZoomToItems(a);
                case "save_item_viewpoint": return SaveItemViewpoint(a);
                case "apply_viewpoint": return ApplyViewpoint(a);
                case "delete_viewpoint": return DeleteViewpoint(a);
                case "hide_items": return HideItems(a, true);
                case "unhide_items": return HideItems(a, false);
                case "isolate_items": return IsolateItems(a);
                case "unhide_all": Doc.Models.ResetAllHidden(); return new JObject { ["unhiddenAll"] = true };
                case "set_section_box": return SetSectionBox(a);
                case "clear_section_box": return ClearSectionBox();
                case "get_selection": return GetSelection(a);
                case "save_selection_as_set": return SaveSelectionAsSet(a);
                case "capture_view": return CaptureView(a);
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

        /// <summary>
        /// Property search. Optional filters: `model` (text contained in the source model's file name, e.g. "ARC"),
        /// `guid` (exact instance GUID, as returned by search_items). `guid` alone is enough to find one item.
        /// </summary>
        private static ModelItemCollection FindItems(JObject a, out Search search)
        {
            string cat = (string)a["category"], prop = (string)a["property"], val = (string)a["value"];
            string mode = ((string)a["match"] ?? "equals").ToLowerInvariant();
            string guid = (string)a["guid"], model = (string)a["model"];
            if (cat == null && prop == null && val == null && !string.IsNullOrEmpty(guid))
            {
                cat = "Item"; prop = "GUID"; val = guid; mode = "contains";
            }
            if (cat == null || prop == null || val == null)
                throw new ArgumentException("category, property and value are required (or give guid)");

            search = new Search();
            search.Selection.SelectAll();
            var hasProp = SearchCondition.HasPropertyByDisplayName(cat, prop);
            search.SearchConditions.Add(mode == "contains"
                ? hasProp.DisplayStringContains(val)
                : hasProp.EqualValue(VariantData.FromDisplayString(val)));
            var found = search.FindAll(Doc, false);
            if (string.IsNullOrEmpty(guid) && string.IsNullOrEmpty(model)) return found;

            var filtered = new ModelItemCollection();
            foreach (var it in found)
            {
                if (!string.IsNullOrEmpty(guid) && !string.Equals(it.InstanceGuid.ToString(), guid, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(model) && !ModelName(it).ToLowerInvariant().Contains(model.ToLowerInvariant())) continue;
                filtered.Add(it);
            }
            return filtered;
        }

        /// <summary>File name (without folder) of the appended model an item comes from.</summary>
        private static string ModelName(ModelItem it)
        {
            var root = it.AncestorsAndSelf.Last();
            foreach (var m in Doc.Models)
                if (m.RootItem.Equals(root)) return System.IO.Path.GetFileName(m.FileName ?? root.DisplayName);
            return root.DisplayName ?? "";
        }

        private static JToken SearchItems(JObject a)
        {
            var items = FindItems(a, out _);
            int limit = (int?)a["limit"] ?? 50;
            var arr = new JArray();
            foreach (var it in items.Take(limit))
                arr.Add(ItemJson(it));
            return new JObject { ["totalFound"] = items.Count, ["returned"] = arr.Count, ["items"] = arr };
        }

        private static JObject ItemJson(ModelItem it)
        {
            return new JObject
            {
                ["name"] = it.DisplayName,
                ["class"] = it.ClassDisplayName,
                ["guid"] = it.InstanceGuid.ToString(),
                ["model"] = ModelName(it),
                ["hidden"] = it.IsHidden
            };
        }

        private static JToken SelectItems(JObject a)
        {
            var items = FindItems(a, out _);
            var d = Doc;
            d.CurrentSelection.Clear();
            d.CurrentSelection.CopyFrom(items);
            JToken zoom = null;
            // FocusOnCurrentSelection() only moves the orbit focus point, not the camera, so zoom explicitly.
            if (items.Count > 0 && ((bool?)a["zoom"] ?? true))
                zoom = ZoomCore(items, (string)a["direction"] ?? "current", (double?)a["padding"] ?? 0.3, false, false);
            return new JObject { ["selected"] = items.Count, ["zoom"] = zoom };
        }

        // ---------- camera / visibility (1.2.0) ----------

        private static readonly Dictionary<string, double[]> Directions = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Direction from the target to the camera (camera sits on this side, looking back at the target).
            ["iso"] = new[] { 1.0, -1.0, 0.8 }, ["iso_se"] = new[] { 1.0, -1.0, 0.8 }, ["iso_sw"] = new[] { -1.0, -1.0, 0.8 },
            ["iso_ne"] = new[] { 1.0, 1.0, 0.8 }, ["iso_nw"] = new[] { -1.0, 1.0, 0.8 },
            ["top"] = new[] { 0.0, -0.02, 1.0 }, ["front"] = new[] { 0.0, -1.0, 0.15 }, ["back"] = new[] { 0.0, 1.0, 0.15 },
            ["left"] = new[] { -1.0, 0.0, 0.15 }, ["right"] = new[] { 1.0, 0.0, 0.15 }
        };

        /// <summary>
        /// Points the camera at the items' bounding box and fits it in view.
        /// Optionally makes the items visible (unhide), hides everything else (isolate) and puts a section box around them.
        /// </summary>
        private static JObject ZoomCore(ModelItemCollection items, string direction, double padding, bool unhide, bool sectionBox, bool isolate = false)
        {
            var d = Doc;
            var notes = new JArray();
            if (isolate) IsolateCore(items);
            else if (unhide) UnhideCore(items);

            var box = items.BoundingBox(false);
            if (box == null) throw new InvalidOperationException("Items have no geometry (no bounding box).");
            double sx = box.Max.X - box.Min.X, sy = box.Max.Y - box.Min.Y, sz = box.Max.Z - box.Min.Z;
            double maxDim = Math.Max(sx, Math.Max(sy, sz));
            if (!(maxDim > 0) || double.IsInfinity(maxDim)) throw new InvalidOperationException("Items have an empty bounding box.");
            double pad = Math.Max(padding, 0) * maxDim;
            var min = new Point3D(box.Min.X - pad, box.Min.Y - pad, box.Min.Z - pad);
            var max = new Point3D(box.Max.X + pad, box.Max.Y + pad, box.Max.Z + pad);
            var c = new Point3D((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2);
            double radius = 0.5 * Math.Sqrt(Math.Pow(max.X - min.X, 2) + Math.Pow(max.Y - min.Y, 2) + Math.Pow(max.Z - min.Z, 2));

            var vp = d.CurrentViewpoint.CreateCopy();
            double[] dir;
            if (string.Equals(direction, "current", StringComparison.OrdinalIgnoreCase))
            {
                var p = vp.Position;
                dir = new[] { p.X - c.X, p.Y - c.Y, p.Z - c.Z };
                if (Math.Abs(dir[0]) + Math.Abs(dir[1]) + Math.Abs(dir[2]) < 1e-9) dir = Directions["iso"];
            }
            else if (!Directions.TryGetValue(direction ?? "iso", out dir))
                throw new ArgumentException("direction must be one of: current, " + string.Join(", ", Directions.Keys));
            double len = Math.Sqrt(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);

            // Vertical field of view (radians) for perspective views; fall back to 45 degrees.
            double fov = 0.785;
            try { var hf = vp.GetType().GetProperty("HeightField")?.GetValue(vp, null); if (hf is double h && h > 0.05 && h < 3.0) fov = h; } catch { }
            double dist = radius / Math.Sin(Math.Min(fov, 1.4) / 2) * 1.05;

            vp.Position = new Point3D(c.X + dir[0] / len * dist, c.Y + dir[1] / len * dist, c.Z + dir[2] / len * dist);
            vp.PointAt(c);
            vp.FocalDistance = dist;
            if (!TryInvoke(vp, "AlignUp", new Vector3D(0, 0, 1))) notes.Add("AlignUp not available; camera roll left as is");
            d.CurrentViewpoint.CopyFrom(vp);

            bool sectioned = false;
            if (sectionBox)
            {
                try { SetSectionBoxCore(min, max); sectioned = true; }
                catch (Exception ex) { notes.Add("Section box not applied: " + ex.Message); }
            }
            return new JObject
            {
                ["items"] = items.Count,
                ["center"] = new JArray(c.X, c.Y, c.Z),
                ["size"] = new JArray(sx, sy, sz),
                ["cameraDistance"] = dist,
                ["sectionBox"] = sectioned,
                ["notes"] = notes
            };
        }

        private static void UnhideCore(IEnumerable<ModelItem> items)
        {
            var show = new List<ModelItem>();
            foreach (var it in items) { show.AddRange(it.AncestorsAndSelf); show.AddRange(it.Descendants); }
            Doc.Models.SetHidden(show, false);
        }

        /// <summary>Like Navisworks "Hide Unselected": only the items (and what is under them) stay visible.</summary>
        private static int IsolateCore(ModelItemCollection items)
        {
            var keep = new HashSet<ModelItem>();
            foreach (var it in items) foreach (var x in it.AncestorsAndSelf) keep.Add(x);
            var hide = new List<ModelItem>();
            foreach (var m in Doc.Models) if (!keep.Contains(m.RootItem)) hide.Add(m.RootItem);
            var itemSet = new HashSet<ModelItem>(items);
            foreach (var k in keep)
            {
                if (itemSet.Contains(k)) continue; // keep everything below a target item
                foreach (var ch in k.Children) if (!keep.Contains(ch)) hide.Add(ch);
            }
            Doc.Models.ResetAllHidden();
            Doc.Models.SetHidden(hide, true);
            return hide.Count;
        }

        private static JToken ZoomToItems(JObject a)
        {
            var items = FindItems(a, out _);
            if (items.Count == 0) throw new ArgumentException("No items matched.");
            if ((bool?)a["select"] ?? true) { Doc.CurrentSelection.Clear(); Doc.CurrentSelection.CopyFrom(items); }
            return ZoomCore(items, (string)a["direction"] ?? "iso", (double?)a["padding"] ?? 0.3,
                (bool?)a["unhide"] ?? true, (bool?)a["section_box"] ?? false, (bool?)a["isolate"] ?? false);
        }

        /// <summary>Zoom to the items (made visible, section box around them by default) and save that as a viewpoint, in one step.</summary>
        private static JToken SaveItemViewpoint(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            var items = FindItems(a, out _);
            if (items.Count == 0) throw new ArgumentException("No items matched.");
            Doc.CurrentSelection.Clear();
            Doc.CurrentSelection.CopyFrom(items);
            var zoom = ZoomCore(items, (string)a["direction"] ?? "iso", (double?)a["padding"] ?? 0.3,
                (bool?)a["unhide"] ?? true, (bool?)a["section_box"] ?? true, (bool?)a["isolate"] ?? false);
            int replaced = 0;
            if ((bool?)a["replace"] ?? true) replaced = RemoveViewpointsNamed(name);
            if ((bool?)a["clear_selection"] ?? true) Doc.CurrentSelection.Clear(); // so the selection highlight is not baked into the view
            var vp = new SavedViewpoint(Doc.CurrentViewpoint.CreateCopy()) { DisplayName = name };
            Doc.SavedViewpoints.AddCopy(vp);
            return new JObject { ["saved"] = name, ["replacedExisting"] = replaced, ["zoom"] = zoom };
        }

        private static JToken HideItems(JObject a, bool hidden)
        {
            var items = FindItems(a, out _);
            if (hidden) Doc.Models.SetHidden(items, true);
            else UnhideCore(items);
            return new JObject { [hidden ? "hidden" : "unhidden"] = items.Count };
        }

        private static JToken IsolateItems(JObject a)
        {
            var items = FindItems(a, out _);
            if (items.Count == 0) throw new ArgumentException("No items matched.");
            return new JObject { ["isolated"] = items.Count, ["hiddenNodes"] = IsolateCore(items) };
        }

        // ---------- section box ----------

        private static void SetSectionBoxCore(Point3D min, Point3D max)
        {
            var box = new JArray(new JArray(min.X, min.Y, min.Z), new JArray(max.X, max.Y, max.Z));
            var json = new JObject
            {
                ["Type"] = "ClipPlaneSet",
                ["Version"] = 1,
                ["OrientedBox"] = new JObject { ["Type"] = "OrientedBox3D", ["Version"] = 1, ["Box"] = box, ["Rotation"] = new JArray(0, 0, 0) },
                ["Enabled"] = true
            }.ToString(Newtonsoft.Json.Formatting.None);
            if (!TryInvoke(Doc.ActiveView, "SetClippingPlanes", json))
                throw new NotSupportedException("This Navisworks version has no View.SetClippingPlanes API.");
        }

        private static JToken SetSectionBox(JObject a)
        {
            Point3D min, max;
            if (a["min"] is JArray mn && a["max"] is JArray mx)
            {
                min = new Point3D((double)mn[0], (double)mn[1], (double)mn[2]);
                max = new Point3D((double)mx[0], (double)mx[1], (double)mx[2]);
            }
            else
            {
                var items = FindItems(a, out _);
                if (items.Count == 0) throw new ArgumentException("No items matched (or pass min/max).");
                var b = items.BoundingBox(false);
                double pad = Math.Max((double?)a["padding"] ?? 0.3, 0) * Math.Max(b.Max.X - b.Min.X, Math.Max(b.Max.Y - b.Min.Y, b.Max.Z - b.Min.Z));
                min = new Point3D(b.Min.X - pad, b.Min.Y - pad, b.Min.Z - pad);
                max = new Point3D(b.Max.X + pad, b.Max.Y + pad, b.Max.Z + pad);
            }
            SetSectionBoxCore(min, max);
            return new JObject { ["sectionBox"] = new JArray(new JArray(min.X, min.Y, min.Z), new JArray(max.X, max.Y, max.Z)) };
        }

        private static JToken ClearSectionBox()
        {
            var view = Doc.ActiveView;
            var m = view.GetType().GetMethod("GetClippingPlanes", System.Type.EmptyTypes);
            if (m == null) throw new NotSupportedException("This Navisworks version has no View.GetClippingPlanes API.");
            var cur = m.Invoke(view, null) as string;
            var j = string.IsNullOrWhiteSpace(cur) ? new JObject { ["Type"] = "ClipPlaneSet", ["Version"] = 1 } : JObject.Parse(cur);
            j["Enabled"] = false;
            TryInvoke(view, "SetClippingPlanes", j.ToString(Newtonsoft.Json.Formatting.None));
            return new JObject { ["sectioning"] = "off" };
        }

        // ---------- selection (1.2.0) ----------

        private static JToken GetSelection(JObject a)
        {
            var sel = Doc.CurrentSelection.SelectedItems;
            int limit = (int?)a["limit"] ?? 200;
            var arr = new JArray();
            var perModel = new JObject();
            foreach (var it in sel)
            {
                var mn = ModelName(it);
                perModel[mn] = ((int?)perModel[mn] ?? 0) + 1;
                if (arr.Count < limit) arr.Add(ItemJson(it));
            }
            return new JObject { ["selectedCount"] = sel.Count, ["perModel"] = perModel, ["returned"] = arr.Count, ["items"] = arr };
        }

        /// <summary>Saves the current selection (optionally only the items from models whose file name contains `model`) as a fixed Selection Set.</summary>
        private static JToken SaveSelectionAsSet(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            string model = (string)a["model"];
            var items = new ModelItemCollection();
            foreach (var it in Doc.CurrentSelection.SelectedItems)
                if (string.IsNullOrEmpty(model) || ModelName(it).ToLowerInvariant().Contains(model.ToLowerInvariant())) items.Add(it);
            if (items.Count == 0) throw new InvalidOperationException("Nothing to save: the selection is empty" + (string.IsNullOrEmpty(model) ? "." : " or has no items from a model matching '" + model + "'."));
            var set = new SelectionSet(items) { DisplayName = name };
            Doc.SelectionSets.AddCopy(set);
            return new JObject { ["created"] = name, ["items"] = items.Count };
        }

        // ---------- image capture (1.2.0) ----------

        /// <summary>Renders the current view (or a saved viewpoint, applied first) to a PNG so the assistant can check it.</summary>
        private static JToken CaptureView(JObject a)
        {
            string vpName = (string)a["viewpoint"];
            if (!string.IsNullOrEmpty(vpName)) ApplyViewpointCore(FindViewpoint(vpName));
            int w = (int?)a["width"] ?? 960, h = (int?)a["height"] ?? 600;
            var view = Doc.ActiveView;
            System.Reflection.MethodInfo gen = null;
            foreach (var m in view.GetType().GetMethods())
                if (m.Name == "GenerateImage" && m.GetParameters().Length == 3) { gen = m; break; }
            if (gen == null) throw new NotSupportedException("This Navisworks version has no View.GenerateImage API.");
            var styleType = gen.GetParameters()[0].ParameterType;
            object style = null;
            if (styleType.IsEnum)
            {
                foreach (var n in new[] { "ScenePlusOverlay", "Scene" })
                    if (Enum.IsDefined(styleType, n)) { style = Enum.Parse(styleType, n); break; }
                if (style == null) style = Enum.GetValues(styleType).GetValue(0);
            }
            var img = gen.Invoke(view, new object[] { style, w, h }) as System.Drawing.Image;
            if (img == null) throw new InvalidOperationException("Navisworks returned no image.");
            using (img)
            using (var ms = new System.IO.MemoryStream())
            {
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return new JObject { ["imageBase64"] = Convert.ToBase64String(ms.ToArray()), ["mimeType"] = "image/png", ["width"] = w, ["height"] = h };
            }
        }

        // ---------- reflection helper (APIs that differ between Navisworks versions) ----------

        private static bool TryInvoke(object target, string method, params object[] args)
        {
            foreach (var m in target.GetType().GetMethods())
            {
                if (m.Name != method) continue;
                var ps = m.GetParameters();
                if (ps.Length != args.Length) continue;
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                    if (args[i] != null && !ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
                if (!ok) continue;
                m.Invoke(target, args);
                return true;
            }
            return false;
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

        private static void WalkWithParent(GroupItem parent, SavedItemCollection c, Action<GroupItem, SavedItem> f)
        {
            foreach (SavedItem s in c)
            {
                if (s.IsGroup) WalkWithParent((GroupItem)s, ((GroupItem)s).Children, f);
                else f(parent, s);
            }
        }

        private static GroupItem ViewpointsRoot()
        {
            return Doc.SavedViewpoints.GetType().GetProperty("RootItem")?.GetValue(Doc.SavedViewpoints, null) as GroupItem;
        }

        private static SavedViewpoint FindViewpoint(string name)
        {
            SavedViewpoint found = null;
            Walk(Doc.SavedViewpoints.Value, "", (s, p) =>
            {
                if (found == null && s is SavedViewpoint v && string.Equals(v.DisplayName, name, StringComparison.OrdinalIgnoreCase)) found = v;
            });
            return found ?? throw new ArgumentException("Viewpoint not found: " + name);
        }

        private static void ApplyViewpointCore(SavedViewpoint sv)
        {
            var svs = Doc.SavedViewpoints;
            var cur = svs.GetType().GetProperty("CurrentSavedViewpoint");
            if (cur != null && cur.CanWrite) { cur.SetValue(svs, sv, null); return; } // restores camera, hide state and sectioning like a click in the UI
            var vp = sv.GetType().GetProperty("Viewpoint")?.GetValue(sv, null) as Viewpoint;
            if (vp == null) throw new NotSupportedException("Cannot apply saved viewpoints in this Navisworks version.");
            Doc.CurrentViewpoint.CopyFrom(vp);
        }

        private static JToken ApplyViewpoint(JObject a)
        {
            var sv = FindViewpoint((string)a["name"] ?? throw new ArgumentException("name is required"));
            ApplyViewpointCore(sv);
            return new JObject { ["applied"] = sv.DisplayName };
        }

        /// <summary>Removes every saved viewpoint (not folders) whose name equals `name`, case-insensitive. Returns how many.</summary>
        private static int RemoveViewpointsNamed(string name)
        {
            var root = ViewpointsRoot();
            var hits = new List<KeyValuePair<GroupItem, SavedItem>>();
            WalkWithParent(root, Doc.SavedViewpoints.Value, (parent, s) =>
            {
                if (s is SavedViewpoint && string.Equals(s.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                    hits.Add(new KeyValuePair<GroupItem, SavedItem>(parent, s));
            });
            int n = 0;
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                var parent = hits[i].Key; var item = hits[i].Value;
                var svs = Doc.SavedViewpoints;
                bool ok = parent != null && TryInvoke(svs, "Remove", parent, item);
                if (!ok && parent != null)
                {
                    int idx = -1, k = 0;
                    foreach (SavedItem ch in parent.Children) { if (ch.Equals(item)) { idx = k; break; } k++; }
                    if (idx >= 0) ok = TryInvoke(svs, "Remove", parent, idx);
                }
                if (!ok) ok = TryInvoke(svs, "Remove", item);
                if (!ok) throw new NotSupportedException("Cannot delete viewpoints in this Navisworks version.");
                n++;
            }
            return n;
        }

        private static JToken DeleteViewpoint(JObject a)
        {
            var names = new List<string>();
            if (a["names"] is JArray arr) foreach (var t in arr) names.Add((string)t);
            if (a["name"] != null) names.Add((string)a["name"]);
            if (names.Count == 0) throw new ArgumentException("name or names is required");
            var deleted = new JArray(); var notFound = new JArray();
            foreach (var n in names.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                int c = RemoveViewpointsNamed(n);
                if (c > 0) deleted.Add(new JObject { ["name"] = n, ["count"] = c }); else notFound.Add(n);
            }
            return new JObject { ["deleted"] = deleted, ["notFound"] = notFound };
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
