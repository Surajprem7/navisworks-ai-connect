using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using Newtonsoft.Json.Linq;

namespace NavisBridge
{
    /// <summary>
    /// Loads with Navisworks, checks GitHub (at most once a day) for a newer release and asks the user whether to update.
    /// Nothing is installed without the user's consent; the downloaded installer is verified against the published SHA-256.
    /// Disable completely with the environment variable AI_CONNECT_NO_UPDATE_CHECK=1.
    /// </summary>
    [Plugin("NavisBridge.Updater", "TXGL", DisplayName = "AI Connect updater")]
    public class UpdateWatcher : EventWatcherPlugin
    {
        private static Timer _timer;

        static UpdateWatcher()
        {
            // make sure the Newtonsoft resolver in BridgePlugin's static constructor is registered
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(BridgePlugin).TypeHandle);
        }

        public override void OnLoaded()
        {
            try
            {
                if (Environment.GetEnvironmentVariable("AI_CONNECT_NO_UPDATE_CHECK") == "1") return;
                // Runs on the Navisworks UI thread: this timer ticks there as well.
                _timer = new Timer { Interval = 25000 };
                _timer.Tick += (s, e) => { _timer.Stop(); _timer.Dispose(); _timer = null; Updater.Start(); };
                _timer.Start();
            }
            catch (Exception ex) { BridgePlugin.Log("updater load: " + ex.Message); }
        }

        public override void OnUnloading() { }
    }

    internal static class Updater
    {
        const string Repo = "Surajprem7/navisworks-ai-connect";
        const string AssetName = "AI-Connect-Setup.exe";
        const int StrongReminderDays = 7;

        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI Connect");
        static readonly string SettingsFile = Path.Combine(Dir, "update.json");

        class Found { public string Tag, Url, ShaUrl, Page; public DateTime Published; }
        static volatile Found _found;
        static volatile string _error;
        static volatile bool _done;
        static Timer _poll;

        static JObject Load()
        {
            try { if (File.Exists(SettingsFile)) return JObject.Parse(File.ReadAllText(SettingsFile)); } catch { }
            return new JObject();
        }
        static void Save(JObject o)
        {
            try { Directory.CreateDirectory(Dir); File.WriteAllText(SettingsFile, o.ToString()); } catch { }
        }

        static int[] Parse(string v)
        {
            var r = new int[3];
            var p = (v ?? "").TrimStart('v', 'V').Split('.');
            for (int i = 0; i < 3 && i < p.Length; i++) { int n; int.TryParse(new string(Array.FindAll(p[i].ToCharArray(), char.IsDigit)), out n); r[i] = n; }
            return r;
        }
        static bool Newer(string a, string b)
        {
            var x = Parse(a); var y = Parse(b);
            for (int i = 0; i < 3; i++) if (x[i] != y[i]) return x[i] > y[i];
            return false;
        }

        public static void Start()
        {
            try
            {
                var s = Load();
                var last = (DateTime?)s["lastCheckUtc"];
                if (last.HasValue && (DateTime.UtcNow - last.Value).TotalHours < 24)
                {
                    // Checked recently: only re-show a stored, week-old pending update (no network).
                    var p = s["pending"] as JObject;
                    if (p == null) return;
                    var f = new Found { Tag = (string)p["tag"], Url = (string)p["url"], ShaUrl = (string)p["sha"], Page = (string)p["page"], Published = (DateTime)p["published"] };
                    if (!Newer(f.Tag, BridgePlugin.Ver)) { s.Remove("pending"); Save(s); return; }
                    if (AlreadyQueued(s, f.Tag)) return;
                    if ((DateTime.UtcNow - f.Published).TotalDays >= StrongReminderDays && !((bool?)s["autoUpdate"] ?? false)) Prompt(s, f);
                    return;
                }
                _done = false;
                Task.Run(() => Check());
                _poll = new Timer { Interval = 2000 };
                _poll.Tick += (a, b) =>
                {
                    if (!_done) return;
                    _poll.Stop(); _poll.Dispose(); _poll = null;
                    try { OnResult(); } catch (Exception ex) { BridgePlugin.Log("updater ui: " + ex); }
                };
                _poll.Start();
            }
            catch (Exception ex) { BridgePlugin.Log("updater start: " + ex.Message); }
        }

        static void Check()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "ai-connect-updater";
                    wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    var json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
                    var rel = JObject.Parse(json);
                    var f = new Found { Tag = (string)rel["tag_name"], Page = (string)rel["html_url"], Published = ((DateTime?)rel["published_at"] ?? DateTime.UtcNow).ToUniversalTime() };
                    foreach (var a in (JArray)rel["assets"])
                    {
                        var n = (string)a["name"]; var u = (string)a["browser_download_url"];
                        if (n == AssetName) f.Url = u;
                        else if (n == AssetName + ".sha256") f.ShaUrl = u;
                    }
                    _found = f;
                }
            }
            catch (Exception ex) { _error = ex.Message; BridgePlugin.Log("update check failed: " + ex.Message); }
            finally { _done = true; }
        }

        static bool SafeUrl(string u)
        {
            return u != null && u.StartsWith("https://github.com/" + Repo + "/releases/download/", StringComparison.Ordinal);
        }

        // A verified installer was already downloaded for this version in the last 2 days (waiting for Navisworks to close).
        static bool AlreadyQueued(JObject s, string tag)
        {
            var d = (DateTime?)s["downloadedUtc"];
            return (string)s["downloadedTag"] == tag && d.HasValue && (DateTime.UtcNow - d.Value).TotalDays < 2;
        }

        static void OnResult()
        {
            var s = Load();
            s["lastCheckUtc"] = DateTime.UtcNow;
            var f = _found;
            if (f == null || !SafeUrl(f.Url) || !SafeUrl(f.ShaUrl) || !Newer(f.Tag, BridgePlugin.Ver)) { s.Remove("pending"); Save(s); return; }
            s["pending"] = new JObject { ["tag"] = f.Tag, ["url"] = f.Url, ["sha"] = f.ShaUrl, ["page"] = f.Page, ["published"] = f.Published };
            Save(s);
            if (AlreadyQueued(s, f.Tag)) return;
            if ((bool?)s["autoUpdate"] ?? false) { Download(f, false); return; }
            Prompt(s, f);
        }

        static void Prompt(JObject s, Found f)
        {
            double age = (DateTime.UtcNow - f.Published).TotalDays;
            bool strong = age >= StrongReminderDays;
            var snooze = (DateTime?)s["snoozeUntilUtc"];
            if (snooze.HasValue && DateTime.UtcNow < snooze.Value && !strong) return;
            bool autoOpt; var choice = Ask(f, strong, (int)age, out autoOpt);
            if (autoOpt) s["autoUpdate"] = true;
            if (choice == DialogResult.Yes) { s.Remove("snoozeUntilUtc"); Save(s); Download(f, true); }
            else { if (!strong) s["snoozeUntilUtc"] = DateTime.UtcNow.AddDays(1); Save(s); }
        }

        static DialogResult Ask(Found f, bool strong, int ageDays, out bool autoOpt)
        {
            using (var form = new Form())
            {
                form.Text = "AI Connect update";
                form.FormBorderStyle = FormBorderStyle.FixedDialog; form.MaximizeBox = false; form.MinimizeBox = false;
                form.StartPosition = FormStartPosition.CenterScreen; form.ClientSize = new Size(460, 190); form.TopMost = true;
                var msg = new Label { Left = 16, Top = 14, Width = 430, Height = 80, Text =
                    "AI Connect " + f.Tag.TrimStart('v') + " is available (you have " + BridgePlugin.Ver + ")." +
                    (strong ? "\r\n\r\nThis release is " + ageDays + " days old. Updating is recommended." : "") +
                    "\r\n\r\nThe update installs when you close Navisworks (Windows will ask for administrator permission)." };
                var chk = new CheckBox { Left = 16, Top = 100, Width = 430, Text = "Update automatically in the future (I will still be asked by Windows)" };
                var yes = new Button { Text = "Update", Left = 196, Top = 142, Width = 120, DialogResult = DialogResult.Yes };
                var no = new Button { Text = strong ? "Not now" : "Remind me later", Left = 326, Top = 142, Width = 120, DialogResult = DialogResult.No };
                var web = new LinkLabel { Text = "What's new", Left = 16, Top = 148, Width = 100 };
                web.LinkClicked += (a, b) => { try { Process.Start(f.Page); } catch { } };
                form.Controls.AddRange(new Control[] { msg, chk, yes, no, web });
                form.AcceptButton = yes; form.CancelButton = no;
                var r = form.ShowDialog();
                autoOpt = chk.Checked && r == DialogResult.Yes;
                return r;
            }
        }

        static void Download(Found f, bool interactive)
        {
            if (interactive) { Cursor.Current = Cursors.WaitCursor; DoDownload(f, true); Cursor.Current = Cursors.Default; }
            else Task.Run(() => DoDownload(f, false));
        }

        static void DoDownload(Found f, bool interactive)
        {
            string err = null;
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var exe = Path.Combine(Path.GetTempPath(), "AI-Connect-Setup-" + f.Tag.TrimStart('v') + ".exe");
                string shaText;
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "ai-connect-updater";
                    shaText = wc.DownloadString(f.ShaUrl);
                    wc.DownloadFile(f.Url, exe);
                }
                var want = shaText.Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();
                string got;
                using (var fs = File.OpenRead(exe)) got = BitConverter.ToString(SHA256.Create().ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                if (want.Length != 64 || want != got) { try { File.Delete(exe); } catch { } throw new Exception("Checksum mismatch - update cancelled."); }
                RunAfterNavisworksCloses(exe);
                var st = Load(); st["downloadedTag"] = f.Tag; st["downloadedUtc"] = DateTime.UtcNow; Save(st);
                BridgePlugin.Log("update " + f.Tag + " downloaded and verified");
            }
            catch (Exception ex) { err = ex.Message; BridgePlugin.Log("update download failed: " + ex.Message); }
            if (interactive)
                MessageBox.Show(err == null ? "AI Connect " + f.Tag.TrimStart('v') + " was downloaded and verified.\r\nIt installs when you close Navisworks (Windows will ask for permission)." : "AI Connect update failed: " + err,
                    "AI Connect", MessageBoxButtons.OK, err == null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        static void RunAfterNavisworksCloses(string exe)
        {
            var ps = "while (Get-Process roamer -ErrorAction SilentlyContinue) { Start-Sleep 3 }; Start-Process -FilePath '" + exe.Replace("'", "''") + "' -ArgumentList '/SILENT','/NORESTART' -Verb RunAs";
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"" + ps.Replace("\"", "\\\"") + "\"")
            { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
            Process.Start(psi);
        }
    }
}
