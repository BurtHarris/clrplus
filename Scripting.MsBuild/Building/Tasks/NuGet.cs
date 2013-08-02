namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System.Diagnostics;
    using System.Linq;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Platform.Process;

    public class NuGet : MsBuildTaskBase {
        public ITaskItem[] Push {get; set;}
        public ITaskItem[] Delete {get; set;}
        public ITaskItem[] Install {get; set;}

        public override bool Execute() {
            if (!Push.IsNullOrEmpty()) {
                if (!ExecutePush()) {
                    return false;
                }
            }
            if (!Delete.IsNullOrEmpty()) {
                if (!ExecuteDelete()) {
                    return false;
                }
            }
            if (!Install.IsNullOrEmpty()) {
                if (!ExecuteInstall()) {
                    return false;
                }
            }
            return true;
        }

        public bool ExecutePush() {
            int n = 0;
            foreach (var i in Push.Select(each => each.ItemSpec)) {
                Log.LogMessage("Pushing : '{0}'".format(i));

                var proc = AsyncProcess.Start(new ProcessStartInfo {
                    FileName = "NuGet.exe",
                    Arguments = "push {0}".format(i),
                });

                proc.StandardOutput.ForEach(each => {
                    if (each.Is()) {
                        Log.LogMessage(each);
                    }
                });
                proc.StandardError.ForEach(each => {
                    if (each.Is()) {
                        Log.LogError(each);
                    }
                });
                if (proc.ExitCode != 0) {
                    return false;
                }
                n++;
            }
            Log.LogMessage("Pushed {0} packages".format(n));
            return true;
        }

        public bool ExecuteDelete() {
            int n = 0;
            foreach (var i in Delete.Select(each => each.ItemSpec)) {
                Log.LogMessage("Unlisting : '{0}'".format(i));

                var proc = AsyncProcess.Start(new ProcessStartInfo {
                    FileName = "NuGet.exe",
                    Arguments = "delete {0}".format(i),
                });

                proc.StandardOutput.ForEach(each => {
                    if (each.Is()) {
                        Log.LogMessage(each);
                    }
                });
                /*
                proc.StandardError.ForEach(each => {
                    if (each.Is()) {
                        Log.LogError(each);
                    }
                });
                if (proc.ExitCode != 0) {
                    return false;
                }
                 * */
                n++;
            }

            Log.LogMessage("Unlisted {0} packages".format(n));
            return true;
        }

        public bool ExecuteInstall() {
            return false;
        }
    }

    public class Requires : MsBuildTaskBase {


        public ITaskItem[] Library {get;set;}
        public ITaskItem[] Libraries{get;set;}

        public ITaskItem[] Tool{get;set;}
        public ITaskItem[] Tools{get;set;}


        public override bool Execute() {
            return false;
        }
    }

    public class Install : MsBuildTaskBase {

        public ITaskItem[] Package{get;set;}
        public ITaskItem[] Packages{get;set;}

        public override bool Execute() {
            return false;
        }
    } 
}