using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Scripting.MsBuild.Building {
    using Core.Tasks;
    using Microsoft.Build.Framework;

    public class Logger : Microsoft.Build.Utilities.Logger  {
        public override void Initialize(IEventSource eventSource) {
            if (eventSource == null) {
                return;
            }

            eventSource.BuildFinished += eventSource_BuildFinished;
            eventSource.BuildStarted += eventSource_BuildStarted;
            eventSource.CustomEventRaised += eventSource_CustomEventRaised;
            eventSource.ErrorRaised += eventSource_ErrorRaised;
            eventSource.MessageRaised += eventSource_MessageRaised;
            eventSource.ProjectFinished += eventSource_ProjectFinished;
            eventSource.ProjectStarted += eventSource_ProjectStarted;
            eventSource.StatusEventRaised += eventSource_StatusEventRaised;
            eventSource.TargetFinished += eventSource_TargetFinished;
            eventSource.TargetStarted += eventSource_TargetStarted;
            eventSource.TaskFinished += eventSource_TaskFinished;
            eventSource.TaskStarted += eventSource_TaskStarted;
            eventSource.WarningRaised += eventSource_WarningRaised;
        }

        void eventSource_WarningRaised(object sender, BuildWarningEventArgs e) {
        }

       
        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e) {
            
        }

        void eventSource_TaskFinished(object sender, TaskFinishedEventArgs e) {
            
        }

        void eventSource_TargetStarted(object sender, TargetStartedEventArgs e) {
            
        }

        void eventSource_TargetFinished(object sender, TargetFinishedEventArgs e) {
            
        }

        void eventSource_StatusEventRaised(object sender, BuildStatusEventArgs e) {
            
        }

        void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e) {
            
        }

        void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e) {
            
        }

        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e) {
            
        }

        void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e) {
            
        }

        void eventSource_CustomEventRaised(object sender, CustomBuildEventArgs e) {
            
        }

        void eventSource_BuildStarted(object sender, BuildStartedEventArgs e) {
            
        }

        void eventSource_BuildFinished(object sender, BuildFinishedEventArgs e) {
            
        }
    }
}
