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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using CSharpTest.Net.RpcLibrary;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Tasks;
    using Core.Utility;
    using Languages.PropertySheet;
    using Languages.PropertySheetV3;
    using Microsoft.Build.Framework;
    using Platform;
    using Platform.Process;
    using ServiceStack.Text;
    using Debug = System.Diagnostics.Debug;

    public class MsBuildEx : MsBuildTaskBase {
        private static int _maxThreads;
        private static MSBuildTaskScheduler _scheduler;
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private static readonly List<MsBuildEx> _builds = new List<MsBuildEx>();
        private static int Counter;

        private static Regex[] filters = new[] {
            new Regex("Target.*skipped. Previously"),
            new Regex("Overriding target"),
        };

        internal static bool _stopOnError;

        internal ManualResetEvent Completed = new ManualResetEvent(false);
        public int Index;
        internal ConcurrentQueue<BuildMessage> Messages = new ConcurrentQueue<BuildMessage>();
        private IDictionary _environment;
        protected string[] projectFiles;
        private bool skip;

        public MsBuildEx() {
            ResetEnvironmentFirst = true;
        }

        private static TaskScheduler Scheduler {
            get {
                lock (typeof (MsBuildEx)) {
                    if (_scheduler == null) {
                        var max = Environment.GetEnvironmentVariable("MaxThreads");
                        var n = max.ToInt32(0);
                        if (n == 0) {
                            n = Environment.ProcessorCount;
                        }
                        _scheduler = new MSBuildTaskScheduler(_maxThreads != 0 ? _maxThreads : n);
                    }
                    return _scheduler;
                }
            }
        }

        public static bool AnyBuildsRunning {
            get {
                lock (_builds) {
                    return _builds.Any();
                }
            }
        }

        public static MsBuildEx[] Builds {
            get {
                lock (_builds) {
                    return _builds.ToArray();
                }
            }
        }

        public bool StopOnFirstError {
            get {
                return _stopOnError;
            }
            set {
                _stopOnError = value;
            }
        }

        public bool Result {set; get;}

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

        private static void ReleaseScheduler() {
            lock (typeof (MsBuildEx)) {
                if (_scheduler != null && !_scheduler.IsRunning) {
                    _scheduler = null;
                }
            }
        }

        public static void KillOutstandingBuilds() {
            if (_stopOnError) {
                cancellationTokenSource.Cancel();
            }
        }

        public static void RemoveBuild(MsBuildEx build) {
            lock (_builds) {
                _builds.Remove(build);
            }
        }

        public override bool Execute() {
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


        protected bool ValidateParameters() {
            Index = ++Counter;

            if ((((ushort)GetKeyState(0x91)) & 0xffff) != 0) {
                Debugger.Break();
            }

            if (Projects.IsNullOrEmpty()) {
                return false;
            }
            var badFiles = new List<string>();

            projectFiles = Projects.Select(each => {
                var r = each.ItemSpec.GetFullPath();
                if (File.Exists(r)) {
                    return each.ItemSpec.GetFullPath();
                }
                badFiles.Add(each.ItemSpec);
                return null;
            }).ToArray();

            if (badFiles.Count > 0) {
                LogError("Unable to resolve location of project files:");
                badFiles.ForEach(each => LogError("»  {0}".format(each)));
                return false;
            }

            lock (typeof (MsBuildEx)) {
                try {
                    EnvironmentUtility.Push();

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
                                    Messages.Enqueue( new BuildMessage( SkippingMessage));
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
                    EnvironmentUtility.Pop();
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
                    Messages.Enqueue( new BuildMessage( StartMessage));
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
                            var message = JsonSerializer.DeserializeFromString<BuildMessage>(arg.ToUtf8String());
                            if (!filters.Any(each => each.IsMatch(message.Message))) {
                                Messages.Enqueue(message);
                            }
                            if (cancellationTokenSource.IsCancellationRequested) {
                                return new byte[1] {
                                    0x01
                                };
                            }

                            return new byte[0];
                        };

                    foreach (var project in projectFiles) {
                        if (cancellationTokenSource.IsCancellationRequested) {
                            Result = false;
                            return;
                        }

                        currentProjectName = project;
                        if (ProjectStartMessage.Is()) {
                            Messages.Enqueue(new BuildMessage(ProjectStartMessage));
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
                                    ,
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
                            Messages.Enqueue( new BuildMessage( "{0},{1},{2}".format(e.GetType().Name, e.Message, e.StackTrace)) {
                                EventType = "BuildError",
                            });

                            Result = false;
                            return;
                        }

                        if (ProjectEndMessage.Is()) {
                            Messages.Enqueue(new BuildMessage (ProjectEndMessage));
                        }
                    }
                }

                if (EndMessage.Is()) {
                    Messages.Enqueue(new BuildMessage(EndMessage));
                }
            } catch (Exception e) {
                Messages.Enqueue(new BuildMessage("{0},{1},{2}".format(e.GetType().Name, e.Message, e.StackTrace)) {
                    EventType = "BuildError",
                });
            } finally {
                Completed.Set();
            }
        }
    }

    public class InvokeBuild : MsBuildTaskBase {
        public string Location {get; set;}
        public string ScriptFile {get; set;}
        public ITaskItem[] Defines {
            get;
            set;
        }
        public ITaskItem[] Targets {
            get;
            set;
        }

        public int MaxThreads {
            get;
            set;
        }

        private static int MaxBuildId = 0;

        public override bool Execute() {
            int buildId;

            try {
                dynamic buildEngine = BuildEngine.AccessPrivate();

                var host = buildEngine.host;
                dynamic pHost = new AccessPrivateWrapper(host);
                var bp = pHost.buildParameters;
                dynamic pBp = new AccessPrivateWrapper(bp);

                buildId = pBp.buildId;

                lock (typeof(InvokeBuild)) {
                    if (buildId <= MaxBuildId) {
                        // this happens when we build a solution, and it wants to build each project
                        return true;
                    }
                    MaxBuildId = buildId;
                }
            }
            catch {
            
            }

            using (new PushDirectory(Environment.CurrentDirectory)) {
                try {
                    if (Location.Is()) {
                        Environment.CurrentDirectory = Location.GetFullPath();
                    }

                    if (string.IsNullOrWhiteSpace(ScriptFile)) {
                        // search for it.
                        ScriptFile = new[] {
                            @"copkg\.buildinfo", @"contrib\.buildinfo", @"contrib\coapp\.buildinfo", @".buildinfo"
                        }.WalkUpPaths();

                        if (string.IsNullOrEmpty(ScriptFile)) {
                            throw new ClrPlusException(@"Unable to find .buildinfo file anywhere in the current directory structure.");
                        }
                    }

                    if (!File.Exists(ScriptFile)) {
                        throw new ClrPlusException(@"Unable to find Invoke-build script file '{0}'.".format(ScriptFile));
                    }

                    string[] defines = Defines.IsNullOrEmpty() ? new string[0] : Defines.Select(each => each.ItemSpec).ToArray();

                    using (var buildScript = new BuildScript(ScriptFile)) {

                        buildScript.BuildMessage += message => {
                            try {
                                switch (message.EventType) {
                                    case "BuildWarning":
                                        Log.LogWarning(message.Subcategory, message.Code, message.HelpKeyword, message.File, message.LineNumber, message.ColumnNumber, message.EndLineNumber, message.EndColumnNumber, message.Message);
                                        break;
                                    case "BuildError":
                                        Log.LogError(message.Subcategory, message.Code, message.HelpKeyword, message.File, message.LineNumber, message.ColumnNumber, message.EndLineNumber, message.EndColumnNumber, message.Message);
                                        break;
                                    case "ProjectStarted":
                                        Log.LogExternalProjectStarted(message.Message, message.HelpKeyword, message.ProjectFile, message.TargetNames);
                                        break;
                                    case "ProjectFinished":
                                        Log.LogExternalProjectFinished(message.Message, message.HelpKeyword, message.ProjectFile, message.Succeeded);
                                        break;
                                    case "TaskStarted":
                                        Log.LogMessage(MessageImportance.Low, message.Message);
                                        break;
                                    case "TaskFinished":
                                        Log.LogMessage(MessageImportance.Low, message.Message);
                                        break;
                                    case "TargetStarted":
                                        Log.LogMessage(MessageImportance.Low, message.Message);
                                        break;
                                    case "TargetFinished":
                                        Log.LogMessage(MessageImportance.Low, message.Message);
                                        break;
                                    case "BuildStarted":
                                        Log.LogMessage(MessageImportance.Low, message.Message);
                                        break;
                                    case "BuildFinished":
                                        Log.LogMessage(MessageImportance.Low, message.Message);
                                        break;
                                    case "BuildMessage":
                                        Log.LogMessage((MessageImportance)message.Importance, message.Message);
                                        break;
                                    default:
                                        Log.LogMessage(MessageImportance.Low, message.Message);
                                        break;
                                }
                                
                            }
                            catch (Exception e) {
                                LogError("{0}/{1}/{2}", e.GetType().Name, e.Message, e.StackTrace );
                            }
                            return false;
                        };

                        foreach (var i in defines) {
                            var p = i.IndexOf("=");
                            var k = p > -1 ? i.Substring(0, p) : i;
                            var v = p > -1 ? i.Substring(p + 1) : "";
                            buildScript.AddMacro(k, v);
                        }

                        var targets = Targets.IsNullOrEmpty() ? new string[0] : Targets.Select(each => each.ItemSpec).ToArray();

                        if (Targets.IsNullOrEmpty()) {
                            targets = new string[] {
                                "default"
                            };
                        }

                        Environment.SetEnvironmentVariable("MaxThreads", "" + MaxThreads);
                        Environment.SetEnvironmentVariable("HIDE_THREADS", "true");

                        buildScript.MaxThreads = MaxThreads;
                        return buildScript.Execute(targets);
                    }

                } catch (Exception e) {
                    LogError("{0}/{1}/{2}".format(e.GetType().Name, e.Message, e.StackTrace));
                    return false;
                } finally {
                    Environment.SetEnvironmentVariable("HIDE_THREADS", null);
                    Environment.SetEnvironmentVariable("MaxThreads", null);
                }
            }

        }
    }
}