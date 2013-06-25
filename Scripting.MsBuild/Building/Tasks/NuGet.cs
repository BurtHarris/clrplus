using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System.Diagnostics;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Platform.Process;

    public class NuGet : ITask {
        
        public TaskItem[] Push;
        public TaskItem[] Delete;
        public TaskItem[] Install;

        private TaskLoggingHelper log;

        public NuGet() {
            log = new TaskLoggingHelper(this);
        }

        public bool Execute() {
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
            foreach (var i in Push.Select(each => each.ItemSpec)) {
                var proc = AsyncProcess.Start(new ProcessStartInfo {
                    FileName = "NuGet.exe",
                    Arguments = "push {0}".format(i),
                });
                proc.WaitForExit();
                proc.StandardOutput.ForEach( each => log.LogMessage(each) );
                proc.StandardError.ForEach(each => log.LogError(each));
                if (proc.ExitCode != 0) {
                    return false;
                }

            }
            return true;
        }

        public bool ExecuteDelete() {
            foreach (var i in Push.Select(each => each.ItemSpec)) {
                var proc = AsyncProcess.Start(new ProcessStartInfo {
                    FileName = "NuGet.exe",
                    Arguments = "delete {0}".format(i),
                });
                proc.WaitForExit();
                proc.StandardOutput.ForEach(each => log.LogMessage(each));
                proc.StandardError.ForEach(each => log.LogError(each));
                if (proc.ExitCode != 0) {
                    return false;
                }
            }
            return true;
        }

        public bool ExecuteInstall() {
            return false;
        }

        public IBuildEngine BuildEngine {
            get;
            set;
        }
        public ITaskHost HostObject {
            get;
            set;
        }
    }
}
