using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Scripting.MsBuild.Building {
    using CSharpTest.Net.RpcLibrary;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheet;
    using Microsoft.Build.Framework;
    using Remoting;
    using ServiceStack.Text;

    public class BuildMessage {
        public string EventType {get; set;}
        public SourceLocation SourceLocation {get; set;}
        public string Message { get; set; }

        public byte[] ToByteArray() {
            return JsonSerializer.SerializeToString(this).ToByteArray();
        }
    }

    public class Logger : Microsoft.Build.Utilities.Logger, IDisposable  {
       public Logger() {
           
       }

        private RpcClientApi _client;
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

        void eventSource_WarningRaised(object sender, BuildWarningEventArgs e) {
            
            _client.Execute(new BuildMessage { EventType = "WarningRaised", SourceLocation = new SourceLocation { Column = e.ColumnNumber, Row= e.LineNumber, SourceFile = e.File }, Message = e.Message }.ToByteArray());
        }

        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "TaskStarted",  Message = e.Message}.ToByteArray());
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "TaskFinished",  Message = e.Message}.ToByteArray());
        }

        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "TargetStarted",  Message = e.Message}.ToByteArray());
        }

        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "TargetFinished", Message = e.Message}.ToByteArray());
        }

        void eventSource_StatusEventRaised(object sender, BuildStatusEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "StatusEventRaised", Message = e.Message}.ToByteArray());
        }

        void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e) {
                _client.Execute(new BuildMessage {
                    EventType = "ProjectStarted",
                    Message = e.Message
                }.ToByteArray());
        }

        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "ProjectFinished",  Message = e.Message}.ToByteArray());
        }

        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e) {
            if (e.Message.IndexOf("task from assembly") > -1 || e.Message.IndexOf("Building with tools version") > -1) {
                return;
            }
            _client.Execute(new BuildMessage { EventType = "MessageRaised",  Message = e.Message}.ToByteArray());
        }

        void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "ErrorRaised", SourceLocation = new SourceLocation { Column = e.ColumnNumber, Row= e.LineNumber, SourceFile = e.File }, Message = e.Message}.ToByteArray());
        }

        void eventSource_CustomEventRaised(object sender, CustomBuildEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "CustomEventRaised",  Message = e.Message}.ToByteArray());
        }

        void eventSource_BuildStarted(object sender, BuildStartedEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "BuildStarted", Message = e.Message}.ToByteArray());
        }

        void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e) {
            _client.Execute(new BuildMessage { EventType = "BuildFinished",  Message = e.Message}.ToByteArray());
        }

        public void Dispose() {
            if (_client != null) {
                _client.Dispose();
                _client = null;
            }
        }
    }
}
