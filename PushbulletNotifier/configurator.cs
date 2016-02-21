using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushbulletNotifier
{
    class Configurator : EasyConfigurator
    {
        public Configurator(Addon addon)
            : base(addon)
        {
            RegisterVar("access_token", addon, "AccessToken");
            RegisterVar("device", addon, "Device");
        }

        public Addon Addon { get; private set; }
    }
}
