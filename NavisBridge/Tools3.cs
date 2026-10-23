using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Navisworks.Api;
using Newtonsoft.Json.Linq;

namespace NavisBridge
{
    // Tools added in 1.7.0: put the camera at an exact point (set_camera) and build a walk through door openings (create_walkthrough).
    // The camera calls are made by name (reflection) so one build works across Navisworks versions; a missing call gives a clear message.
    public static partial class Tools
    {
        private static JToken RunExtra2(string tool, JObject a)
        {
            switch (tool)
            {
                case "set_camera": return SetCamera(a);
                case "create_walkthrough": return CreateWalkthrough(a);
                default: return null;
            }
        }

        private static double R3(double v) { return Math.Round(v, 3); }

        private static double[] Vec3(JToken t, string what)
        {
            var arr = t as JArray;
            if (arr == null || arr.Count != 3) throw new ArgumentException(what + " must be a list of 3 numbers [x, y, z]");
            return new[] { (double)arr[0], (double)arr[1], (double)arr[2] };
        }

        private static double UnitsPerMetre()
        {
            switch (Doc.Units.ToString())
            {
                case "Meters": return 1.0;
                case "Millimeters": return 1000.0;
                case "Centimeters": return 100.0;
                case "Inches": return 39.3700787;
                case "Feet": return 3.2808399;
                default: return 1.0;
            }
        }

        // A copy of the current view with the camera put at pos, looking along dir, with the vertical axis up.
        private static Viewpoint MakeCamera(double[] pos, double[] dir)
        {
            double len = Math.Sqrt(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
            if (len < 1e-9) throw new ArgumentException("The look direction is zero (the camera position and the look-at point are the same).");
            var vp = Doc.CurrentViewpoint.CreateCopy();
            var pp = vp.GetType().GetProperty("Position");
            var ad = FindMethod(vp, "AlignDirection", 1);
            var au = FindMethod(vp, "AlignUp", 1);
            if (pp == null || !pp.CanWrite || ad == null || au == null)
                throw new InvalidOperationException("This Navisworks version has no writable Viewpoint.Position / AlignDirection / AlignUp, so the camera cannot be set from AI Connect. Viewpoint members found: " +
                    string.Join(", ", vp.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name).Distinct().OrderBy(x => x)));
            var p = new Point3D(pos[0], pos[1], pos[2]);
            pp.SetValue(vp, p, null);
            ad.Invoke(vp, new object[] { new Vector3D(dir[0] / len, dir[1] / len, dir[2] / len) });
            au.Invoke(vp, new object[] { new Vector3D(0, 0, 1) });
            pp.SetValue(vp, p, null);
            return vp;
        }

        private static double[] RotateBy(double x, double y, double z, double w, double[] v)
        {
            double[] t1 = { y * v[2] - z * v[1] + w * v[0], z * v[0] - x * v[2] + w * v[1], x * v[1] - y * v[0] + w * v[2] };
            double[] t2 = { y * t1[2] - z * t1[1], z * t1[0] - x * t1[2], x * t1[1] - y * t1[0] };
            return new[] { v[0] + 2 * t2[0], v[1] + 2 * t2[1], v[2] + 2 * t2[2] };
        }

        // Where the camera is and which way its axes point (so the result can be checked without looking at the screen).
        private static JObject CameraReport(Viewpoint vp)
        {
            var o = new JObject();
            try { o["position"] = new JArray(vp.Position.X, vp.Position.Y, vp.Position.Z); } catch { }
            try
            {
                var r = vp.Rotation;
                o["rotation"] = new JArray(r.A, r.B, r.C, r.D);
                o["axisX"] = new JArray(RotateBy(r.A, r.B, r.C, r.D, new[] { 1.0, 0, 0 }).Select(R3));
                o["axisY"] = new JArray(RotateBy(r.A, r.B, r.C, r.D, new[] { 0, 1.0, 0 }).Select(R3));
                o["axisZ"] = new JArray(RotateBy(r.A, r.B, r.C, r.D, new[] { 0, 0, 1.0 }).Select(R3));
            }
            catch { }
            return o;
        }

        private static void SetEnabledValue(JToken t, bool value)
        {
            if (t is JObject o)
                foreach (var p in o.Properties().ToList())
                {
                    if (string.Equals(p.Name, "Enabled", StringComparison.OrdinalIgnoreCase) && p.Value.Type == JTokenType.Boolean) p.Value = new JValue(value);
                    else SetEnabledValue(p.Value, value);
                }
            else if (t is JArray a) foreach (var c in a) SetEnabledValue(c, value);
        }

        // Switches the section box off (the walk would otherwise be cut by it). Returns true when a section was found and switched off.
        private static bool DisableSection()
        {
            string cur = null;
            try { cur = GetClipJson(); } catch { return false; }
            if (string.IsNullOrWhiteSpace(cur)) return false;
            JToken tok;
            try { tok = JToken.Parse(cur); } catch { return false; }
            SetEnabledValue(tok, false);
            try { return SetClipJson(tok.ToString(Newtonsoft.Json.Formatting.None)); } catch { return false; }
        }

        // Puts the camera at an exact point. position/lookAt (or direction) are in model units (see get_model_info / get_section "units").
        private static JToken SetCamera(JObject a)
        {
            var pos = Vec3(a["position"], "position");
            double[] dir;
            if (a["lookAt"] != null) { var l = Vec3(a["lookAt"], "lookAt"); dir = new[] { l[0] - pos[0], l[1] - pos[1], l[2] - pos[2] }; }
            else if (a["direction"] != null) dir = Vec3(a["direction"], "direction");
            else throw new ArgumentException("Give lookAt [x, y, z] or direction [x, y, z].");
            string save = (string)a["save"];
            if (!string.IsNullOrWhiteSpace(save) && FindViewpointByName(Doc.SavedViewpoints.Value, save) != null)
                throw new ArgumentException("A viewpoint named '" + save + "' already exists. Use another name.");
            var vp = MakeCamera(pos, dir);
            Doc.CurrentViewpoint.CopyFrom(vp);
            Pump(300);
            if (!string.IsNullOrWhiteSpace(save))
                Doc.SavedViewpoints.AddCopy(new SavedViewpoint(Doc.CurrentViewpoint.CreateCopy()) { DisplayName = save });
            double len = Math.Sqrt(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
            return new JObject
            {
                ["units"] = Doc.Units.ToString(),
                ["camera"] = CameraReport(Doc.CurrentViewpoint),
                ["requestedDirection"] = new JArray(dir.Select(d => R3(d / len))),
                ["saved"] = save,
                ["saveReminder"] = SaveNote()
            };
        }

        private sealed class Stop { public double[] Pos; public double[] Dir; public string Label; }

        // Builds an animation that walks through door openings (or along waypoints you give).
        // Doors: for each door the camera starts in front of it, passes through the opening at eye height and ends inside, always looking the way it moves.
        // Between doors the camera moves in a straight line, so give extra waypoints for corridors or turns.
        private static JToken CreateWalkthrough(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            if (FindViewpointByName(Doc.SavedViewpoints.Value, name) != null) throw new ArgumentException("A viewpoint or animation named '" + name + "' already exists. Use another name.");
            double upm = UnitsPerMetre();
            double eye = ((double?)a["eyeHeightM"] ?? 1.65) * upm;
            double appr = ((double?)a["approachM"] ?? 2.5) * upm;
            double inside = ((double?)a["insideM"] ?? 2.5) * upm;
            int maxDoors = Math.Max(1, Math.Min(25, (int?)a["maxDoors"] ?? 12));
            string entry = ((string)a["entry"] ?? "inward").ToLowerInvariant();
            string order = ((string)a["order"] ?? "nearest").ToLowerInvariant();
            var stops = new List<Stop>();
            var doorsReport = new JArray();

            if (a["waypoints"] is JArray wps && wps.Count > 0)
            {
                foreach (var w in wps)
                {
                    var pos = Vec3(w["position"], "waypoint position");
                    double[] dir;
                    if (w["lookAt"] != null) { var l = Vec3(w["lookAt"], "waypoint lookAt"); dir = new[] { l[0] - pos[0], l[1] - pos[1], l[2] - pos[2] }; }
                    else if (w["direction"] != null) dir = Vec3(w["direction"], "waypoint direction");
                    else throw new ArgumentException("Each waypoint needs lookAt [x,y,z] or direction [x,y,z].");
                    stops.Add(new Stop { Pos = pos, Dir = dir, Label = "waypoint" });
                }
            }
            else
            {
                var items = ResolveItems(a);
                if (items.Count == 0) throw new ArgumentException("No doors found. Give a search (for example category Element, property Category, value Doors), a set, or waypoints.");
                double mcx = 0, mcy = 0;
                var ext = ModelExtents();
                if (ext != null)
                {
                    var mn = (JArray)ext["min"]; var mx = (JArray)ext["max"];
                    mcx = ((double)mn[0] + (double)mx[0]) / 2; mcy = ((double)mn[1] + (double)mx[1]) / 2;
                }
                var doors = new List<Stop>(); // one entry per door: Pos = centre at eye height, Dir = way in
                foreach (ModelItem it in items)
                {
                    double[] lo = { double.MaxValue, double.MaxValue, double.MaxValue }, hi = { double.MinValue, double.MinValue, double.MinValue };
                    if (!BoxOf(it, lo, hi)) continue;
                    double cx = (lo[0] + hi[0]) / 2, cy = (lo[1] + hi[1]) / 2;
                    double W = hi[0] - lo[0], H = hi[1] - lo[1];
                    double nx, ny;
                    double yaw;
                    if (ItemYaw(it, out yaw) && Math.Abs(yaw) > 0.01)
                    {
                        double rad = yaw * Math.PI / 180.0, c = Math.Cos(rad), s = Math.Sin(rad);
                        double ac = Math.Abs(c), asn = Math.Abs(s), den = ac * ac - asn * asn;
                        double sa = W, sb = H;
                        if (Math.Abs(den) > 0.15)
                        {
                            double a2 = (W * ac - H * asn) / den, b2 = (H * ac - W * asn) / den;
                            if (a2 > 1e-6 && b2 > 1e-6) { sa = a2; sb = b2; }
                        }
                        // the door is wide along its longer side; it is crossed along the other one
                        if (sa >= sb) { nx = -s; ny = c; } else { nx = c; ny = s; }
                    }
                    else if (W >= H) { nx = 0; ny = 1; } else { nx = 1; ny = 0; }
                    bool towardCentre = nx * (mcx - cx) + ny * (mcy - cy) >= 0;
                    bool flip = entry == "outward" ? towardCentre : !towardCentre;
                    if (flip) { nx = -nx; ny = -ny; }
                    doors.Add(new Stop { Pos = new[] { cx, cy, lo[2] + eye }, Dir = new[] { nx, ny, 0.0 }, Label = it.DisplayName + " " + (PropValue(it, "Item", "GUID") ?? "") });
                }
                if (doors.Count == 0) throw new ArgumentException("The doors found have no geometry to measure.");
                if (order == "nearest")
                {
                    var rest = new List<Stop>(doors); var sorted = new List<Stop>();
                    var cur = rest[0]; rest.RemoveAt(0); sorted.Add(cur);
                    while (rest.Count > 0)
                    {
                        Stop best = null; double bd = double.MaxValue;
                        foreach (var d in rest)
                        {
                            double dd = (d.Pos[0] - cur.Pos[0]) * (d.Pos[0] - cur.Pos[0]) + (d.Pos[1] - cur.Pos[1]) * (d.Pos[1] - cur.Pos[1]);
                            if (dd < bd) { bd = dd; best = d; }
                        }
                        rest.Remove(best); sorted.Add(best); cur = best;
                    }
                    doors = sorted;
                }
                if (doors.Count > maxDoors) doors = doors.Take(maxDoors).ToList();
                foreach (var d in doors)
                {
                    stops.Add(new Stop { Pos = new[] { d.Pos[0] - d.Dir[0] * appr, d.Pos[1] - d.Dir[1] * appr, d.Pos[2] }, Dir = d.Dir, Label = "before door" });
                    stops.Add(new Stop { Pos = new[] { d.Pos[0], d.Pos[1], d.Pos[2] }, Dir = d.Dir, Label = "in door" });
                    stops.Add(new Stop { Pos = new[] { d.Pos[0] + d.Dir[0] * inside, d.Pos[1] + d.Dir[1] * inside, d.Pos[2] }, Dir = d.Dir, Label = "inside" });
                    doorsReport.Add(new JObject { ["door"] = d.Label, ["centre"] = new JArray(d.Pos.Select(R3)), ["wayIn"] = new JArray(R3(d.Dir[0]), R3(d.Dir[1])) });
                }
            }
            if (stops.Count < 2) throw new ArgumentException("A walkthrough needs at least 2 stops.");
            if (stops.Count > 80) throw new ArgumentException("Too many stops (" + stops.Count + "). Use fewer doors or waypoints (maximum 80 frames).");

            var d0 = Doc;
            Viewpoint original = null; try { original = d0.CurrentViewpoint.CreateCopy(); } catch { }
            bool sectionOff = (bool?)a["keepSection"] == true ? false : DisableSection();
            var frames = new List<Viewpoint>();
            if ((bool?)a["includeCurrentView"] ?? true) { try { frames.Add(d0.CurrentViewpoint.CreateCopy()); } catch { } }
            foreach (var s in stops) frames.Add(MakeCamera(s.Pos, s.Dir));

            var anim = NewAnimation(name, frames[0]);
            d0.SavedViewpoints.AddCopy(anim);
            var live = FindAnim(name);
            var coll = SavedColl(true);
            var ins = FindMethod(coll, "InsertCopy", 3);
            if (ins == null) throw new InvalidOperationException("This Navisworks version has no InsertCopy call, so frames cannot be added to an animation from AI Connect.");
            for (int i = 0; i < frames.Count; i++)
                ins.Invoke(coll, new object[] { live, i, new SavedViewpoint(frames[i]) { DisplayName = name + " " + (i + 1) } });
            if (original != null) { try { d0.CurrentViewpoint.CopyFrom(original); } catch { } }
            int count = 0; try { foreach (SavedItem ch in FindAnim(name).Children) if (ch is SavedViewpoint) count++; } catch { }
            var first = frames.Count > 1 ? CameraReport(frames[1]) : null;
            return new JObject
            {
                ["created"] = name, ["units"] = d0.Units.ToString(),
                ["framesRequested"] = frames.Count, ["framesInAnimation"] = count,
                ["doors"] = doorsReport, ["sectionBoxSwitchedOff"] = sectionOff,
                ["firstWalkFrame"] = first,
                ["note"] = "Between stops the camera moves in a straight line, so it can pass through walls where there is no door. Add waypoints for corridors and turns. The length of the animation is set by Navisworks (more frames = longer).",
                ["saveReminder"] = SaveNote()
            };
        }
    }
}
