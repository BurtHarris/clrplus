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
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using CSharpTest.Net.RpcLibrary;
    using Core.Extensions;
    using Languages.PropertySheet;
    using Microsoft.Build.Framework;
    using ServiceStack.Text;

    public class BuildMessage {
        public string EventType {get; set;}
        public SourceLocation SourceLocation {get; set;}
        public string Message {get; set;}

        public byte[] ToByteArray() {
            return JsonSerializer.SerializeToString(this).ToByteArray();
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
                BuildMessage msg;

                while (!stop || _messages.Count > 0) {
                    if (_messages.TryDequeue(out msg)) {
                        _client.Execute(msg.ToByteArray());
                        continue;
                    }
                    Thread.Sleep(5);
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
            Execute(new BuildMessage {
                EventType = "WarningRaised",
                SourceLocation = new SourceLocation {
                    Column = e.ColumnNumber,
                    Row = e.LineNumber,
                    SourceFile = e.File
                },
                Message = e.Message
            });
        }

        private void eventSource_TaskStarted(object sender, TaskStartedEventArgs e) {
            Execute(new BuildMessage {
                EventType = "TaskStarted",
                Message = e.Message
            });
        }

        private void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e) {
            Execute(new BuildMessage {
                EventType = "TaskFinished",
                Message = e.Message
            });
        }

        private void eventSource_TargetStarted(object sender, TargetStartedEventArgs e) {
            Execute(new BuildMessage {
                EventType = "TargetStarted",
                Message = e.Message
            });
        }

        private void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e) {
            Execute(new BuildMessage {
                EventType = "TargetFinished",
                Message = e.Message
            });
        }

        private void eventSource_StatusEventRaised(object sender, BuildStatusEventArgs e) {
            Execute(new BuildMessage {
                EventType = "StatusEventRaised",
                Message = e.Message
            });
        }

        private void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e) {
            Execute(new BuildMessage {
                EventType = "ProjectStarted",
                Message = e.Message
            });
        }

        private void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e) {
            Execute(new BuildMessage {
                EventType = "ProjectFinished",
                Message = e.Message
            });
        }

        private void eventSource_MessageRaised(object sender, BuildMessageEventArgs e) {
            if (e.Message.IndexOf("task from assembly") > -1 || e.Message.IndexOf("Building with tools version") > -1) {
                return;
            }
            Execute(new BuildMessage {
                EventType = "MessageRaised",
                Message = e.Message
            });
        }

        private void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e) {
            Execute(new BuildMessage {
                EventType = "ErrorRaised",
                SourceLocation = new SourceLocation {
                    Column = e.ColumnNumber,
                    Row = e.LineNumber,
                    SourceFile = e.File
                },
                Message = e.Message
            });
        }

        private void eventSource_CustomEventRaised(object sender, CustomBuildEventArgs e) {
            Execute(new BuildMessage {
                EventType = "CustomEventRaised",
                Message = e.Message
            });
        }

        private void eventSource_BuildStarted(object sender, BuildStartedEventArgs e) {
            Execute(new BuildMessage {
                EventType = "BuildStarted",
                Message = e.Message
            });
        }

        private void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e) {
            Execute(new BuildMessage {
                EventType = "BuildFinished",
                Message = e.Message
            });
            while (_messages.Count > 0) {
                Thread.Sleep(10);
            }
        }
    }
}