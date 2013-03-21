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
    using Core.Collections;
    using Microsoft.Build.Evaluation;

    public class Condition {
        internal string Parameter;
        internal Project Project;

        private static XDictionary<Project, List<string>> _projects = new XDictionary<Project,List<string>>();

        public static IDictionary<string, string> Create(Project parent) {
            if (!_projects.ContainsKey(parent)) {
                _projects.Add(parent, new List<string>());
            }

            var list = _projects[parent];

            return new DelegateDictionary<string, string>(
                () => list,
                key =>  key ,
                (s, c) => {
                    if (!list.Contains(s)) {
                        list.Add(s);
                    }
                },
                list.Remove,
                list.Clear);
        }

        public static string NormalizeConfigurationKey(string key) {
            return key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {
                '\\', ' '
            }, StringSplitOptions.RemoveEmptyEntries).OrderBy(each => each).Aggregate((c, e) => c + "\\" + e).Trim('\\');
        }
    }
}