using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleIO
{
    class StringConsole : MiniConsole
    {
        private StringWriter writer;

        public StringConsole()
        {
            writer = new StringWriter();
        }

        public string Output
        {
            get
            {
                return writer.ToString();
            }
        }

        public override void Write(string text)
        {
            writer.Write(text);
        }

        public override int CursorTop
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public override int CursorLeft
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        public override ConsoleKeyInfo ReadKey(bool intercept)
        {
            return default(ConsoleKeyInfo);
        }

        public override void Clear()
        {
            writer = new StringWriter();
        }
    }
}
