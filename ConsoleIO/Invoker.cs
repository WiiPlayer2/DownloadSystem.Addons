using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleIO
{
    class Invoker : EasyInvoker
    {
        public Invoker(Addon addon)
            : base(addon)
        {
            RegisterMethod("command", addon, "InvokeCommand");
        }
    }
}
