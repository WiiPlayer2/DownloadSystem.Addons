using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebInterface
{
    class Configurator : EasyConfigurator
    {
        public Configurator(WebInterface web)
            : base(web)
        {
            RegisterVar<int>("port", web, "Port");
            RegisterVar<string>("htmlpath", web, "HtmlPath");
        }
    }
}
