using DownloadSystem.Shared;
using PushbulletSharp;
using PushbulletSharp.Models.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushbulletNotifier
{
    public class Addon : IDownloadsInterface, IConfigurable
    {
        private Configurator config;
        private PushbulletClient client;
        private string userIden;

        public Addon()
        {
            config = new Configurator(this);
        }

        public IDownloadsDatabase DownloadsDatabase { get; set; }

        public void Load()
        {
        }

        public void Ready()
        {
        }

        public void Unload()
        {
        }

        public string Name
        {
            get { return "pushbulletnotifier"; }
        }

        public string FullName
        {
            get { return "Pushbullet Notifier"; }
        }

        public IConfigurator Configurator
        {
            get { return config; }
        }

        public string AccessToken { get; set; }

        public string Device { get; set; }

        public void ConfigLoaded()
        {
            try
            {
                client = new PushbulletClient(AccessToken);
                //userIden = client.CurrentUsersInformation().Iden;
                userIden = client.CurrentUsersDevices().Devices.First(o => o.Nickname == Device).Iden;

                DownloadsDatabase.DownloadAdded += DownloadsDatabase_DownloadAdded;
                DownloadsDatabase.DownloadRemoved += DownloadsDatabase_DownloadRemoved;
            }
            catch(Exception e)
            {

            }
        }

        void DownloadsDatabase_DownloadRemoved(IDownload download)
        {
            download.DownloadStatusChanged -= download_DownloadStatusChanged;
        }

        void DownloadsDatabase_DownloadAdded(IDownload download)
        {
            try
            {
                download.DownloadStatusChanged += download_DownloadStatusChanged;

                var request = new PushNoteRequest()
                {
                    Title = "[DLSystem] Download added",
                    Body = string.Format("{0} has been added", download.Name),
                    DeviceIden = userIden,
                };
                client.PushNote(request);
            }
            catch { }
        }

        void download_DownloadStatusChanged(IDownload download, DownloadStatus oldState, DownloadStatus newState)
        {
            try
            {
                var request = new PushNoteRequest()
                {
                    Title = "[DLSystem] Download state changed",
                    Body = string.Format("{0} has changed from {1} to {2}",
                        download.Name, oldState, newState),
                    DeviceIden = userIden,
                };
                client.PushNote(request);
            }
            catch { }
        }
    }
}
