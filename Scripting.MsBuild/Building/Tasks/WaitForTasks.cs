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
    using Core.Extensions;

    public class WaitForTasks : MsBuildTaskBase {
        public override bool Execute() {
            while (MsBuildEx.AnyBuildsRunning) {
                foreach (var msbuild in MsBuildEx.Builds) {
                    // Yeah, as if this ever worked...
                    // (BuildEngine as IBuildEngine3).Yield();
                    BuildMessage message;
                    while (msbuild.Messages.TryDequeue(out message)) {
                        message.Message = "{0,4} » {1}".format(msbuild.Index, message.Message);
                        switch (message.EventType) {
                            case "WarningRaised":
                                Log.LogWarning("" + msbuild.Index, "", "", message.SourceLocation.SourceFile, message.SourceLocation.Row, message.SourceLocation.Column, 0, 0, message.Message);
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
                                // Log.LogMessage(message.Message);
                                break;
                            case "TargetFinished":
                                // Log.LogMessage(message.Message);
                                break;
                            case "BuildStarted":
                                // Log.LogMessage(message.Message);
                                break;
                            case "BuildFinished":
                                // Log.LogMessage(message.Message);
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
}