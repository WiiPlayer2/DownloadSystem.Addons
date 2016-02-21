using DownloadSystem.Shared;
using MonoTorrent.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorrentDownloader
{
    class TorrentInvoker : EasyInvoker
    {
        public TorrentInvoker(TorrentDownloader torrentDownloader)
            : base(torrentDownloader)
        {
            Downloader = torrentDownloader;

            RegisterMethod("start", this, "StartTorrent");
            RegisterMethod("stop", this, "StopTorrent");
            RegisterMethod("pause", this, "PauseTorrent");
            RegisterMethod("state", this, "GetState");
        }

        public TorrentDownloader Downloader { get; private set; }

        public void StartTorrent(int id)
        {
            var torrent = GetTorrent(id);
            torrent.Manager.Start();
        }

        public void StopTorrent(int id)
        {
            var torrent = GetTorrent(id);
            torrent.Manager.Stop();
        }

        public void PauseTorrent(int id)
        {
            var torrent = GetTorrent(id);
            torrent.Manager.Pause();
        }

        public TorrentState GetState(int id)
        {
            var torrent = GetTorrent(id);
            return torrent.Manager.State;
        }

        private TorrentDownload GetTorrent(int id)
        {
            return Downloader.GetDownloads()
                .OfType<TorrentDownload>()
                .Single(o => o.ID == id);
        }
    }
}
