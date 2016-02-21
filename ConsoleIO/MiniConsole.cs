using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleIO
{
    abstract class MiniConsole
    {
        public abstract void Write(string text);

        public void Write(string format, params object[] args)
        {
            Write(string.Format(format, args));
        }

        public void Write(object o)
        {
            Write("{0}", o);
        }

        public void WriteLine(string text)
        {
            Write("{0}{1}", text, "\n");
        }

        public void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(format, args));
        }

        public void WriteLine(object o)
        {
            WriteLine("{0}", o);
        }

        public abstract int CursorTop { get; set; }

        public abstract int CursorLeft { get; set; }

        public abstract ConsoleKeyInfo ReadKey(bool intercept);

        public abstract void Clear();
    }
}
