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
        IInvokerInterface
    {
        private Timer refreshTimer;
        private Thread inputThread;
        private Mutex mutex;
        private List<Tuple<Regex, Action<Match>>> actions;

        public IDownloadsDatabase DownloadsDatabase { get; set; }

        public Addon()
        {
            mutex = new Mutex();

            actions = new List<Tuple<Regex, Action<Match>>>();
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^shutdown$"), Stop));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^d\.(?<downloader>\w+)\ (?<url>.+)\ (?<path>.+)$"), Download));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^downloads$"), m => PrintDownloadsInfo()));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^remove (?<id>-?\d+)$"), RemoveDownload));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^archive (?<id>-?\d+)$"), ArchiveDownload));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^addons$"), PrintAddonsInfo));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^follow (?<id>-?\d+)$"), FollowDownload));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^cls$"), m => Console.Clear()));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^files (?<id>-?\d+)$"), ShowDownloadFiles));
            actions.Add(new Tuple<Regex, Action<Match>>(
                new Regex(@"^update$"), UpdateSystem));
        }

        private void UpdateSystem(Match obj)
        {
            Console.WriteLine("Looking for update...");

            var updater = InvokerDatabase.GetInvoker("updater");
            var res = updater.Invoke<bool>("update");

            if(res)
            {
                Console.WriteLine("Update found and downloaded.");
            }
            else
            {
                Console.WriteLine("No update found.");
            }
        }

        private void ShowDownloadFiles(Match obj)
        {
            var id = int.Parse(obj.Groups["id"].Value);
            var down = DownloadsDatabase.GetDownload(id);
            foreach (var f in down.Files)
            {
                Console.WriteLine(f);
            }
        }

        private void FollowDownload(Match obj)
        {
            var id = int.Parse(obj.Groups["id"].Value);
            var d = DownloadsDatabase.GetDownload(id);
            var pmutex = new Mutex();

            var line = Console.CursorTop;
            Console.WriteLine("{0,-50} [----------]", d.Name);
            Console.WriteLine("ETA: {0,19} | Speed: {1,10}/s | Size: {2,10}",
                "N/A", ((long)d.DownloadSpeed).ToReadableByteSize(),
                d.Size.ToReadableByteSize());

            Action<IDownload> update = down =>
                {
                    pmutex.WaitOne();

                    try
                    {
                        var progress = (int)(down.Progress * 10);
                        var eta = CalcETA(down);

                        Console.CursorTop = line;
                        Console.CursorLeft = 52;
                        for (var i = 0; i < 10; i++)
                        {
                            if (i < progress)
                            {
                                Console.Write("#");
                            }
                            else if (i == progress)
                            {
                                Console.Write(">");
                            }
                            else
                            {
                                Console.Write("-");
                            }
                        }

                        Console.CursorTop = line + 1;
                        Console.CursorLeft = 5;
                        Console.Write("{0,19}", eta.HasValue ? eta.ToString() : "N/A");
                        Console.CursorLeft = 34;
                        Console.Write("{0,10}", ((long)down.DownloadSpeed).ToReadableByteSize());
                    }
                    catch(ArgumentOutOfRangeException e)
                    {
                        Console.WriteLine("Top: {0}; Left: {1}; Line: {2}",
                            Console.CursorTop, Console.CursorLeft, line);
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

            Console.ReadKey(true);

            d.DownloadProgressUpdated -= tmpEvent;

            pmutex.WaitOne();
            Console.CursorTop = line + 2;
            Console.CursorLeft = 0;
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

        private void PrintAddonsInfo(Match obj)
        {
            var fHead = "{0,-50} {1,-20}";
            var fItem = "{0,-50} {1,-20}";
            var fLine = "----------------------------------------------------------------";

            Console.WriteLine(fHead, "Full Name", "Name");
            Console.WriteLine(fLine);
            foreach (var a in AddonDatabse.GetAddons())
            {
                Console.WriteLine(fItem, a.FullName, a.Name);
            }
        }

        private void ArchiveDownload(Match obj)
        {
            var d = DownloadsDatabase.GetDownload(int.Parse(obj.Groups["id"].Value));
            DownloadsDatabase.UnregisterDownload(d, false);
        }

        private void RemoveDownload(Match obj)
        {
            var d = DownloadsDatabase.GetDownload(int.Parse(obj.Groups["id"].Value));
            DownloadsDatabase.UnregisterDownload(d, true);
        }

        private void Download(Match obj)
        {
            var down = DownloaderDatabase.GetDownloader(obj.Groups["downloader"].Value);
            var d = down.Download(obj.Groups["url"].Value, obj.Groups["path"].Value);
            Console.WriteLine("Added Download: {0}", d);
        }

        private void Stop(Match obj)
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
                    result = InputHandle(Console.ReadLine());
                }
                Console.WriteLine("Input deactivated!");
            }));

            inputThread.Start();
        }

        private bool InputHandle(string input)
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
                        t.Item2(m);
                        return true;
                    }
                }

                Console.WriteLine("Unknown command");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while excecuting command");
                Console.WriteLine(e);
            }
            return true;
        }

        void refreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            refreshTimer.Stop();

            PrintDownloadsInfo();

            refreshTimer.Start();
        }

        void DownloadsDatabase_DownloadRemoved(IDownload download)
        {
            //Console.WriteLine("DownloadRemoved({0})", download);
            PrintDownloadsInfo();
        }

        void download_DownloadStatusChanged(IDownload download, DownloadStatus oldState, DownloadStatus newState)
        {
            //Console.WriteLine("DownloadStatusChanged({0}, {1}, {2})", download, oldState, newState);
            PrintDownloadsInfo();
        }

        void DownloadsDatabase_DownloadAdded(IDownload download)
        {
            //Console.WriteLine("DownloadAdded({0})", download);
            PrintDownloadsInfo();

            download.DownloadStatusChanged += download_DownloadStatusChanged;
            download.DownloadProgressUpdated += download_DownloadProgressUpdated;
        }

        private void PrintDownloadsInfo()
        {
            mutex.WaitOne();

            var fHead = "{3,-4} {0,-50} {1,-11} {2,-8}";
            var f = "{3,4} {0,-50} {1,11} {2,7:0.##}%";

            //Console.Clear();
            Console.WriteLine(fHead, "Name", "State", "Progress", "ID");
            Console.WriteLine("----------------------------------------------------------------------------");

            foreach (var d in DownloadsDatabase.GetDownloads())
            {
                Console.WriteLine(f, Trunc(d.Name, 50), d.Status, d.Progress * 100, d.ID);
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
    }
}
