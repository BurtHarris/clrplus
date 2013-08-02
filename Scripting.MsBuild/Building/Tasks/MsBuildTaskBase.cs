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
    using System.Runtime.InteropServices;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public abstract class MsBuildTaskBase : ITask {
        private TaskLoggingHelper log;

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern short GetKeyState(int keyCode);

        public MsBuildTaskBase() {
            log = new TaskLoggingHelper(this);
        }

        public TaskLoggingHelper Log {
            get {
                return log;
            }
        }

        public abstract bool Execute();

        public IBuildEngine BuildEngine {get; set;}
        public ITaskHost HostObject {get; set;}

        public void LogMessage(string message, params object[] objs) {
            if (message.Is()) {
                Log.LogMessage(message, objs);
            }
        }

        public void LogError(string message, params object[] objs) {
            if (message.Is()) {
                Log.LogError(message, objs);
            }
        }

        public void LogWarning(string message, params object[] objs) {
            if (message.Is()) {
                Log.LogWarning(message, objs);
            }
        }
    }
}