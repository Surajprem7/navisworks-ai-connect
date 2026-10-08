using System;
using System.IO;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api.Plugins;
using Newtonsoft.Json.Linq;

namespace NavisBridge
{
    /// <summary>
    /// Ribbon button (Add-ins tab): starts/stops a localhost HTTP bridge.
    /// All Navisworks API calls are marshalled onto the Navisworks UI thread.
    /// </summary>
    [Plugin("NavisBridge.Link", "TXGL", DisplayName = "AI Connect")]
    [RibbonLayout("ClaudeRibbon.xaml")]
    [RibbonTab("ID_ClaudeTab", DisplayName = "AI Connect")]
    [Command("ID_ClaudeLink_Toggle", DisplayName = "AI Connect", CanToggle = true,
        Icon = "Resources\\claude16.png", LargeIcon = "Resources\\claude32.png",
        ToolTip = "AI Connect: connect or disconnect the AI assistant",
        ExtendedToolTip = "Starts a local link (127.0.0.1:47800) so an AI assistant can search, select, create clash tests and read results. Click again to disconnect.")]
    public class BridgePlugin : CommandHandlerPlugin
    {
        public const int Port = 47800;
        internal static void Log(string m)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "navisbridge.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " [t" + Thread.CurrentThread.ManagedThreadId + "] " + m + "\r\n"); } catch { }
        }
        private static HttpListener _listener;
        private static string _token;
        // Work queued by the HTTP thread, drained on the Navisworks UI thread by a WinForms timer.
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private static System.Windows.Forms.Timer _timer;

        static BridgePlugin()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                try
                {
                    var name = new System.Reflection.AssemblyName(e.Name).Name;
                    var dir = Path.GetDirectoryName(typeof(BridgePlugin).Assembly.Location);
                    var path = Path.Combine(dir, name + ".dll");
                    Log("resolve " + name + " -> " + path + " exists=" + File.Exists(path));
                    return File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
                }
                catch { return null; }
            };
        }

        public override CommandState CanExecuteCommand(string commandId)
        {
            return new CommandState(true) { IsChecked = _listener != null && _listener.IsListening };
        }

        public override int ExecuteCommand(string commandId, params string[] parameters)
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
                _listener = null;
                if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
                System.Windows.Forms.MessageBox.Show("AI Connect disconnected.", "AI Connect", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                return 0;
            }

            // Execute() runs on the Navisworks UI thread, so a timer created here ticks on that thread.
            _timer = new System.Windows.Forms.Timer { Interval = 50 };
            _timer.Tick += (s, e) =>
            {
                Action a;
                int guard = 0;
                while (guard++ < 20 && _queue.TryDequeue(out a)) { Log("tick dequeue"); a(); }
            };
            _timer.Start();

            // Optional shared secret. If NAVIS_BRIDGE_TOKEN is set, requests must send header X-Bridge-Token.
            _token = Environment.GetEnvironmentVariable("NAVIS_BRIDGE_TOKEN");

            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + Port + "/");
            _listener.Start(); Log("listener started");
            Task.Run(() => Loop(_listener));
            System.Windows.Forms.MessageBox.Show("AI Connect is on (127.0.0.1:" + Port + ").\nClick AI Connect again to disconnect.", "AI Connect", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            return 0;
        }

        private static async Task Loop(HttpListener l)
        {
            while (l.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await l.GetContextAsync(); }
                catch (Exception ex) { Log("accept failed: " + ex.Message); break; }
                Log("accepted " + ctx.Request.HttpMethod);
                _ = Task.Run(() =>
                {
                    try { Handle(ctx); }
                    catch (Exception ex)
                    {
                        Log("Handle crash: " + ex);
                        try
                        {
                            var b = Encoding.UTF8.GetBytes("{\"ok\":false,\"error\":\"bridge crash: " + ex.GetType().Name + ": " + ex.Message.Replace("\\", "/").Replace("\"", "'").Replace("\r", " ").Replace("\n", " ") + "\"}");
                            ctx.Response.ContentType = "application/json";
                            ctx.Response.OutputStream.Write(b, 0, b.Length);
                            ctx.Response.Close();
                        }
                        catch { }
                    }
                });
            }
        }

        private static void Handle(HttpListenerContext ctx)
        {
            string result;
            Log("Handle start");
            try
            {
                if (!string.IsNullOrEmpty(_token) && ctx.Request.Headers["X-Bridge-Token"] != _token)
                    throw new UnauthorizedAccessException("bad token");

                string body;
                using (var r = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) body = r.ReadToEnd();
                var req = JObject.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                string tool = (string)req["tool"];
                var args = req["args"] as JObject ?? new JObject();

                JToken output = null;
                Exception err = null;
                var done = new ManualResetEventSlim(false);
                _queue.Enqueue(() =>
                {
                    try { Log("run " + tool); output = Tools.Run(tool, args); Log("run done"); }
                    catch (Exception ex) { err = ex; Log("run err " + ex); }
                    finally { done.Set(); }
                });
                if (!done.Wait(TimeSpan.FromSeconds(45))) throw new TimeoutException("Navisworks did not respond within 45s (busy, or a dialog is open?)");

                result = err == null
                    ? new JObject { ["ok"] = true, ["result"] = output }.ToString()
                    : new JObject { ["ok"] = false, ["error"] = err.GetType().Name + ": " + err.Message }.ToString();
            }
            catch (Exception ex)
            {
                result = new JObject { ["ok"] = false, ["error"] = ex.Message }.ToString();
            }

            Log("responding");
            var bytes = Encoding.UTF8.GetBytes(result);
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }
    }
}
