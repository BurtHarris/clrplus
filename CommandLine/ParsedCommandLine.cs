//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.CommandLine {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Core.Collections;
    using Core.Extensions;
    using Platform;

    public class ParsedCommandLine {
        /// <summary>
        ///     Collection to store the cached set of parsed command lines
        /// </summary>
        private static readonly XDictionary<CacheEnumerable<string>, ParsedCommandLine> _cache =
            new XDictionary<CacheEnumerable<string>, ParsedCommandLine>(new Core.Extensions.EqualityComparer<CacheEnumerable<string>>((a, b) => a.SequenceEqual(b), strings => {
                unchecked {
                    return strings.Aggregate(0, (current, next) => (current*419) ^ (next ?? new object()).GetHashCode());
                }
            }));

        /// <summary>
        ///     Parses the command line and returns a ParsedCommandLine
        ///     Multiple calls to this method will return the identical object (it's cached and keyed against the collection of items in the command line)
        /// </summary>
        /// <param name="args">the items in the command line to parse</param>
        /// <returns></returns>
        public static ParsedCommandLine Parse(IEnumerable<string> args) {
            return args.ToCacheEnumerable().With(lazy => _cache.GetOrAdd(lazy, () => new ParsedCommandLine(lazy)));
        }

        /// <summary>
        ///     the parsed switches from the arguments
        /// </summary>
        private readonly IDictionary<string, List<string>> _switches;

        /// <summary>
        ///     the parameters that are left after parsing the switches
        /// </summary>
        private readonly IEnumerable<string> _parameters;

        public static IEnumerable<string> SplitArgs(string unsplitArgumentLine) {
            return CommandLine.SplitArgs(unsplitArgumentLine);
        }

        protected ParsedCommandLine(CacheEnumerable<string> args) {
            var assemblypath = Assembly.GetEntryAssembly().Location;

            _switches = new XDictionary<string, List<string>>();

            var v = Environment.GetEnvironmentVariable("_" + Path.GetFileNameWithoutExtension(assemblypath) + "_");
            if (!string.IsNullOrEmpty(v)) {
                var extraSwitches = SplitArgs(v).Where(each => each.StartsWith("--"));
                if (!args.IsNullOrEmpty()) {
                    args = args.Concat(extraSwitches);
                }
            }

            // load a <exe>.properties file in the same location as the executing assembly.

            var propertiespath = "{0}\\{1}.properties".format(Path.GetDirectoryName(assemblypath), Path.GetFileNameWithoutExtension(assemblypath));
            if (File.Exists(propertiespath)) {
                LoadConfiguration(propertiespath);
            }

            var argEnumerator = args.GetEnumerator();
            //while(firstarg < args.Length && args[firstarg].StartsWith("--")) {
            while (argEnumerator.MoveNext() && argEnumerator.Current.StartsWith("--")) {
                var arg = argEnumerator.Current.Substring(2).ToLower();
                var param = "";
                int pos;

                if ((pos = arg.IndexOf("=")) > -1) {
                    param = argEnumerator.Current.Substring(pos + 3);
                    arg = arg.Substring(0, pos);
                    /*
                    if(string.IsNullOrEmpty(param) || string.IsNullOrEmpty(arg)) {
                        "Invalid Option :{0}".Print(argEnumerator.Current.Substring(2).ToLower());
                        switches.Clear();
                        switches.Add("help", new List<string>());
                        return switches;
                    } */
                }
                if (arg.Equals("load-config")) {
                    // loads the config file, and then continues parsing this line.
                    LoadConfiguration(param);
                    // firstarg++;
                    continue;
                }
#if TODO 
    // make an extensibility model for intercepting arguments 
    // so that console project can do this.

                if (arg.Equals("list-bugtracker") || arg.Equals("list-bugtrackers")) {
                    // the user is asking for the bugtracker URLs for this application.
                    ListBugTrackers();
                    continue;
                }

                if (arg.Equals("open-bugtracker") || arg.Equals("open-bugtrackers")) {
                    // the user is asking for the bugtracker URLs for this application.
                    OpenBugTracker();
                    continue;
                }
#endif
                if (!_switches.ContainsKey(arg)) {
                    _switches.Add(arg, new List<string>());
                }

                _switches[arg].Add(param);
                // firstarg++;
            }
            _parameters = args.Where(argument => !(argument.StartsWith("--")));
        }

        /// <summary>
        ///     Loads the configuration.
        /// </summary>
        /// <param name="file"> The file. </param>
        /// <remarks>
        /// </remarks>
        public void LoadConfiguration(string file) {
            var param = "";
            var category = "";

            if (File.Exists(file)) {
                var lines = File.ReadAllLines(file);
                for (var ln = 0; ln < lines.Length; ln++) {
                    var line = lines[ln].Trim();
                    while (line.EndsWith("\\") && ln < lines.Length) {
                        line = line.Substring(0, line.Length - 1);
                        if (++ln < lines.Length) {
                            line += lines[ln].Trim();
                        }
                    }
                    var arg = line;

                    param = "";

                    if (arg.IndexOf("[") == 0) {
                        // category 
                        category = arg.Substring(1, arg.IndexOf(']') - 1).Trim();
                        continue;
                    }

                    if (string.IsNullOrEmpty(arg) || arg.StartsWith(";") || arg.StartsWith("#")) // comments
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(category)) {
                        arg = "{0}-{1}".format(category, arg);
                    }

                    int pos;
                    if ((pos = arg.IndexOf("=")) > -1) {
                        param = arg.Substring(pos + 1);
                        arg = arg.Substring(0, pos).ToLower();

                        if (string.IsNullOrEmpty(param) || string.IsNullOrEmpty(arg)) {
                            Console.WriteLine("Invalid Option in config file [{0}]: {1}", file, line.Trim());
                            _switches.Add("help", new List<string>());
                            return;
                        }
                    }

                    if (!_switches.ContainsKey(arg)) {
                        _switches.Add(arg, new List<string>());
                    }

                    (_switches[arg]).Add(param);
                }
            } else {
                Console.WriteLine("Unable to find configuration file [{0}]", param);
            }
        }

        public IEnumerable<string> Parameters {
            get {
                return _parameters;
            }
        }

        public IDictionary<string, List<string>> Switches {
            get {
                return _switches;
            }
        }

        public IEnumerable<string> GetParametersForSwitchOrNull(string key) {
            if (_switches.ContainsKey(key)) {
                return _switches[key];
            }

            return null;
        }
    }
}