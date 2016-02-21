using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoTorrent.Client;
using System.Timers;
using System.Net;
using MonoTorrent.Dht.Listeners;
using MonoTorrent.Dht;
using System.IO;

namespace TorrentDownloader
{
    public class TorrentDownloader : IDownloader, IConfigurable, IInvokable
    {
        private Timer dhtTimer;
        private DhtListener listener;
        private DhtEngine dht;
        private List<IDownload> downloads;
        private TorrentConfigurator config;
        private TorrentInvoker invoker;

        public TorrentDownloader()
        {
            downloads = new List<IDownload>();
            EngineSettings = new EngineSettings();
            Engine = new ClientEngine(EngineSettings);
            Settings = new TorrentSettings()
            {
                EnablePeerExchange = true,
                UseDht = true,
            };
            config = new TorrentConfigurator(this);
            invoker = new TorrentInvoker(this);
        }

        public IDownload Download(string url, string path)
        {
            var ret = new TorrentDownload(this, DownloadRegister, url, path);
            downloads.Add(ret);
            DownloadRegister.RegisterDownload(ret, true);
            ret.Prepare();
            return ret;
        }

        public void Load()
        {
            if (!Directory.Exists("./torrents"))
            {
                Directory.CreateDirectory("./torrents");
            }

            //StartDht(1338);
        }

        public void Unload()
        {
            Engine.StopAll();
            //StopDht();
        }

        public TorrentSettings Settings { get; private set; }

        public EngineSettings EngineSettings { get; private set; }

        public ClientEngine Engine { get; private set; }

        public string Name { get { return "torrent"; } }

        public string FullName { get { return "Torrent Downloader"; } }

        public IConfigurator Configurator
        {
            get { return config; }
        }

        public IDownloadRegister DownloadRegister { get; set; }

        #region DHT
        public void StartDht(int port)
        {
            dhtTimer = new Timer(60000);
            dhtTimer.Elapsed += dhtTimer_Elapsed;

            // Send/receive DHT messages on the specified port
            IPEndPoint listenAddress = new IPEndPoint(IPAddress.Any, port);

            // Create a listener which will process incoming/outgoing dht messages
            listener = new DhtListener(listenAddress);

            // Create the dht engine
            dht = new DhtEngine(listener);

            // Connect the Dht engine to the MonoTorrent engine
            Engine.RegisterDht(dht);

            // Start listening for dht messages and activate the DHT engine
            listener.Start();

            // If there are existing DHT nodes stored on disk, load them
            // into the DHT engine so we can try and avoid a (very slow)
            // full bootstrap
            byte[] nodes = null;
            if (File.Exists(Path.Combine("dht.nodes")))
            {
                nodes = File.ReadAllBytes(Path.Combine("dht.nodes"));
            }
            dht.Start(nodes);
            dhtTimer.Enabled = true;
        }

        void dhtTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            File.WriteAllBytes(Path.Combine("dht.nodes"), dht.SaveNodes());
        }

        public void StopDht()
        {
            // Stop the listener and dht engine. This does not
            // clear internal data so the DHT can be started again
            // later without needing a full bootstrap.
            listener.Stop();
            dht.Stop();

            // Save all known dht nodes to disk so they can be restored
            // later. This is *highly* recommended as it makes startup
            // much much faster.
            File.WriteAllBytes(Path.Combine("dht.nodes"), dht.SaveNodes());
        }
        #endregion

        public IEnumerable<IDownload> GetDownloads()
        {
            return downloads;
        }


        public void ConfigLoaded()
        {
            //throw new NotImplementedException();
        }


        public IDownload Continue(Database.DataEntry data)
        {
            var d = new TorrentDownload(this, DownloadRegister, data.AsObject);
            downloads.Add(d);
            DownloadRegister.RegisterDownload(d, false);
            d.Prepare();
            return d;
        }

        public void Ready()
        {
        }

        public IInvoker Invoker
        {
            get { return invoker; }
        }
    }
}
