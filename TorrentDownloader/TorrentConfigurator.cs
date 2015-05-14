using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TorrentDownloader
{
    class TorrentConfigurator : EasyConfigurator
    {
        public TorrentConfigurator(TorrentDownloader downloader)
            : base(downloader)
        {

        }
    }
}
