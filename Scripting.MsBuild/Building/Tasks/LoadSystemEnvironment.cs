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

namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System;
    using System.Linq;
    using Core.Extensions;

    public class LoadSystemEnvironment : MsBuildTaskBase {
        private string[] _ignore = new[] {
            "SYSTEMDRIVE",
            "PROGRAMFILES(X86)",
            "PROGRAMW6432",
            "USERPROFILE",
            "USERNAME",
            "LOGONSERVER",
            "SYSTEMROOT",
            "COMMONPROGRAMFILES",
            "PROGRAMDATA",
            "HOMEPATH",
            "COMPUTERNAME",
            "ALLUSERSPROFILE",
            "COMMONPROGRAMW6432",
            "COMMONPROGRAMFILES(X86)",
            "HOMEDRIVE",
            "PROGRAMFILES",
            "PROMPT",
            "APPDATA",
            "USERDOMAIN",
            "LOCALAPPDATA",
            "USERDOMAIN_ROAMINGPROFILE",
            "PUBLIC",
            "COAPPETCDIRECTORY",
            "MSBUILDLOADMICROSOFTTARGETSREADONLY",
        };

        public override bool Execute() {
            var keys = Environment.GetEnvironmentVariables().Keys.ToEnumerable<object>().Select(each => each.ToString()).Union(
                Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine).Keys.ToEnumerable<object>().Select(each => each.ToString())).Union(
                    Environment.GetEnvironmentVariables(EnvironmentVariableTarget.User).Keys.ToEnumerable<object>().Select(each => each.ToString())).ToArray();

            foreach (var key in keys) {
                if (_ignore.ContainsIgnoreCase(key)) {
                    continue;
                }

                var s = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Machine);
                var u = Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User);
                var val = string.Empty;
                if (key.ToLower().IndexOf("path") > -1 || (s.Is() && s.IndexOf(';') > -1) || (u.Is() && u.IndexOf(';') > -1) || key.ToLower() == "lib" || key.ToLower() == "include") {
                    // combine these fields
                    if (s.Is()) {
                        val = s;
                    }

                    if (u.Is()) {
                        val = val.Is() ? val + ";" + u : u;
                    }
                } else {
                    // otherwise user overrides system.
                    if (u.Is()) {
                        val = u;
                    } else {
                        if (s.Is()) {
                            val = s;
                        }
                    }
                }
                if (val == "") {
                    val = null;
                }
                if (val != Environment.GetEnvironmentVariable(key)) {
                    Environment.SetEnvironmentVariable(key, val);
                }
            }

            return true;
        }
    }
}