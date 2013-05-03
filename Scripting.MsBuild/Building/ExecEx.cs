using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Scripting.MsBuild.Building {
    using System.Diagnostics;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Platform.Process;
    using ProcessStartInfo = Platform.Process.ProcessStartInfo;

    public class ExecEx : ITask {
        public bool Execute() {
            try {
                var proc  = AsyncProcess.Start(
                    new ProcessStartInfo(Executable.ItemSpec, Parameters.Select(each => each.ItemSpec).Aggregate((cur, each) => cur + @" ".format(each))) {
                        WindowStyle = ProcessWindowStyle.Normal,
                    });
                proc.WaitForExit();
                StdErr = proc.StandardError.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                StdOut = proc.StandardOutput.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                return true;
            } catch (Exception e) {
                Console.WriteLine("{0},{1},{2}",e.GetType().Name, e.Message, e.StackTrace);
                return false;
            }
        }

        [Required]
        public ITaskItem Executable {get; set;}

        [Required]
        public ITaskItem[] Parameters { get; set; }

        [Output]
        public ITaskItem[] StdOut { get; set;}

        [Output]
        public ITaskItem[] StdErr { get; set; }

        public IBuildEngine BuildEngine {get; set;}
        public ITaskHost HostObject { get; set; }
    }
}
