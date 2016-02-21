using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ConsoleIO
{
    public class Addon : IDownloadsInterface, ISystemInterface, IDownloaderInterface, IAddonInterface,
        IInvokerInterface, IInvokable
    {
        private Timer refreshTimer;
        private Thread inputThread;
        private Mutex mutex;
        private List<Tuple<Regex, Action<Match, MiniConsole>>> actions;

        private Invoker invoker;
        private DefaultConsole defaultConsole;

        public IDownloadsDatabase DownloadsDatabase { get; set; }

        public Addon()
        {
            mutex = new Mutex();

            invoker = new Invoker(this);
            defaultConsole = new DefaultConsole();

            actions = new List<Tuple<Regex, Action<Match, MiniConsole>>>();
            Register(@"^shutdown$", Stop);
            Register(@"^d\.(?<downloader>\w+)\ (?<url>.+)\ (?<path>.+)$", Download);
            Register(@"^downloads$", (m, c) => PrintDownloadsInfo(c));
            Register(@"^remove (?<id>-?\d+)$", RemoveDownload);
            Register(@"^archive (?<id>-?\d+)$", ArchiveDownload);
            Register(@"^addons$", PrintAddonsInfo);
            Register(@"^follow (?<id>-?\d+)$", FollowDownload);
            Register(@"^cls$", (m, c) => c.Clear());
            Register(@"^files (?<id>-?\d+)$", ShowDownloadFiles);
            Register(@"^update$", UpdateSystem);
            Register(@"^(?<addon>(\?|\w+))\.(\?|(?<method>\w+)(\ (?<argument>[^\ \n]+))*)$",
                InvokeAddonMethod);
        }

        private void InvokeAddonMethod(Match obj, MiniConsole console)
        {
            var addonName = obj.Groups["addon"].Value;
            if (addonName == "?")
            {
                var addons = InvokerDatabase.GetInvokers();

                var fHead = "{0,-50} {1,-20}";
                var fItem = "{0,-50} {1,-20}";
                var fLine = "----------------------------------------------------------------";

                console.WriteLine(fHead, "Full Name", "Name");
                console.WriteLine(fLine);
                foreach (var a in addons)
                {
                    console.WriteLine(fItem, a.Invokable.FullName, a.Invokable.Name);
                }
            }
            else
            {
                var addon = InvokerDatabase.GetInvoker(addonName);
                var method = obj.Groups["method"];
                if (method.Success)
                {
                    var args = obj.Groups["argument"];
                    var param = GetParamValues(addon.GetParameterTypes(method.Value),
                        args.Captures.Cast<Capture>().Select(o => o.Value).ToArray());
                    var ret = addon.Invoke(method.Value, param);
                    console.WriteLine(ret);
                }
                else
                {
                    foreach (var k in addon.Methods)
                    {
                        console.WriteLine(GetInvokeMethodSignature(addon, k));
                    }
                }
            }
        }

        private object[] GetParamValues(Type[] types, string[] values)
        {
            return types
                .Zip(values, (t, v) => GetParamValue(t, v))
                .ToArray();
        }

        private object GetParamValue(Type type, string value)
        {
            if (type == typeof(bool))
            {
                return bool.Parse(value);
            }
            else if (type == typeof(sbyte))
            {
                return sbyte.Parse(value);
            }
            else if (type == typeof(short))
            {
                return ushort.Parse(value);
            }
            else if (type == typeof(int))
            {
                return int.Parse(value);
            }
            else if (type == typeof(long))
            {
                return long.Parse(value);
            }
            else if (type == typeof(byte))
            {
                return byte.Parse(value);
            }
            else if (type == typeof(ushort))
            {
                return ushort.Parse(value);
            }
            else if (type == typeof(uint))
            {
                return uint.Parse(value);
            }
            else if (type == typeof(ulong))
            {
                return ulong.Parse(value);
            }
            else if (type == typeof(float))
            {
                return float.Parse(value);
            }
            else if (type == typeof(double))
            {
                return double.Parse(value);
            }
            else if (type == typeof(string))
            {
                return value;
            }
            throw new NotSupportedException(string.Format("{0} is not supported.", type.FullName));
        }

        private string GetInvokeMethodSignature(IInvoker invoker, string name)
        {
            var retType = invoker.GetReturnType(name);
            var param = invoker.GetParameterTypes(name);
            var ret = string.Format("{0}{1}({2})",
                retType == typeof(void) ? "" : string.Format("{0} ", GetTypeName(retType)), name,
                string.Join(", ", param.Select(o => GetTypeName(o))));
            return ret;
        }

        private string GetTypeName(Type t)
        {
            var name = t.FullName;
            switch (name)
            {
                case "System.Boolean":
                    return "bool";
                case "System.SByte":
                    return "sbyte";
                case "System.Int16":
                    return "short";
                case "System.Int32":
                    return "int";
                case "System.Int64":
                    return "long";
                case "System.Byte":
                    return "byte";
                case "System.UInt16":
                    return "ushort";
                case "System.UInt32":
                    return "uint";
                case "System.UInt64":
                    return "ulong";
                case "System.Single":
                    return "float";
                case "System.Double":
                    return "double";
                case "System.String":
                    return "string";
                default:
                    return name;
            }
        }

        private void Register(string regex, Action<Match, MiniConsole> action)
        {
            actions.Add(new Tuple<Regex, Action<Match, MiniConsole>>(new Regex(regex), action));
        }

        private void UpdateSystem(Match obj, MiniConsole console)
        {
            console.WriteLine("Looking for update...");

            var updater = InvokerDatabase.GetInvoker("updater");
            var res = updater.Invoke<bool>("update");

            if (res)
            {
                console.WriteLine("Update found and downloaded.");
            }
            else
            {
                console.WriteLine("No update found.");
            }
        }

        private void ShowDownloadFiles(Match obj, MiniConsole console)
        {
            var id = int.Parse(obj.Groups["id"].Value);
            var down = DownloadsDatabase.GetDownload(id);
            foreach (var f in down.Files)
            {
                console.WriteLine(f);
            }
        }

        private void FollowDownload(Match obj, MiniConsole console)
        {
            var id = int.Parse(obj.Groups["id"].Value);
            var d = DownloadsDatabase.GetDownload(id);
            var pmutex = new Mutex();

            var line = console.CursorTop;
            console.WriteLine("{0,-50} [----------]", d.Name);
            console.WriteLine("ETA: {0,19} | Speed: {1,10}/s | Size: {2,10}",
                "N/A", ((long)d.DownloadSpeed).ToReadableByteSize(),
                d.Size.ToReadableByteSize());

            Action<IDownload> update = down =>
                {
                    pmutex.WaitOne();

                    try
                    {
                        var progress = (int)(down.Progress * 10);
                        var eta = CalcETA(down);

                        console.CursorTop = line;
                        console.CursorLeft = 52;
                        for (var i = 0; i < 10; i++)
                        {
                            if (i < progress)
                            {
                                console.Write("#");
                            }
                            else if (i == progress)
                            {
                                console.Write(">");
                            }
                            else
                            {
                                console.Write("-");
                            }
                        }

                        console.CursorTop = line + 1;
                        console.CursorLeft = 5;
                        console.Write("{0,19}", eta.HasValue ? eta.ToString() : "N/A");
                        console.CursorLeft = 34;
                        console.Write("{0,10}", ((long)down.DownloadSpeed).ToReadableByteSize());
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        console.WriteLine("Top: {0}; Left: {1}; Line: {2}",
                            console.CursorTop, console.CursorLeft, line);
                        throw e;
                    }

                    pmutex.ReleaseMutex();
                };
            DownloadProgressUpdatedEventHandler tmpEvent = (sender, progress) =>
            {
                update(sender);
            };
            update(d);
            d.DownloadProgressUpdated += tmpEvent;

            console.ReadKey(true);

            d.DownloadProgressUpdated -= tmpEvent;

            pmutex.WaitOne();
            console.CursorTop = line + 2;
            console.CursorLeft = 0;
            pmutex.ReleaseMutex();
        }

        private TimeSpan? CalcETA(IDownload d)
        {
            return CalcETA(d.Size, d.DownloadedBytes, d.DownloadSpeed);
        }

        private TimeSpan? CalcETA(long size, long downloadedBytes, int downloadSpeed)
        {
            if (size == 0 || downloadedBytes > size || downloadSpeed == 0)
            {
                return null;
            }
            var diff = size - downloadedBytes;
            var time = diff / downloadSpeed;
            var eta = new TimeSpan(0, 0, (int)time);
            return eta;
        }

        private void PrintAddonsInfo(Match obj, MiniConsole console)
        {
            var fHead = "{0,-50} {1,-20}";
            var fItem = "{0,-50} {1,-20}";
            var fLine = "----------------------------------------------------------------";

            console.WriteLine(fHead, "Full Name", "Name");
            console.WriteLine(fLine);
            foreach (var a in AddonDatabse.GetAddons())
            {
                console.WriteLine(fItem, a.FullName, a.Name);
            }
        }

        private void ArchiveDownload(Match obj, MiniConsole console)
        {
            var d = DownloadsDatabase.GetDownload(int.Parse(obj.Groups["id"].Value));
            DownloadsDatabase.UnregisterDownload(d, false);
        }

        private void RemoveDownload(Match obj, MiniConsole console)
        {
            var d = DownloadsDatabase.GetDownload(int.Parse(obj.Groups["id"].Value));
            DownloadsDatabase.UnregisterDownload(d, true);
        }

        private void Download(Match obj, MiniConsole console)
        {
            var down = DownloaderDatabase.GetDownloader(obj.Groups["downloader"].Value);
            var d = down.Download(obj.Groups["url"].Value, obj.Groups["path"].Value);
            console.WriteLine("Added Download: {0}", d);
        }

        private void Stop(Match obj, MiniConsole console)
        {
            System.Shutdown();
        }

        public void Load()
        {
            Console.WriteLine("ConsoleIO - Load");

            //DownloadsDatabase.DownloadAdded += DownloadsDatabase_DownloadAdded;
            //DownloadsDatabase.DownloadRemoved += DownloadsDatabase_DownloadRemoved;

            refreshTimer = new Timer(5000);
            refreshTimer.Elapsed += refreshTimer_Elapsed;
            refreshTimer.AutoReset = true;
            //refreshTimer.Start();

            inputThread = new Thread(new ThreadStart(() =>
            {
                var result = true;
                while (result)
                {
                    Console.Write("> ");
                    result = InputHandle(Console.ReadLine(), defaultConsole);
                }
                Console.WriteLine("Input deactivated!");
            }));

            inputThread.Start();
        }

        public string InvokeCommand(string command)
        {
            var console = new StringConsole();
            InputHandle(command, console);
            return console.Output;
        }

        private bool InputHandle(string input, MiniConsole console)
        {
            if (input == null)
            {
                return false;
            }

            try
            {
                foreach (var t in actions)
                {
                    var m = t.Item1.Match(input);
                    if (m.Success)
                    {
                        t.Item2(m, console);
                        return true;
                    }
                }

                console.WriteLine("Unknown command");
            }
            catch (Exception e)
            {
                console.WriteLine("Error while excecuting command");
                console.WriteLine(e);
            }
            return true;
        }

        void refreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            refreshTimer.Stop();

            PrintDownloadsInfo(defaultConsole);

            refreshTimer.Start();
        }

        void DownloadsDatabase_DownloadRemoved(IDownload download)
        {
            //Console.WriteLine("DownloadRemoved({0})", download);
            PrintDownloadsInfo(defaultConsole);
        }

        void download_DownloadStatusChanged(IDownload download, DownloadStatus oldState, DownloadStatus newState)
        {
            //Console.WriteLine("DownloadStatusChanged({0}, {1}, {2})", download, oldState, newState);
            PrintDownloadsInfo(defaultConsole);
        }

        void DownloadsDatabase_DownloadAdded(IDownload download)
        {
            //Console.WriteLine("DownloadAdded({0})", download);
            PrintDownloadsInfo(defaultConsole);

            download.DownloadStatusChanged += download_DownloadStatusChanged;
            download.DownloadProgressUpdated += download_DownloadProgressUpdated;
        }

        private void PrintDownloadsInfo(MiniConsole console)
        {
            mutex.WaitOne();

            var fHead = "{3,-4} {0,-50} {1,-11} {2,-8}";
            var f = "{3,4} {0,-50} {1,11} {2,7:0.##}%";

            //Console.Clear();
            console.WriteLine(fHead, "Name", "State", "Progress", "ID");
            console.WriteLine("----------------------------------------------------------------------------");

            foreach (var d in DownloadsDatabase.GetDownloads())
            {
                console.WriteLine(f, Trunc(d.Name, 50), d.Status, d.Progress * 100, d.ID);
            }

            mutex.ReleaseMutex();
        }

        private string Trunc(string str, int maxLength)
        {
            return str.Substring(0, Math.Min(maxLength, str.Length));
        }

        void download_DownloadProgressUpdated(IDownload download, double progress)
        {
            //Console.WriteLine("DownloadProgressUpdated({0}, {1:0.00##})", download, progress);
        }

        public void Unload()
        {
            Console.WriteLine("ConsoleIO - Unload");
        }

        public string Name { get { return "consoleio"; } }

        public string FullName { get { return "Console IO"; } }

        public ISystem System { get; set; }

        public IDownloaderDatabase DownloaderDatabase { get; set; }


        public void Ready()
        {
        }

        public IAddonDatabase AddonDatabse { get; set; }

        public IInvokerDatabase InvokerDatabase { get; set; }

        public IInvoker Invoker
        {
            get { return invoker; }
        }
    }
}
