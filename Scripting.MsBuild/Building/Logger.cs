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

namespace ClrPlus.Scripting.MsBuild.Building {
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using CSharpTest.Net.RpcLibrary;
    using Core.Extensions;
    using Languages.PropertySheet;
    using Microsoft.Build.Framework;
    using ServiceStack.Text;

    public delegate bool MSBuildMessage(BuildMessage message);


    public class BuildMessage {
        [DllImport("kernel32.dll")]
        static extern void OutputDebugString(string lpOutputString);

        public byte[] ToByteArray() {
            var result = JsonSerializer.SerializeToString(this);
            OutputDebugString(result);

            // return JsonSerializer.SerializeToString(this).ToByteArray();
            return result.ToByteArray();
        }

        public string EventType {get; set;}
        public string Message {get; set;}
        public string Subcategory {get; set;}
        public string Code {get; set;}
        public string File {get; set;}
        public string ProjectFile {get; set;}
        public int LineNumber {get; set;}
        public int ColumnNumber {get; set;}
        public int EndLineNumber {get; set;}
        public int EndColumnNumber {get; set;}
        public string TaskName {get; set;}
        public string TaskFile {get; set;}
        public bool Succeeded {get; set;}
        public string TargetName {get; set;}
        public string TargetFile {get; set;}
        public string ParentTarget {get; set;}
        public int ProjectId {get; set;}
        public string TargetNames {get; set;}
        public string ToolsVersion {get; set;}
        public int Importance {get; set;}

        public string HelpKeyword {
            get;
            set;
        }
        public string SenderName {
            get;
            set;
        }
        public DateTime Timestamp {
            get;
            set;
        }
        public int ThreadId {
            get;
            set;
        }

        // private IEnumerable targetOutputs;
        // private BuildEventContext parentProjectBuildEventContext;
        // private IDictionary<string, string> globalProperties;
        // private IEnumerable properties;
        // private IEnumerable items;
        // private IDictionary<string, string> environmentOnBuildStart;

        public BuildMessage(string message) {
            EventType = string.Empty;
            Message = message;
        }


        public BuildMessage(BuildWarningEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Code = args.Code;
            ColumnNumber = args.ColumnNumber;
            EndColumnNumber = args.EndColumnNumber;
            EndLineNumber = args.EndLineNumber;
            File = args.File;
            LineNumber = args.LineNumber;
            Message = args.Message;
            ProjectFile = args.ProjectFile;
            Subcategory = args.Subcategory;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;

        }

        public BuildMessage(TaskStartedEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            ProjectFile = args.ProjectFile;
            TaskFile = args.TaskFile;
            TaskName = args.TaskName;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(TaskFinishedEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            ProjectFile = args.ProjectFile;
            Succeeded = args.Succeeded;
            TaskFile = args.TaskFile;
            TaskName = args.TaskName;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(TargetStartedEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            ParentTarget = args.ParentTarget;
            ProjectFile = args.ProjectFile;
            TargetFile = args.TargetFile;
            TargetName = args.TargetName;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(TargetFinishedEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            ProjectFile = args.ProjectFile;
            Succeeded = args.Succeeded;
            TargetFile = args.TargetFile;
            TargetName = args.TargetName;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(BuildStatusEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(BuildErrorEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Code = args.Code;
            ColumnNumber = args.ColumnNumber;
            EndColumnNumber = args.EndColumnNumber;
            EndLineNumber = args.EndLineNumber;
            File = args.File;
            LineNumber = args.LineNumber;
            Message = args.Message;
            ProjectFile = args.ProjectFile;
            Subcategory = args.Subcategory;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(CustomBuildEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(ProjectStartedEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            ProjectFile = args.ProjectFile;
            ProjectId = args.ProjectId;
            TargetNames = args.TargetNames;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(ProjectFinishedEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            ProjectFile = args.ProjectFile;
            Succeeded = args.Succeeded;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(BuildMessageEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Importance = (int)args.Importance;
            Message = args.Message;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(BuildStartedEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }

        public BuildMessage(BuildFinishedEventArgs args) {
            EventType = args.GetType().Name.Replace("EventArgs", "");
            Message = args.Message;
            Succeeded = args.Succeeded;
            HelpKeyword = args.HelpKeyword;
            SenderName = args.SenderName;
            Timestamp = args.Timestamp;
            ThreadId = args.ThreadId;
        }
    }

    public class Logger : Microsoft.Build.Utilities.Logger, IDisposable {
        private readonly ConcurrentQueue<BuildMessage> _messages = new ConcurrentQueue<BuildMessage>();
        private RpcClientApi _client;
        private Task messagePump;
        private bool stop;

        public void Dispose() {
            stop = true;
            if (messagePump != null) {
                messagePump.Wait();
            }

            if (_client != null) {
                _client.Dispose();
                _client = null;
            }
        }

        

        public override void Initialize(IEventSource eventSource) {
            if (eventSource == null) {
                return;
            }

            var p = Parameters.Split(';');

            if (p.Length < 2) {
                throw new Exception("Requires at least pipeName and guid");
            }

            var pipeName = p[0];
            var iid = new Guid(p[1]);

            _client = new RpcClientApi(iid, RpcProtseq.ncacn_np, null, pipeName);
            messagePump = Task.Factory.StartNew(() => {
                try {
                    BuildMessage msg;

                    while (!stop || _messages.Count > 0) {
                        if (_messages.TryDequeue(out msg)) {
                            var result = _client.Execute(msg.ToByteArray());
                            if (result.Length > 0 && result[1] == 0x01) {
                                // we've been asked to kill ourselves.
                                stop = true;
                            }
                            continue;
                        }
                        Thread.Sleep(5);
                    }
                }
                finally {
                    stop = true;
                }

                
            });

            eventSource.BuildFinished += eventSource_BuildFinished;
            eventSource.BuildStarted += eventSource_BuildStarted;
            eventSource.CustomEventRaised += eventSource_CustomEventRaised;
            eventSource.ErrorRaised += eventSource_ErrorRaised;
            eventSource.MessageRaised += eventSource_MessageRaised;
            eventSource.ProjectFinished += eventSource_ProjectFinished;
            eventSource.ProjectStarted += eventSource_ProjectStarted;
            //eventSource.StatusEventRaised += eventSource_StatusEventRaised;
            eventSource.TargetFinished += eventSource_TargetFinished;
            eventSource.TargetStarted += eventSource_TargetStarted;
            eventSource.TaskFinished += eventSource_TaskFinished;
            eventSource.TaskStarted += eventSource_TaskStarted;
            eventSource.WarningRaised += eventSource_WarningRaised;
        }

        private void Execute(BuildMessage message) {
            _messages.Enqueue(message);
        }

        private void eventSource_WarningRaised(object sender, BuildWarningEventArgs e) {
            if (stop) {
                return;
            }
            Execute(new BuildMessage(e));

            if (stop) {
                KillThyself();
            }
        }

        private void eventSource_TaskStarted(object sender, TaskStartedEventArgs e) {
            if (stop) {
                return;
            }
            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e) {
            if (stop) {
                return;
            }
            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_TargetStarted(object sender, TargetStartedEventArgs e) {
            if (stop) {
                return;
            }
            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e) {
            if (stop) {
                return;
            }
            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_StatusEventRaised(object sender, BuildStatusEventArgs e) {
            if (stop) {
                return;
            }
            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e) {
            if (stop) {
                return;
            }

            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e) {
            if (stop) {
                return;
            }
            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_MessageRaised(object sender, BuildMessageEventArgs e) {
            if (stop) {
                return;
            
            }
            if (e.Message.IndexOf("task from assembly") > -1 || e.Message.IndexOf("Building with tools version") > -1) {
                return;
            }
            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e) {
            if (stop) {
                return;
            }

            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_CustomEventRaised(object sender, CustomBuildEventArgs e) {
            if (stop) {
                return;
            }

            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void KillThyself() {
            // Process.GetCurrentProcess().Kill();
            System.Environment.Exit(1);
        }

        private void eventSource_BuildStarted(object sender, BuildStartedEventArgs e) {
            if (stop) {
                return;
            }

            Execute(new BuildMessage(e));
            if (stop) {
                KillThyself();
            }

        }

        private void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e) {
            if (stop) {
                return;
            }

            Execute(new BuildMessage(e));

            while (_messages.Count > 0 && !stop) {
                Thread.Sleep(10);
            }
        }
    }
}