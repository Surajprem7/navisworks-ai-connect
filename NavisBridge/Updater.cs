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
                _timer = new Timer { Interval = 8000 };
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
            // Also accepts the repository's planned new name, so a rename does not break updates.
            return u != null && (u.StartsWith("https://github.com/" + Repo + "/releases/download/", StringComparison.Ordinal)
                || u.StartsWith("https://github.com/Surajprem7/navisworks-mcp/releases/download/", StringComparison.Ordinal));
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
            BridgePlugin.Log("update check: installed=" + BridgePlugin.Ver + " latest=" + (f == null ? "(none) " + _error : f.Tag) + " newer=" + (f != null && Newer(f.Tag, BridgePlugin.Ver)));
            if (f == null || !SafeUrl(f.Url) || !SafeUrl(f.ShaUrl) || !Newer(f.Tag, BridgePlugin.Ver)) { s.Remove("pending"); Save(s); return; }
            s["pending"] = new JObject { ["tag"] = f.Tag, ["url"] = f.Url, ["sha"] = f.ShaUrl, ["page"] = f.Page, ["published"] = f.Published };
            Save(s);
            // Auto-update mode: download quietly (once per version). Otherwise ask every time Navisworks starts while an update is available.
            s.Remove("autoUpdate"); // always ask: no way to switch the prompt off
            Prompt(s, f);
        }

        static void Prompt(JObject s, Found f)
        {
            double age = (DateTime.UtcNow - f.Published).TotalDays;
            bool strong = age >= StrongReminderDays;
            BridgePlugin.Log("update prompt shown for " + f.Tag);
            bool autoOpt; var choice = Ask(f, strong, (int)age, out autoOpt);
            BridgePlugin.Log("update prompt answer: " + choice);
            if (choice == DialogResult.Yes) { s.Remove("snoozeUntilUtc"); Save(s); Download(f, true); }
            else { s.Remove("snoozeUntilUtc"); Save(s); }
        }

        static DialogResult Ask(Found f, bool strong, int ageDays, out bool autoOpt)
        {
            autoOpt = false;
            using (var form = new Form())
            {
                form.Text = "AI Connect update";
                form.FormBorderStyle = FormBorderStyle.FixedDialog; form.MaximizeBox = false; form.MinimizeBox = false;
                form.StartPosition = FormStartPosition.CenterScreen; form.ClientSize = new Size(640, 300); form.TopMost = true;
                var title = new Label { Left = 24, Top = 20, Width = 590, Height = 40, Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                    Text = "New version " + f.Tag.TrimStart('v') + " is ready" };
                var msg = new Label { Left = 24, Top = 72, Width = 590, Height = 120, Font = new Font("Segoe UI", 12f), Text =
                    "You have " + BridgePlugin.Ver + "." +
                    (strong ? "\r\nThis release is " + ageDays + " days old." : "") +
                    "\r\n\r\nClick Update. It installs when you close Navisworks." };
                var yes = new Button { Text = "Update", Left = 270, Top = 220, Width = 160, Height = 44, Font = new Font("Segoe UI", 12f, FontStyle.Bold), DialogResult = DialogResult.Yes };
                var no = new Button { Text = "Ask me next time", Left = 446, Top = 220, Width = 168, Height = 44, Font = new Font("Segoe UI", 11f), DialogResult = DialogResult.No };
                var web = new LinkLabel { Text = "What's new", Left = 24, Top = 232, Width = 160, Font = new Font("Segoe UI", 11f) };
                web.LinkClicked += (a, b) => { try { Process.Start(f.Page); } catch { } };
                form.Controls.AddRange(new Control[] { title, msg, web, yes, no });
                form.AcceptButton = yes; form.CancelButton = no;
                return form.ShowDialog();
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
                RunAfterNavisworksCloses(exe, want);
                var st = Load(); st["downloadedTag"] = f.Tag; st["downloadedUtc"] = DateTime.UtcNow; Save(st);
                BridgePlugin.Log("update " + f.Tag + " downloaded and verified");
            }
            catch (Exception ex) { err = ex.Message; BridgePlugin.Log("update download failed: " + ex.Message); }
            if (interactive)
                MessageBox.Show(err == null ? "AI Connect " + f.Tag.TrimStart('v') + " was downloaded and verified.\r\nIt installs when you close Navisworks (Windows will ask for permission)." : "AI Connect update failed: " + err,
                    "AI Connect", MessageBoxButtons.OK, err == null ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        static void RunAfterNavisworksCloses(string exe, string sha256)
        {
            // The installer may sit in %TEMP% for hours, so it is hashed again right before it is started with admin rights.
            var q = exe.Replace("'", "''");
            var ps = "while (Get-Process roamer -ErrorAction SilentlyContinue) { Start-Sleep 3 }; " +
                     "if ((Get-FileHash -LiteralPath '" + q + "' -Algorithm SHA256).Hash.ToLower() -eq '" + sha256 + "') { " +
                     "Start-Process -FilePath '" + q + "' -ArgumentList '/SILENT','/NORESTART' -Verb RunAs } else { Remove-Item -LiteralPath '" + q + "' -Force -ErrorAction SilentlyContinue }";
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"" + ps.Replace("\"", "\\\"") + "\"")
            { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden };
            Process.Start(psi);
        }
    }
}
