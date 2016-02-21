using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace IgnoreCertificates
{
    public class Addon : IAddon
    {
        public void Load()
        {
            ServicePointManager.ServerCertificateValidationCallback = ValidateCertificate;
        }

        private bool ValidateCertificate(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public void Ready()
        {
        }

        public void Unload()
        {
        }

        public string Name
        {
            get { return "ignorecerts"; }
        }

        public string FullName
        {
            get { return "Ignore Certificates"; }
        }
    }
}
