using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using Timer = System.Timers.Timer;

namespace FileIO
{
    public class Addon : IDownloadsInterface
    {
        private Timer timer;
        private Mutex mutex;

        public IDownloadsDatabase DownloadsDatabase { get; set; }

        public void Load()
        {
            mutex = new Mutex();

            timer = new Timer(5000);
            timer.AutoReset = true;
            timer.Elapsed += timer_Elapsed;

            DownloadsDatabase.DownloadAdded += DownloadsDatabase_DownloadAdded;
            DownloadsDatabase.DownloadRemoved += DownloadsDatabase_DownloadRemoved;

            timer.Start();
        }

        void DownloadsDatabase_DownloadRemoved(IDownload download)
        {
            download.DownloadStatusChanged -= download_DownloadStatusChanged;
            WriteInfo();
        }

        void download_DownloadStatusChanged(IDownload download, DownloadStatus oldState, DownloadStatus newState)
        {
            WriteInfo();
        }

        void DownloadsDatabase_DownloadAdded(IDownload download)
        {
            download.DownloadStatusChanged += download_DownloadStatusChanged;
            WriteInfo();
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            WriteInfo();
        }

        private void WriteInfo()
        {
            mutex.WaitOne();
            try
            {
                var fHead = "{8,-4} {0,-60} {1,-11} {2,-12} {3,-19} {4,-19} {5,-8} {6,-10} {9,-19} {7,-30}";
                var fItem = "{8,4} {0,-60} {1,11} {2,10}/s {3,19} {4,19} {5,7:0.##}% {6,10} {9,19} {7,30}";
                var fLine = "-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------";

                var writer = new StreamWriter("fileio.output");
                writer.WriteLine(fHead,
                    "Name", "Status", "Speed", "Started", "Finished", "Progress", "Size", "Downloader", "ID", "ETA");
                writer.WriteLine(fLine);

                var allSize = 0L;
                var allDownload = 0L;
                var allSpeed = 0;

                foreach (var d in DownloadsDatabase.GetDownloads().ToArray())
                {
                    var eta = CalcETA(d);
                    writer.WriteLine(fItem,
                        d.Name.Truncate(60), d.Status, ((long)d.DownloadSpeed).ToReadableByteSize(),
                        d.Started,
                        d.Status == DownloadStatus.Finished ? d.Finished.ToString() : "N/A",
                        d.Progress * 100d, d.Size.ToReadableByteSize(),
                        d.Downloader != null ? d.Downloader.FullName.Truncate(30) : "N/A",
                        d.ID, eta.HasValue ? eta.Value.ToString() : "N/A");

                    allSize += d.Size;
                    allDownload += d.DownloadedBytes;
                    allSpeed += d.DownloadSpeed;
                }

                var allEta = CalcETA(allSize, allDownload, allSpeed);
                var allProgress = allSize > 0 ? (double)allDownload / allSize : 0;
                writer.WriteLine(fLine);
                writer.WriteLine(fItem,
                    "Total", "",
                    ((long)allSpeed).ToReadableByteSize(), "", "",
                    allProgress * 100,
                    allSize.ToReadableByteSize(), "", "",
                    allEta.HasValue ? allEta.ToString() : "N/A");
                writer.Close();
            }
            catch (Exception ef)
            {

            }
            mutex.ReleaseMutex();
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

        public void Unload()
        {
            timer.Stop();
        }

        public string Name { get { return "fileio"; } }

        public string FullName { get { return "File IO"; } }


        public void Ready()
        {
        }
    }
}
