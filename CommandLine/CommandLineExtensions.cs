//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Original Copyright (c) 2009 Microsoft Corporation. All rights reserved.
//     Changes Copyright (c) 2010  Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

// -----------------------------------------------------------------------
// Original Code: 
// (c) 2009 Microsoft Corporation -- All rights reserved
// This code is licensed under the MS-PL
// http://www.opensource.org/licenses/ms-pl.html
// Courtesy of the Open Source Techology Center: http://port25.technet.com
// -----------------------------------------------------------------------

namespace ClrPlus.CommandLine {
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Core.Extensions;
    using Platform;

    /// <summary>
    /// </summary>
    /// <remarks>
    ///     NOTE: Explicity Ignore, testing this will produce no discernable value, and will only lead to heartbreak.
    /// </remarks>
    public static class CommandLineExtensions {
        /// <summary>
        ///     Gets the parameters for switch.
        /// </summary>
        /// <param name="args"> The args. </param>
        /// <param name="key"> The key. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static IEnumerable<string> GetParametersForSwitch(this IEnumerable<string> args, string key) {
            return ParsedCommandLine.Parse(args).With(p => p.Switches.ContainsKey(key) ? p.Switches[key] : Enumerable.Empty<string>());
        }

        /// <summary>
        ///     Switches the value.
        /// </summary>
        /// <param name="args"> The args. </param>
        /// <param name="key"> The key. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static string SwitchValue(this IEnumerable<string> args, string key) {
            return ParsedCommandLine.Parse(args).With(p => p.Switches.ContainsKey(key) ? p.Switches[key].FirstOrDefault() : null);
        }

        public static IEnumerable<string> SplitArgs(string unsplitArgumentLine) {
            return CommandLine.SplitArgs(unsplitArgumentLine);
        }

        /// <summary>
        ///     Switcheses the specified args.
        /// </summary>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static IDictionary<string, List<string>> Switches(this IEnumerable<string> args) {
            return ParsedCommandLine.Parse(args).Switches;
        }

#if TODO
    // move this to the ClrPlus.Console project

        public static void ListBugTrackers() {
            using (new ConsoleColors(ConsoleColor.Cyan, ConsoleColor.Black)) {
                Console.WriteLine(Assembly.GetEntryAssembly().Logo());
            }
            Assembly.GetEntryAssembly().SetLogo("");
            using (new ConsoleColors(ConsoleColor.White, ConsoleColor.Black)) {
                foreach (var a in GetBugTrackers()) {
                    Console.WriteLine("Bug Tracker URL for Assembly [{0}]: {1}", a.Key.Title(), a.Value);
                }
            }
        }

        public static void OpenBugTracker() {
            foreach (var a in GetBugTrackers()) {
                if (a.Key == Assembly.GetEntryAssembly()) {
                    Process.Start(a.Value);
                    return;
                }
            }
            var tracker = GetBugTrackers().FirstOrDefault().Value;
            if (tracker != null) {
                Process.Start(tracker);
            }
        }

        public static IEnumerable<KeyValuePair<Assembly, string>> GetBugTrackers() {
            return from a in AppDomain.CurrentDomain.GetAssemblies()
                let attributes = a.GetCustomAttributes(false)
                from attribute in attributes.Where(attribute => (attribute as Attribute) != null).Where(attribute => (attribute as Attribute).GetType().Name == "AssemblyBugtrackerAttribute")
                select new KeyValuePair<Assembly, string>(a, attribute.ToString());
        }
#endif

        // handles complex option switches
        // RX for splitting comma seperated values:
        //  http://dotnetslackers.com/Regex/re-19977_Regex_This_regex_splits_comma_or_semicolon_separated_lists_of_optionally_quoted_strings_It_hand.aspx
        //      @"\s*[;,]\s*(?!(?<=(?:^|[;,])\s*""(?:[^""]|""""|\\"")*[;,])(?:[^""]|""""|\\"")*""\s*(?:[;,]|$))"
        //  http://regexlib.com/REDetails.aspx?regexp_id=621
        //      @",(?!(?<=(?:^|,)\s*\x22(?:[^\x22]|\x22\x22|\\\x22)*,)(?:[^\x22]|\x22\x22|\\\x22)*\x22\s*(?:,|$))"
        /// <summary>
        ///     Gets the complex options.
        /// </summary>
        /// <param name="rawParameterList"> The raw parameter list. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static IEnumerable<ComplexOption> GetComplexOptions(this IEnumerable<string> rawParameterList) {
            var optionList = new List<ComplexOption>();
            foreach (var p in rawParameterList) {
                var m = Regex.Match(p, @"\[(?>\"".*?\""|\[(?<DEPTH>)|\](?<-DEPTH>)|[^[]]?)*\](?(DEPTH)(?!))");
                if (m.Success) {
                    var co = new ComplexOption();
                    var v = m.Groups[0].Value;
                    var len = v.Length;
                    co.WholePrefix = v.Substring(1, len - 2);
                    co.WholeValue = p.Substring(len);

                    var parameterStrings = Regex.Split(co.WholePrefix, @",(?!(?<=(?:^|,)\s*\x22(?:[^\x22]|\x22\x22|\\\x22)*,)(?:[^\x22]|\x22\x22|\\\x22)*\x22\s*(?:,|$))");
                    foreach (var q in parameterStrings) {
                        v = q.Trim();
                        if (v[0] == '"' && v[v.Length - 1] == '"') {
                            v = v.Trim('"');
                        }
                        co.PrefixParameters.Add(v);
                    }

                    var values = co.WholeValue.Split('&');
                    foreach (var q in values) {
                        var pos = q.IndexOf('=');
                        if (pos > -1 && pos < q.Length - 1) {
                            co.Values.Add(q.Substring(0, pos).UrlDecode(), q.Substring(pos + 1).UrlDecode());
                        } else {
                            co.Values.Add(q.Trim('='), "");
                        }
                    }
                    optionList.Add(co);
                }
            }
            return optionList;
        }

        // public static List<string> Data(this string[] args) {
        /// <summary>
        ///     Parameterses the specified args.
        /// </summary>
        /// <param name="args"> The args. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public static IEnumerable<string> Parameters(this IEnumerable<string> args) {
            return ParsedCommandLine.Parse(args).Parameters;
        }

        public static string ToCommandLine(this IEnumerable<string> args) {
            return args.Aggregate((current, each) => current + string.Format(@"{0}{1}{2}{3}",
                current.Length > 0 ? " ":"", 
                each.IndexOf(' ') > -1 ? @"""" : "", 
                each,
                current.Length > 0 ? " ":""
                ));
        }

        /// <summary>
        /// </summary>
        public const string HelpConfigSyntax =
            @"
Advanced Command Line Configuration Files 
-----------------------------------------
You may pass any double-dashed command line options in a configuration file 
that is loaded with --load-config=<file>.

Inside the configuration file, omit the double dash prefix; simply put 
each option on a seperate line.

On the command line:

    --some-option=foo 

would become the following inside the configuration file: 

    some-option=foo

Additionally, options in the configuration file can be grouped together in 
categories. The category is simply syntatic sugar for simplifying the command
line.

Categories are declared with the square brackets: [] 

The category is appended to options that follow its declaration.

A configuration file expressed as:

source-option=foo
source-option=bar
source-option=bin
source-add=baz
source-ignore=bug

can also be expressed as:

[source]
option=foo
option=bar
option=bin
add=baz
ignore=bug
";
    }
}