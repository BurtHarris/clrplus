namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Platform.Process;
    using ProcessStartInfo = Platform.Process.ProcessStartInfo;

    public class GetEnvironmentFromBatchFile : ITask {
        public bool Execute() {
            try {
                var cmd = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\system32\cmd.exe");
                var args = @"/c ""{0}"" {1} & set ".format(BatchFile.ItemSpec , Parameters.Select(each => each.ItemSpec).Aggregate((cur, each) => cur + @" ".format(each)));

                var proc = AsyncProcess.Start(

                    new ProcessStartInfo(cmd, args) {
                        WindowStyle = ProcessWindowStyle.Normal,
                    });
                proc.WaitForExit();
                
                // var dictionary = new Dictionary<string, string>();
                foreach (var each in proc.StandardOutput.Where(each => each.Is() && each.IndexOf('=') > -1)) {
                    var p = each.IndexOf('=');
                    var key = each.Substring(0, p);
                    var val = each.Substring(p + 1);
                    if (Environment.GetEnvironmentVariable(key) != val) {
                        Environment.SetEnvironmentVariable(key, val);
                    }
                }

                return true;
            }
            catch(Exception e) {
                Console.WriteLine("{0},{1},{2}", e.GetType().Name, e.Message, e.StackTrace);
                return false;
            }
        }

        [Required]
        public ITaskItem BatchFile { get; set; }

        [Required]
        public ITaskItem[] Parameters { get; set; }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
    }
}