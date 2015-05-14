using Jayrock.Json;
using Jayrock.Json.Conversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebInterface
{
    static class Utils
    {
        public static string ToStringEx(this IJsonExportable json)
        {
            var writer = new JsonTextWriter();
            writer.PrettyPrint = true;
            var context = new ExportContext();
            json.Export(context, writer);
            return writer.ToString();
        }
    }
}
