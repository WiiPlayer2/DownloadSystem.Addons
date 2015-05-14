using DownloadSystem.Shared;
using MonoTorrent.Client;
using MonoTorrent.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TorrentDownloader
{
    class TorrentDownload : IDownload
    {
        private TorrentDownloader downloader;
        private Thread prepareThread;
        private int piecesHashed = 0;
        private DownloadStatus status;

        public TorrentDownload(TorrentDownloader downloader, IDownloadContinueRegister continueRegister, string url, string path)
        {
            this.downloader = downloader;

            Status = DownloadStatus.Preparing;
            Path = path;

            prepareThread = new Thread(new ThreadStart(() =>
            {
                TorrentPath = string.Format("./torrents/{0}", System.IO.Path.GetRandomFileName());
                var torrent = Torrent.Load(new Uri(url), TorrentPath);
                InitDownload(torrent, continueRegister, path);
            }));
        }

        public void Prepare()
        {
            prepareThread.Start();
        }

        private void InitDownload(Torrent torrent, IDownloadContinueRegister continueRegister, string path)
        {
            Path = System.IO.Path.Combine(path, torrent.Name);
            if (!downloader.Engine.Contains(torrent.InfoHash))
            {

                Manager = new TorrentManager(torrent, path, downloader.Settings);
                Manager.PieceHashed += Manager_PieceHashed;
                Manager.TorrentStateChanged += Manager_TorrentStateChanged;

                Status = DownloadStatus.Downloading;

                continueRegister.RegisterContinueData(this);
                downloader.Engine.Register(Manager);
                Manager.Start();
            }
            else
            {
                Status = DownloadStatus.Error;
            }
        }

        public TorrentDownload(TorrentDownloader torrentDownloader, IDownloadContinueRegister continueRegister, Database.DataObject data)
        {
            this.downloader = torrentDownloader;

            var torrentFile = data["torrentFile"].AsString;
            var savePath = data["savePath"].AsString;

            prepareThread = new Thread(new ThreadStart(() =>
            {
                var torrent = Torrent.Load(torrentFile);
                InitDownload(torrent, continueRegister, savePath);
            }));
        }

        void Manager_TorrentStateChanged(object sender, TorrentStateChangedEventArgs e)
        {
            if (e.NewState == TorrentState.Seeding)
            {
                Status = DownloadStatus.Finished;
                Manager.Stop();
            }
            else if (e.NewState == TorrentState.Stopped)
            {
                downloader.Engine.Unregister(Manager);

                if (Status != DownloadStatus.Downloading)
                {
                    File.Delete(Manager.Torrent.TorrentPath);
                }
            }
        }

        void Manager_PieceHashed(object sender, PieceHashedEventArgs e)
        {
            if (e.HashPassed)
            {
                piecesHashed++;
                if (DownloadProgressUpdated != null)
                {
                    DownloadProgressUpdated(this, Progress);
                }
            }
        }

        public TorrentManager Manager { get; private set; }

        public DownloadStatus Status
        {
            get
            {
                return status;
            }
            private set
            {
                var oldState = status;
                status = value;

                if (DownloadStatusChanged != null)
                {
                    DownloadStatusChanged(this, oldState, value);
                }
            }
        }

        public double Progress
        {
            get
            {
                if (Manager != null)
                {
                    //return (double)piecesHashed / Manager.Torrent.Pieces.Count;
                    return DownloadedBytes / (double)Size;
                }
                else
                {
                    return 0d;
                }
            }
        }

        public string Path { get; private set; }

        public string TorrentPath { get; private set; }

        public string Name
        {
            get
            {
                if (Manager == null)
                {
                    return "--- FETCHING METADATA ---";
                }
                else
                {
                    return Manager.Torrent.Name;
                }
            }
        }

        public IDownloader Downloader { get { return downloader; } }

        public int ID { get; set; }

        public event DownloadStatusChangedEventHandler DownloadStatusChanged;

        public event DownloadProgressUpdatedEventHandler DownloadProgressUpdated;

        public override string ToString()
        {
            return string.Format("[{0}] <#{1}>", Name, ID);
        }

        public void CleanUp()
        {
            if (Status != DownloadStatus.Finished)
            {
                Manager.Stop();
            }
            foreach (var f in Files)
            {
                File.Delete(f);
            }
        }


        public IEnumerable<string> Files
        {
            get
            {
                var files = Manager.Torrent.Files;
                return files.Select(o => o.FullPath);
            }
        }


        public bool Continuable
        {
            get { return true; }
        }


        public Database.DataEntry SaveContinuable()
        {
            var dobj = new Database.DataObject();
            dobj["torrentFile"] = Manager.Torrent.TorrentPath;
            dobj["savePath"] = Manager.SavePath;
            return dobj;
        }


        public DateTime Started { get; set; }

        public DateTime Finished { get; set; }


        public int DownloadSpeed
        {
            get
            {
                if (Manager == null)
                {
                    return 0;
                }
                else
                {
                    return Manager.Monitor.DownloadSpeed;
                }
            }
        }


        public long Size
        {
            get
            {
                if (Manager == null)
                {
                    return 0;
                }
                else
                {
                    return Manager.Torrent.Size;
                }
            }
        }


        public long DownloadedBytes
        {
            get
            {
                if (Manager == null)
                {
                    return 0;
                }
                else if (Status == DownloadStatus.Finished)
                {
                    return Size;
                }
                else
                {
                    return piecesHashed * (long)Manager.Torrent.PieceLength;
                }
            }
        }
    }
}
