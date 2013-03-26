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

namespace ClrPlus.Scripting.MsBuild {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;
    using Core.Collections;
    using Core.DynamicXml;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Utility;
    using Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;

    public class Configurations {
        public static void Add(View view, Project project) {
            var prjPlus = project.Lookup();
            if (prjPlus.View != null) {
                throw new ClrPlusException("Configurations already registered for project");
            }
            prjPlus.View = view;

            var pkgName = view.GetMacroValue("pkgname");

            // add the startup/init tasks  
            var task = project.Xml.AddUsingTask(pkgName + "_Contains", @"$(MSBuildToolsPath)\Microsoft.Build.Tasks.v4.0.dll", null);
            task.TaskFactory = "CodeTaskFactory";
            var pgroup = task.AddParameterGroup();
            pgroup.AddParameter("Text", "false", string.Empty, "System.String");
            pgroup.AddParameter("Library", "false", "true", "System.String");
            pgroup.AddParameter("Value", "false", "true", "System.String");
            pgroup.AddParameter("Result", "true", string.Empty, "System.String");
                
            var body = task.AddUsingTaskBody(string.Empty, string.Empty);

            // thank you.
            body.XmlElement().Append("Code").InnerText = @"Result = ((Text ?? """").Split(';').Contains(Library) ) ? Value : String.Empty;";
           
            var initTarget = project.AddInitTarget(pkgName + "_init");

            var pivots = view.GetChildPropertyNames();

            foreach (var pivot in pivots) {
                dynamic cfg = view.GetProperty(pivot);
                IEnumerable<string> choices = cfg.choices;

                if(!((View)cfg).GetChildPropertyNames().Contains("key")) {
                    // add init steps for this.
                    var finalPropName = "{0}-{1}".format(pivot, pkgName);

                    foreach (var choice in choices) {
                        
                        var choicePropName = "{0}-{1}".format(pivot, choice);


                        var tsk = initTarget.AddTask("pkgName_Contains");
                        tsk.SetParameter("Text", choicePropName);
                        tsk.SetParameter("Library", pkgName);
                        tsk.SetParameter("Value", choice);
                        tsk.Condition = @"'$({0})'==''".format(finalPropName);
                        tsk.AddOutputProperty("Result", finalPropName);
                    }

                    project.Xml.AddPropertyGroup().AddProperty(finalPropName, choices.FirstOrDefault()).Condition = @"'$({0})' == ''".format(finalPropName);
                }
            }

        }

        internal static string NormalizeConditionKey(Project project, string key) {
            if(!project.HasProject()) {
                return key;
            }

            return NormalizeConditionKey(key, project.Lookup().View);
        }

        private static IEnumerable<string> NormalizeWork( View view, IEnumerable<string> options, bool fix = false) {
            foreach(var p in view.GetChildPropertyNames()) {
                var pivot = p;
                foreach (var option in from option in options let cfg = view.GetProperty(pivot) where ((dynamic)cfg).choices.Values.Contains(option) select option) {
                    yield return option;
                    
                    break;
                }
            }
        }

        public static string NormalizeConditionKey(string key, View configurationsView) {
            if (string.IsNullOrEmpty(key)) {
                return string.Empty;
            }

            var x = new Regex(@"\s*(!*)\s*(\w*)\s*([|,&\\/]*)\s*");

            foreach (Match match in x.Matches(key)) {
                var bang = match.Groups[1].Value == "!";
                var name = match.Groups[2].Value;
                var oper = match.Groups[3].Value;

            }

            var pivots = configurationsView.GetChildPropertyNames().ToArray();

            var opts = key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {
                '\\', ' '
            }, StringSplitOptions.RemoveEmptyEntries);

            var options = opts.ToList();
         
            var ordered = new List<string>();
            
            foreach (var pivot in pivots) {
                foreach(var option in options) {
                    dynamic cfg = configurationsView.GetProperty(pivot);
                    if (cfg.choices.Values.Contains(option)) {
                        ordered.Add(option);
                        options.Remove(option);
                        break;
                    }
                }
            }
           

            if(options.Any()) {
                // went thru one pass,and we had some that didn't resolve.
                // try again, this time cheat if we have to 
                var unfound = options;

                options = opts.ToList();
                foreach (var opt in unfound) {
                    switch (opt.ToLower()) {
                        case "x86":
                        case "win32":
                        case "ia32":
                        case "386":
                            options.Remove(opt);
                            options.Add("Win32");
                            break;

                        case "x64":
                        case "amd64":
                        case "em64t":
                        case "intel64":
                        case "x86-64":
                        case "x86_64":
                            options.Remove(opt);
                            options.Add("x64");
                            break;

                        case "woa":
                        case "arm":
                            options.Remove(opt);
                            options.Add("ARM");
                            break;

                        case "ia64":
                            options.Remove(opt);
                            options.Add("ia64");
                            break;
                    }
                }

                ordered = new List<string>();

                foreach(var pivot in pivots) {
                    foreach(var option in options) {
                        dynamic cfg = configurationsView.GetProperty(pivot);
                        IEnumerable<string> choices = cfg.choices.Values;
                        if(choices.ContainsIgnoreCase(option)) {
                            ordered.Add(option);
                            options.Remove(option);
                            break;
                        }
                    }
                }

                if (options.Any()) {
                    // STILL?! bail.
                    throw new ClrPlusException("Unknown configuration choice: {0}".format(options.FirstOrDefault()));
                }
            }


            return ordered.Aggregate((c, e) => c + "\\" + e).Trim('\\');
        }

        public static string GenerateCondition(Project project, string key) {
            var view = project.Lookup().View;
            var pivots = view.GetChildPropertyNames();

            var options = key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {
                '\\', ' '
            }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var conditions = new List<string>();

            foreach(var pivot in pivots) {
                foreach(var option in options) {
                    dynamic cfg = view.GetProperty(pivot);
                    IEnumerable<string> choices = cfg.choices;
                    if(choices.Contains(option)) {
                        if(((View)cfg).GetChildPropertyNames().Contains("key")) {
                            // this is a standard property, us
                            conditions.Add("'$({0})' == '{1}'".format((string)cfg.Key, option));
                        } else {
                            conditions.Add("'$({0}-{1})' == '{2}'".format((string)pivot, view.GetMacroValue("pkgname"), option));
                        }

                        options.Remove(option);
                        break;
                    }
                }

            }

            if (options.Any()) {
                throw new ClrPlusException("Unknown configuration choice: {0}".format(options.FirstOrDefault()));
            }

            return conditions.Aggregate((current, each) => (string.IsNullOrEmpty(current) ? current : (current + " and ")) + each);
        }
    }
}