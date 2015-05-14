using DownloadSystem.Shared;
using NyaaTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NyaaAnimeAdder
{
    public class NyaaAnimeAdder : IDownloaderInterface, IConfigurable
    {
        public struct Filter
        {
            public int UserID;
            public string Quality;
            public string Title;
        }

        private NyaaAnimeConfigurator config;
        private Dictionary<int, NyaaPageListener> listeners;
        private Dictionary<int, Regex> regexes;
        private TimeSpan refreshTime;

        public NyaaAnimeAdder()
        {
            config = new NyaaAnimeConfigurator(this);
            listeners = new Dictionary<int, NyaaPageListener>();
            regexes = new Dictionary<int, Regex>();
            refreshTime = new TimeSpan(0, 5, 0);

            UserRegexes = new Dictionary<int, string>();
            LastIDs = new Dictionary<int, int>();
            Filters = new Filter[0];
        }

        public IConfigurator Configurator { get { return config; } }

        public void ConfigLoaded()
        {
            config.LoadExternalConfig();

            foreach(var id in UserRegexes.Keys)
            {
                if(LastIDs.ContainsKey(id))
                {
                    listeners[id] = new NyaaPageListener(id, LastIDs[id], refreshTime);
                }
                else
                {
                    listeners[id] = new NyaaPageListener(id, refreshTime);
                }
                listeners[id].NewTorrentReceived += NyaaAnimeAdder_NewTorrentReceived;
                regexes[id] = new Regex(UserRegexes[id]);
            }
        }

        void NyaaAnimeAdder_NewTorrentReceived(NyaaTorrent pTorrent)
        {
            var m = regexes[pTorrent.SubmitterID].Match(pTorrent.Name);

            if (m.Success)
            {
                try
                {
                    var quality = m.Groups["quality"].Value;
                    var title = m.Groups["title"].Value;

                    if (Filters.Any(o => o.UserID == pTorrent.SubmitterID
                        && o.Quality == quality
                        && o.Title == title))
                    {
                        var down = DownloaderDatabase.GetDownloader("torrent");
                        down.Download(pTorrent.TorrentUrl, "./downloads");
                    }
                }
                catch(Exception)
                {

                }
            }

            LastIDs[pTorrent.SubmitterID] = pTorrent.TorrentID;
            config.SaveConfig("last_ids");
        }

        public void Load()
        {
        }

        public void Ready()
        {
            foreach (var listener in listeners.Values)
            {
                listener.Start();
            }
        }

        public void Unload()
        {
            foreach (var listener in listeners.Values)
            {
                listener.Stop();
            }
        }

        public Dictionary<int, string> UserRegexes { get; set; }

        public Dictionary<int, int> LastIDs { get; set; }

        public Filter[] Filters { get; set; }

        public string Name { get { return "nyaaadder"; } }

        public string FullName { get { return "Nyaa Anime Adder"; } }

        public IDownloaderDatabase DownloaderDatabase { get; set; }
    }
}
