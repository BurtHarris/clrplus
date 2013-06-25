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
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Extensions;
    using Platform;

    public class EnvironmentManager {
        private static EnvironmentManager _instance;
        private readonly Stack<IDictionary> _stack = new Stack<IDictionary>();

        public static EnvironmentManager Instance {
            get {
                return _instance ?? (_instance = new EnvironmentManager());
            }
        }

        public void Push() {
            _stack.Push(Environment.GetEnvironmentVariables());
        }

        public void Pop() {
            if (_stack.Count > 0) {
                Apply(_stack.Pop());
            }
        }

        public void Apply(IDictionary env) {
            var keys = env.Keys.ToEnumerable<object>().Select(each => (string)each).ToList();

            var current = Environment.GetEnvironmentVariables();
            foreach (var key in current.Keys.ToEnumerable<object>().Select(each => each.ToString())) {
                if (keys.Contains(key)) {
                    var curval = current[key].ToString();
                    var val = env[key].ToString();
                    if (val != curval) {
                        Environment.SetEnvironmentVariable(key, val);
                        keys.Remove(key);
                    }
                    continue;
                }
                Environment.SetEnvironmentVariable(key, null);
            }
            foreach (var key in keys) {
                if (key.Equals("path", StringComparison.InvariantCultureIgnoreCase)) {
                    var p = (string)env[key];
                    Environment.SetEnvironmentVariable(key, p.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Append(EnvironmentUtility.DotNetFrameworkFolder).Aggregate((c, each) => c + ";" + each));
                } else {
                    Environment.SetEnvironmentVariable(key, (string)env[key]);
                }
            }
        }
    }
}