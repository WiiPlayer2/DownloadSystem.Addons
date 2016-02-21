using DownloadSystem.Shared;
using Jayrock.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebInterface
{
    public class WebInterface : IDownloadsInterface, IConfigurable, ISystemInterface, IInvokerInterface
    {
        private HttpListener listener;
        private Thread thread;
        private Dictionary<string, Func<Dictionary<string, string>, object>> actions;
        private Configurator config;

        public WebInterface()
        {
            config = new Configurator(this);
            thread = new Thread(new ThreadStart(() =>
            {
                listener.Start();
                while (true)
                {
                    try
                    {
                        var context = listener.GetContext();
                        new Thread(new ThreadStart(() => HandleContext(context))).Start();
                    }
                    catch (Exception e)
                    {

                    }
                }
            }));
            SetupActions();
        }

        private void SetupActions()
        {
            actions = new Dictionary<string, Func<Dictionary<string, string>, object>>();

            actions["/downloads/get"] = GetDownloads;
            actions["/download/get"] = GetDownload;
            actions["/download/archive"] = ArchiveDownload;
            actions["/download/remove"] = RemoveDownload;
            actions["/system/shutdown"] = SystemShutdown;
            actions["/updater/update"] = Update;
            actions["/console/exec"] = ConsoleExec;
        }

        private object ConsoleExec(Dictionary<string, string> arg)
        {
            var console = InvokerDatabase.GetInvoker("consoleio");
            var res = console.Invoke<string>("command", arg["cmd"]);
            return res;
        }

        private object Update(Dictionary<string, string> arg)
        {
            var invoker = InvokerDatabase.GetInvoker("updater");
            var result = invoker.Invoke<bool>("update");
            return result;
        }

        private object SystemShutdown(Dictionary<string, string> arg)
        {
            System.Shutdown();
            return null;
        }

        private object RemoveDownload(Dictionary<string, string> arg)
        {
            throw new NotImplementedException();
        }

        private object ArchiveDownload(Dictionary<string, string> arg)
        {
            var d = DownloadsDatabase.GetDownload(int.Parse(arg["id"]));
            DownloadsDatabase.UnregisterDownload(d, false);
            return null;
        }

        private object GetDownloads(Dictionary<string, string> arg)
        {
            var ret = new JsonArray();
            foreach(var d in DownloadsDatabase.GetDownloads().ToArray())
            {
                ret.Add(WrapDownload(d));
            }
            return ret;
        }

        private object GetDownload(Dictionary<string, string> arg)
        {
            var d = DownloadsDatabase.GetDownload(int.Parse(arg["id"]));
            return WrapDownload(d);
        }

        private JsonObject WrapDownload(IDownload d)
        {
            var ret = new JsonObject();
            ret["id"] = d.ID;
            ret["name"] = d.Name;
            ret["state"] = d.Status;
            ret["size"] = d.Size;
            ret["sizeStr"] = d.Size.ToReadableByteSize();
            ret["progressStr"] = string.Format("{0:0.##}%", d.Progress * 100);
            ret["downSpeedStr"] = string.Format("{0}/s", ((long)d.DownloadSpeed).ToReadableByteSize());
            ret["eta"] = "N/A (Implement me, bitch!)";
            ret["savePath"] = d.Path;
            return ret;
        }

        private void HandleContext(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            var res = "";
            byte[] resBytes = null;
            try
            {
                if (context.Request.Url.AbsolutePath.EndsWith(".action"))
                {
                    var tmp = context.Request.Url.AbsolutePath;
                    tmp = tmp.Remove(tmp.Length - 7);

                    var json = new JsonObject();
                    json.Add("status", "success");
                    json["action"] = tmp;
                    try
                    {
                        json.Add("result", actions[tmp].Invoke(SplitQuery(context.Request.Url.Query)));
                    }
                    catch (Exception e)
                    {
                        json["result"] = null;
                        json["status"] = "error";
                        json["exception"] = WrapException(e);
                    }
                    res = json.ToStringEx();
                }
                else
                {
                    var path = Path.Combine(HtmlPath, context.Request.Url.AbsolutePath.Substring(1));
                    if (File.Exists(path))
                    {
                        resBytes = File.ReadAllBytes(path);
                    }
                    else
                    {
                        response.StatusCode = 404;
                        res = "File not found.";
                    }
                }
            }
            catch(Exception e)
            {
                response.StatusCode = 500;
                res = string.Format("Internal Server Error: {0}", e);
            }

            if (resBytes == null)
            {
                response.OutputStream.SetString(res, Encoding.UTF8);
            }
            else
            {
                response.OutputStream.Write(resBytes, 0, resBytes.Length);
            }
            response.OutputStream.Close();
        }
        JsonObject WrapException(Exception e)
        {
            var ret = new JsonObject();
            ret["type"] = e.GetType();
            ret["message"] = e.Message;
            ret["stacktrace"] = e.StackTrace;
            return ret;
        }

        Dictionary<string, string> SplitQuery(string q)
        {
            var ret = new Dictionary<string, string>();
            var split = q.Split(new[] { "?", "&" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in split)
            {
                var split2 = s.Split('=');
                ret[split2[0]] = Uri.UnescapeDataString(split2[1]);
            }
            return ret;
        }

        public IDownloadsDatabase DownloadsDatabase { get; set; }

        public void Load()
        {
            Port = 1336;
            HtmlPath = "./html";
        }

        public void Ready()
        {
            thread.Start();
        }

        public void Unload()
        {
            thread.Abort();
        }

        public string Name { get { return "webinterface"; } }

        public string FullName { get { return "Web Interface"; } }

        public IConfigurator Configurator
        {
            get { return config; }
        }

        public int Port { get; set; }

        public string HtmlPath { get; set; }

        public void ConfigLoaded()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(string.Format("http://*:{0}/", Port));
        }

        public ISystem System { get; set; }

        public IInvokerDatabase InvokerDatabase { get; set; }
    }
}
