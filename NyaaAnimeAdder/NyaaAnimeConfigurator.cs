using DownloadSystem.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NyaaAnimeAdder
{
    class NyaaAnimeConfigurator : EasyConfigurator
    {
        private Regex regexRegex;
        private Regex filterRegex;

        static NyaaAnimeConfigurator()
        {
            RegisterTupleParser<int, string>();
            RegisterTupleParser<int, int>();
            RegisterTupleParser<Tuple<int, string>, string>();
        }

        public NyaaAnimeConfigurator(NyaaAnimeAdder adder)
            : base(adder)
        {
            Adder = adder;

            regexRegex = new Regex(@"^(?<userid>\d+)\ <-\ (?<regex>.*)$");
            filterRegex = new Regex(@"^(?<userid>\d+)\ ~\ (?<quality>[a-zA-Z0-9]*)\ >>\ (?<title>.*)$");

            RegisterVar<Tuple<int, string>[]>("user_regexes",
                o => adder.UserRegexes
                    .Select(o2 => new Tuple<int, string>(o2.Key, o2.Value))
                    .ToArray(),
                (key, o) => adder.UserRegexes = o
                    .ToDictionary(o2 => o2.Item1, o2 => o2.Item2));
            RegisterVar<Tuple<int, int>[]>("last_ids",
                o => adder.LastIDs
                    .Select(o2 => new Tuple<int, int>(o2.Key, o2.Value))
                    .ToArray(),
                (key, o) => adder.LastIDs = o
                    .ToDictionary(o2 => o2.Item1, o2 => o2.Item2));
            RegisterVar<Tuple<Tuple<int, string>, string>[]>("filters",
                o => adder.Filters
                    .Select(o2 =>
                        new Tuple<Tuple<int, string>, string>(
                            new Tuple<int, string>(o2.UserID, o2.Quality), o2.Title))
                    .ToArray(),
                (key, o) => adder.Filters = o
                    .Select(o2 => new NyaaAnimeAdder.Filter()
                    {
                        UserID = o2.Item1.Item1,
                        Quality = o2.Item1.Item2,
                        Title = o2.Item2,
                    })
                    .ToArray());
        }

        public NyaaAnimeAdder Adder { get; private set; }

        public void LoadExternalConfig()
        {
            if (File.Exists("./nyaa.adders.conf"))
            {
                try
                {
                    var regexes = new Dictionary<int, string>();
                    var filters = new List<NyaaAnimeAdder.Filter>();

                    foreach (var l in File.ReadAllLines("./nyaa.adders.conf"))
                    {
                        Match m = regexRegex.Match(l);
                        if(m.Success)
                        {
                            var id = int.Parse(m.Groups["userid"].Value);
                            var regex = m.Groups["regex"].Value;
                            regexes[id] = regex;
                        }
                        else if((m = filterRegex.Match(l)).Success)
                        {
                            var id = int.Parse(m.Groups["userid"].Value);
                            var quality = m.Groups["quality"].Value;
                            var title = m.Groups["title"].Value;
                            filters.Add(new NyaaAnimeAdder.Filter()
                            {
                                UserID = id,
                                Quality = quality,
                                Title = title,
                            });
                        }
                    }

                    Adder.Filters = filters.ToArray();
                    Adder.UserRegexes = regexes;

                    SaveConfig("filters");
                    SaveConfig("user_regexes");
                }
                catch (Exception)
                {

                }
            }
        }
    }
}
