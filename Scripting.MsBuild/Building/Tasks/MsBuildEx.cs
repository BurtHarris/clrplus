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
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using CSharpTest.Net.RpcLibrary;
    using Core.Collections;
    using Core.Extensions;
    using Core.Utility;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Platform;
    using Platform.Process;
    using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
    using Task = System.Threading.Tasks.Task;


    public class WaitForTasks : ITask {
        public IBuildEngine BuildEngine {
            get;
            set;
        }
        public ITaskHost HostObject {
            get;
            set;
        }

        private TaskLoggingHelper log;

        public WaitForTasks() {
            log = new TaskLoggingHelper(this);
        }

        public TaskLoggingHelper Log {
            get {
                return this.log;
            }
        }

        public bool Execute() {
            while (MsBuildEx.AnyBuildsRunning) {
                foreach (var msbuild in MsBuildEx.Builds) {

                    // Yeah, as if this ever worked...
                    // (BuildEngine as IBuildEngine3).Yield();
                    BuildMessage message;
                    while (msbuild.Messages.TryDequeue(out message)) {
                        message.Message = "{0,4} » {1}".format(msbuild.Index, message.Message);
                        switch (message.EventType) {
                            case "WarningRaised":
                                Log.LogWarning(""+msbuild.Index, "", "", message.SourceLocation.SourceFile, message.SourceLocation.Row, message.SourceLocation.Column, 0, 0, message.Message);
                                break;
                            case "ErrorRaised":
                                Log.LogError("" + msbuild.Index, "", "", message.SourceLocation.SourceFile, message.SourceLocation.Row, message.SourceLocation.Column, 0, 0, message.Message);
                                break;
                            case "ProjectStarted":
                                // Log.LogExternalProjectStarted(message.Message, "", currentProjectName, "");
                                break;
                            case "ProjectFinished":
                                // Log.LogExternalProjectFinished(message.Message, "", currentProjectName, true);
                                break;
                            case "TaskStarted":
                                // Log.LogMessage(message.Message);
                                break;
                            case "TaskFinished":
                                // Log.LogMessage(message.Message);
                                break;
                            case "TargetStarted":
                                Log.LogMessage(message.Message);
                                break;
                            case "TargetFinished":
                                Log.LogMessage(message.Message);
                                break;
                            case "BuildStarted":
                                Log.LogMessage(message.Message);
                                break;
                            case "BuildFinished":
                                Log.LogMessage(message.Message);
                                break;
                            case "MessageRaised":
                                Log.LogMessage(message.Message);
                                break;
                            default:
                                Log.LogMessage(message.Message);
                                break;
                        }
                    }

                    // psshhhh.
                    // (BuildEngine as IBuildEngine3).Reacquire();

                    if (msbuild.Completed.WaitOne(0)) {
                        // remove it from the list of active builds
                        MsBuildEx.RemoveBuild(msbuild);

                        // if it failed, then signal the build as a failure
                        if (!msbuild.Result) {
                            MsBuildEx.KillOutstandingBuilds();
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }

    public class MsBuildEx : ITask {
        public IBuildEngine BuildEngine {
            get;
            set;
        }
        public ITaskHost HostObject {
            get;
            set;
        }

        private static int _maxThreads;
        private static MSBuildTaskScheduler _scheduler;
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private static TaskScheduler Scheduler { get {
            lock (typeof (MsBuildEx)) {
                if (_scheduler == null) {
                    var max = Environment.GetEnvironmentVariable("MaxThreads");
                    var n = max.ToInt32(0);
                    if (n == 0) {
                        n = Environment.ProcessorCount;
                    }
                    _scheduler = new MSBuildTaskScheduler(_maxThreads != 0 ? _maxThreads: n);
                }
                return _scheduler;
            }
        }}

        private static void ReleaseScheduler() {
            lock (typeof (MsBuildEx)) {
                if (_scheduler != null && !_scheduler.IsRunning) {
                    _scheduler = null;
                }
            }
        }

        public static void KillOutstandingBuilds() {
            cancellationTokenSource.Cancel();
        }

        private static readonly List<MsBuildEx> _builds = new List<MsBuildEx>();
        public static bool AnyBuildsRunning { get {
            lock (_builds) {
                return _builds.Any();
            }
        }}

        public static MsBuildEx[] Builds {
            get {
                lock (_builds) {
                    return _builds.ToArray();
                }
            }
        }

        public static void RemoveBuild(MsBuildEx build ){
            lock (_builds) {
                _builds.Remove(build);
            }
        }


        public bool Result {set;get;}

        public bool Execute() {
            if (!ValidateParameters()) {
                return false;
            }

            if (skip) {
                return true;
            }

            _builds.Add(this);

            Task.Factory.StartNew(ExecuteTool, cancellationTokenSource.Token, TaskCreationOptions.LongRunning, Scheduler);

            return true;
        }

        private IDictionary _environment;

        public MsBuildEx() {
            ResetEnvironmentFirst = true;
        }

        internal ManualResetEvent Completed = new ManualResetEvent(false);

        private bool skip;
        public bool ResetEnvironmentFirst {get; set;}
        public string SkippingMessage {get; set;}
        public string StartMessage {get; set;}
        public string EndMessage {get; set;}
        public string ProjectStartMessage {get; set;}
        public string ProjectEndMessage {get; set;}

        public ITaskItem[] LoadEnvironmentFromTargets {get; set;}

        [Required]
        public ITaskItem[] Projects {get; set;}

        public ITaskItem[] Properties {get; set;}

        public int MaxThreads {
            get {
                return _maxThreads;
            }
            set {
                _maxThreads = value;
            }
        }

        protected string MSBuildExecutable {
            get {
                return @"{0}\MSBuild.exe".format(EnvironmentUtility.DotNetFrameworkFolder);
            }
        }

        internal ConcurrentQueue<BuildMessage> Messages = new ConcurrentQueue<BuildMessage>();

        protected string[] projectFiles;

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        public static extern short GetKeyState(int keyCode);

        private static int Counter;
        public int Index;

        protected bool ValidateParameters() {
            Index = ++Counter;

            if ((((ushort)GetKeyState(0x91)) & 0xffff) != 0) {
                Debugger.Break();
            }
            
            if (Projects.IsNullOrEmpty()) {
                return false;
            }

            projectFiles = Projects.Select(each => each.ItemSpec.GetFullPath()).ToArray();

            lock (typeof(MsBuildEx)) {
                try {
                    EnvironmentManager.Instance.Push();

                    if (ResetEnvironmentFirst) {
                        new LoadSystemEnvironment().Execute();
                    }

                    if (!LoadEnvironmentFromTargets.IsNullOrEmpty()) {
                        foreach (var tgt in LoadEnvironmentFromTargets.Select(each => each.ItemSpec)) {
                            var seft = new SetEnvironmentFromTarget {
                                Target = tgt,
                                BuildEngine = BuildEngine,
                                HostObject = HostObject,
                            };
                            seft.Execute();
                            if (!seft.IsEnvironmentValid) {
                                if (SkippingMessage.Is()) {
                                    Messages.Enqueue(new BuildMessage {
                                        EventType = "",
                                        Message = SkippingMessage
                                    });
                                }
                                skip = true;
                                return true;
                            }
                        }
                    }

                    var vars = Environment.GetEnvironmentVariables();
                    _environment = new XDictionary<string, string>();

                    foreach (var i in vars.Keys) {
                        _environment.Add(i.ToString(), ((string)vars[i]) ?? "");
                    }

                } finally {
                    EnvironmentManager.Instance.Pop();
                }
            }
            return true;
        }

        protected void ExecuteTool() {
            try {
                if ((((ushort)GetKeyState(0x91)) & 0xffff) != 0) {
                    Debugger.Break();
                }
                if (skip) {
                    return;
                }

                if (StartMessage.Is()) {
                    Messages.Enqueue(new BuildMessage {
                        EventType = "",
                        Message = StartMessage
                    });

                }

                Guid iid = Guid.NewGuid();
                string pipeName = @"\pipe\ptk_{0}_{1}".format(Process.GetCurrentProcess().Id, Index);
                Result = true;

                using (var server = new RpcServerApi(iid)) {
                    string currentProjectName = string.Empty;
                    //Allow up to 5 connections over named pipes
                    server.AddProtocol(RpcProtseq.ncacn_np, pipeName, 5);
                    //Authenticate via WinNT
                    // server.AddAuthentication(RpcAuthentication.RPC_C_AUTHN_WINNT);

                    //Start receiving calls
                    server.StartListening();
                    //When a call comes, do the following:
                    server.OnExecute +=
                        (client, arg) => {
                            // deserialize the message object and replay thru this logger. 
                            var message = ServiceStack.Text.JsonSerializer.DeserializeFromString<BuildMessage>(arg.ToUtf8String());
                            Messages.Enqueue(message);

                            return new byte[0];
                        };

                    foreach (var project in projectFiles) {
                        if (cancellationTokenSource.IsCancellationRequested) {
                            Result = false;
                            return;
                        }

                        currentProjectName = project;
                        if (ProjectStartMessage.Is()) {
                            Messages.Enqueue(new BuildMessage {
                                EventType = "",
                                Message = ProjectStartMessage
                            });

                        }

                        try {
                            // no logo, thanks.
                            var parameters = " /nologo";

                            // add properties lines.
                            if (!Properties.IsNullOrEmpty()) {
                                parameters = parameters + " /p:" + Properties.Select(each => each.ItemSpec).Aggregate((c, e) => c + ";" + e);
                            }

                            parameters = parameters + @" /noconsolelogger ""/logger:ClrPlus.Scripting.MsBuild.Building.Logger,{0};{1};{2}"" ""{3}""".format(Assembly.GetExecutingAssembly().Location, pipeName, iid, project);
                            if ((((ushort)GetKeyState(0x91)) & 0xffff) != 0) {
                                Debugger.Break();
                            }

                            var proc = AsyncProcess.Start(
                                new ProcessStartInfo(MSBuildExecutable, parameters) {
                                    WindowStyle = ProcessWindowStyle.Normal
                                }, _environment);

                            while (!proc.WaitForExit(20)) {
                                if (cancellationTokenSource.IsCancellationRequested) {
                                    proc.Kill();
                                }
                            }
                            
                            // StdErr = proc.StandardError.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                            // StdOut = proc.StandardOutput.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                            if (proc.ExitCode != 0) {
                                Result = false;
                                return;
                            }
                            ;
                        } catch (Exception e) {
                            
                            Messages.Enqueue(new BuildMessage {
                                EventType = "ErrorRaised",
                                Message = "{0},{1},{2}".format( e.GetType().Name, e.Message, e.StackTrace)
                            });

                            Result = false;
                            return;
                        }

                        if (ProjectEndMessage.Is()) {
                            Messages.Enqueue(new BuildMessage {
                                EventType = "ErrorRaised",
                                Message = ProjectEndMessage
                            });
                        }
                    }
                }

                if (EndMessage.Is()) {
                    Messages.Enqueue(new BuildMessage {
                        EventType = "ErrorRaised",
                        Message = EndMessage
                    });
                }
            } catch (Exception e) {
                Messages.Enqueue(new BuildMessage {
                    EventType = "ErrorRaised",
                    Message = "{0},{1},{2}".format(
                    e.GetType().Name,
                    e.Message,
                    e.StackTrace)
                });

            } finally {
                Completed.Set();
            }

        }
    }
}