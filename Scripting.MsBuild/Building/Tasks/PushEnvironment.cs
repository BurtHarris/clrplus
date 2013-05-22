using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System.Collections;
    using Core.Extensions;
    using Microsoft.Build.Framework;

    public class EnvironmentManager {
        private readonly Stack<IDictionary> _stack = new Stack<IDictionary>();

        private static EnvironmentManager _instance;
        public static EnvironmentManager Instance {get {
            return _instance ?? (_instance = new EnvironmentManager());
        } }

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
            foreach(var key in current.Keys.ToEnumerable<object>().Select(each => each.ToString())) {
                if(keys.Contains(key)) {
                    var curval = current[key].ToString();
                    var val = env[key].ToString();
                    if(val != curval) {
                        Environment.SetEnvironmentVariable(key, val);
                        keys.Remove(key);
                    }
                    continue;
                }

                Environment.SetEnvironmentVariable(key, null);
            }
            foreach(var key in keys) {

                Environment.SetEnvironmentVariable(key, (string)env[key]);
            }
        }
    }

    public class PushEnvironment : ITask {
        public bool Execute() {
            EnvironmentManager.Instance.Push();
            return true;
        }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
    }

    public class PopEnvironment : ITask {
        public bool Execute() {
            EnvironmentManager.Instance.Pop();
            return true;
        }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
    }

    public class LoadSystemEnvironment : ITask {
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
            "MSBUILDLOADMICROSOFTTARGETSREADONLY",
        };

        public bool Execute() {

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
                if (key.ToLower().IndexOf("path") > -1 || (s.Is() && s.IndexOf(';') > -1) || (u.Is() && u.IndexOf(';') > -1) ||key.ToLower() == "lib" || key.ToLower() == "include") {
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

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
    }

    public class AppendEnvironment : ITask {
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        public bool Execute() {
            return true;
        }
    }
}
