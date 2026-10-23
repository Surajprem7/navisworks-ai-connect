using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.Navisworks.Api;
using Newtonsoft.Json.Linq;

namespace NavisBridge
{
    // Tools added in 1.7.1: pauses (cuts), smoothing and loop settings, and deleting animations.
    // The calls whose exact shape differs between Navisworks versions are made by name (reflection) and report what they found.
    public static partial class Tools
    {
        private static JToken RunExtra3(string tool, JObject a)
        {
            switch (tool)
            {
                case "set_animation_options": return SetAnimationOptions(a);
                case "add_animation_cuts": return AddAnimationCuts(a);
                case "delete_animations": return DeleteAnimations(a);
                default: return null;
            }
        }

        private static Exception Inner(Exception e) { return e is TargetInvocationException && e.InnerException != null ? e.InnerException : e; }

        // ---------- smoothing and loop ----------

        private static JObject ApplyAnimationOptions(string name, string smoothing, bool? loops)
        {
            var an = FindAnim(name);
            var ps = typeof(SavedViewpointAnimation).GetProperty("Smoothing");
            var pl = typeof(SavedViewpointAnimation).GetProperty("Loops");
            object smVal = null;
            if (!string.IsNullOrWhiteSpace(smoothing))
            {
                if (ps == null) throw new InvalidOperationException("This Navisworks version has no animation Smoothing property.");
                if (!ps.PropertyType.IsEnum) throw new InvalidOperationException("Smoothing is not a list of named choices in this Navisworks version (type " + ps.PropertyType.Name + ").");
                string want = smoothing.Trim();
                if (string.Equals(want, "synchronize", StringComparison.OrdinalIgnoreCase) || string.Equals(want, "synchronise", StringComparison.OrdinalIgnoreCase)) want = "Sync";
                try { smVal = Enum.Parse(ps.PropertyType, want, true); }
                catch { throw new ArgumentException("smoothing must be one of: " + string.Join(", ", Enum.GetNames(ps.PropertyType)) + " (Sync = Synchronize Angular/Linear Speeds)"); }
            }
            if (smVal == null && !loops.HasValue) throw new ArgumentException("Give smoothing and/or loops.");
            if (loops.HasValue && (pl == null || !pl.CanWrite)) throw new InvalidOperationException("This Navisworks version has no animation Loops property.");

            // read by name, because these two properties are not public in every Navisworks version
            Func<SavedViewpointAnimation, string> getSm = x => { try { return ps == null ? null : Convert.ToString(ps.GetValue(x, null)); } catch { return null; } };
            Func<SavedViewpointAnimation, bool?> getLoop = x => { try { return pl == null ? (bool?)null : (bool)pl.GetValue(x, null); } catch { return null; } };
            Func<SavedViewpointAnimation, bool> ok = x =>
                (smVal == null || string.Equals(getSm(x), smVal.ToString(), StringComparison.OrdinalIgnoreCase)) &&
                (!loops.HasValue || getLoop(x) == loops.Value);

            string how = "in place"; string inPlaceError = null;
            try
            {
                if (smVal != null) ps.SetValue(an, smVal, null);
                if (loops.HasValue) pl.SetValue(an, loops.Value, null);
            }
            catch (Exception e) { inPlaceError = Inner(e).Message; }

            // Saved items are normally changed through a copy that replaces the original; try that when the direct change did not stick.
            if (inPlaceError != null || !ok(FindAnim(name)))
            {
                var cur = FindAnim(name);
                var copy = cur.CreateCopy() as SavedViewpointAnimation;
                if (copy == null) throw new InvalidOperationException("The animation could not be changed directly" + (inPlaceError != null ? " (" + inPlaceError + ")" : "") + " and could not be copied.");
                if (smVal != null) ps.SetValue(copy, smVal, null);
                if (loops.HasValue) pl.SetValue(copy, loops.Value, null);
                var coll = SavedColl(true);
                var m = FindMethod(coll, "ReplaceWithCopy", 2);
                if (m == null) throw new InvalidOperationException("The animation could not be changed directly" + (inPlaceError != null ? " (" + inPlaceError + ")" : "") + " and this version has no ReplaceWithCopy call.");
                m.Invoke(coll, new object[] { cur, copy });
                how = "by replacing it with a changed copy";
            }
            var after = FindAnim(name);
            var o = new JObject { ["animation"] = name, ["changed"] = how, ["smoothing"] = getSm(after), ["loops"] = getLoop(after), ["verified"] = ok(after) };
            if (!ok(after)) o["warning"] = "The value read back is not the one asked for. Check it in Edit Animation (right-click the animation > Edit).";
            return o;
        }

        private static JToken SetAnimationOptions(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            var o = ApplyAnimationOptions(name, (string)a["smoothing"], (bool?)a["loops"]);
            o["saveReminder"] = SaveNote();
            return o;
        }

        // ---------- cuts (pauses) ----------

        private static void SetCutDelay(object cut, double seconds)
        {
            // Delay is not public in every version, so a non-public one is used too
            var p = cut.GetType().GetProperty("Delay", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p == null || p.GetSetMethod(true) == null)
                throw new InvalidOperationException("A cut was created but its Delay cannot be set in this Navisworks version. Cut properties: " +
                    string.Join(", ", cut.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Select(x => x.Name + (x.GetSetMethod(true) != null ? "(set)" : ""))));
            object val;
            if (p.PropertyType == typeof(TimeSpan)) val = TimeSpan.FromSeconds(seconds);
            else val = Convert.ChangeType(seconds, p.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
            p.SetValue(cut, val, null);
        }

        private static SavedViewpointAnimationCut NewCut(string name, double seconds)
        {
            var t = typeof(SavedViewpointAnimationCut);
            var ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Exception last = null;
            foreach (var c in ctors)
            {
                var ps = c.GetParameters();
                object[] args = null;
                if (ps.Length == 0) args = new object[0];
                else if (ps.Length == 1 && ps[0].ParameterType == typeof(string)) args = new object[] { name };
                if (args == null) continue;
                try
                {
                    var cut = c.Invoke(args) as SavedViewpointAnimationCut;
                    if (cut == null) continue;
                    cut.DisplayName = name;
                    SetCutDelay(cut, seconds);
                    return cut;
                }
                catch (Exception e) { last = Inner(e); }
            }
            throw new InvalidOperationException("Could not create a cut in this Navisworks version" + (last != null ? " (" + last.Message + ")" : "") +
                ". Constructors found: " + string.Join("; ", ctors.Select(c => "(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)) + ")")));
        }

        // Index (within the animation's children) just after the n-th frame, or -1 when there are fewer frames.
        private static int IndexAfterFrame(SavedViewpointAnimation an, int n)
        {
            int idx = 0, frames = 0;
            foreach (SavedItem ch in an.Children)
            {
                idx++;
                if (ch is SavedViewpoint) { frames++; if (frames == n) return idx; }
            }
            return -1;
        }

        private static int CountFrames(SavedViewpointAnimation an)
        {
            int f = 0; foreach (SavedItem ch in an.Children) if (ch is SavedViewpoint) f++;
            return f;
        }

        // Adds a pause of "seconds" after each listed frame (1-based). Highest frame first so earlier numbers stay valid.
        private static JObject InsertCuts(string animName, List<KeyValuePair<int, double>> cuts)
        {
            var coll = SavedColl(true);
            var ins = FindMethod(coll, "InsertCopy", 3);
            if (ins == null) throw new InvalidOperationException("This Navisworks version has no InsertCopy call, so cuts cannot be added from AI Connect.");
            int before = 0, added = 0;
            var errors = new JArray();
            before = CountCuts(FindAnim(animName));
            foreach (var c in cuts.OrderByDescending(x => x.Key))
            {
                try
                {
                    var live = FindAnim(animName);
                    int at = IndexAfterFrame(live, c.Key);
                    if (at < 0) { errors.Add("frame " + c.Key + " does not exist"); continue; }
                    var cut = NewCut("Pause after frame " + c.Key, c.Value);
                    ins.Invoke(coll, new object[] { live, at, cut });
                    added++;
                }
                catch (Exception e) { errors.Add("after frame " + c.Key + ": " + Inner(e).Message); if (added == 0) break; }
            }
            var finalAnim = FindAnim(animName);
            int after = CountCuts(finalAnim);
            var delays = new JArray();
            foreach (SavedItem ch in finalAnim.Children)
                if (ch is SavedViewpointAnimationCut) { double dv; double.TryParse(Prop(ch, "Delay"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out dv); delays.Add(dv); }
            return new JObject { ["cutsRequested"] = cuts.Count, ["cutsAdded"] = added, ["cutsInAnimationBefore"] = before, ["cutsInAnimationAfter"] = after, ["delaySecondsNowInAnimation"] = delays, ["errors"] = errors };
        }

        private static int CountCuts(SavedViewpointAnimation an)
        {
            int n = 0; foreach (SavedItem ch in an.Children) if (ch is SavedViewpointAnimationCut) n++;
            return n;
        }

        private static JToken AddAnimationCuts(JObject a)
        {
            string name = (string)a["name"] ?? throw new ArgumentException("name is required");
            var an = FindAnim(name);
            int frames = CountFrames(an);
            var cuts = new List<KeyValuePair<int, double>>();
            if (a["cuts"] is JArray arr && arr.Count > 0)
            {
                foreach (var c in arr)
                {
                    int f = (int?)c["afterFrame"] ?? throw new ArgumentException("each cut needs afterFrame (1-based frame number)");
                    double s = (double?)c["seconds"] ?? 1.0;
                    cuts.Add(new KeyValuePair<int, double>(f, s));
                }
            }
            else if (a["everyFrameSeconds"] != null)
            {
                double s = (double)a["everyFrameSeconds"];
                for (int f = 1; f < frames; f++) cuts.Add(new KeyValuePair<int, double>(f, s));
            }
            else throw new ArgumentException("Give cuts [{afterFrame, seconds}] or everyFrameSeconds.");
            if (cuts.Any(c => c.Value <= 0 || c.Value > 600)) throw new ArgumentException("A pause must be between 0 and 600 seconds.");
            var o = InsertCuts(name, cuts);
            o["animation"] = name; o["frames"] = frames; o["saveReminder"] = SaveNote();
            return o;
        }

        // ---------- delete animations ----------

        private static JToken DeleteAnimations(JObject a)
        {
            var names = (a["names"] as JArray)?.Select(x => (string)x).ToList() ?? new List<string>();
            if (names.Count == 0) throw new ArgumentException("names is required (exact animation names).");
            bool dry = (bool?)a["dryRun"] ?? false;
            var removed = new JArray(); var notFound = new JArray();
            foreach (var n in names)
            {
                SavedViewpointAnimation an;
                try { an = FindAnim(n); } catch { notFound.Add(n); continue; }
                if (!dry) Doc.SavedViewpoints.Remove(an);
                removed.Add(n);
            }
            return new JObject { [dry ? "wouldDelete" : "removed"] = removed, ["notFound"] = notFound, ["dryRun"] = dry, ["saveReminder"] = SaveNote() };
        }
    }
}
