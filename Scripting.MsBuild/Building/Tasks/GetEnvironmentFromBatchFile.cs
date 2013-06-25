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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Platform.Process;

    public class GetEnvironmentFromBatchFile : MsBuildTaskBase {
        [Required]
        public ITaskItem BatchFile {get; set;}

        [Required]
        public ITaskItem[] Parameters {get; set;}

        public override bool Execute() {
            try {
                var cmd = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\system32\cmd.exe");
                if (!File.Exists(BatchFile.ItemSpec)) {
                    return false;
                }

                var args = @"/c ""{0}"" {1} & set ".format(BatchFile.ItemSpec, Parameters.Select(each => each.ItemSpec).Aggregate((cur, each) => cur + @" ".format(each)));

                var proc = AsyncProcess.Start(
                    new ProcessStartInfo(cmd, args) {
                        WindowStyle = ProcessWindowStyle.Normal,
                    });
                proc.WaitForExit();

                if (proc.ExitCode != 0) {
                    return false;
                }

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
            } catch (Exception e) {
                Console.WriteLine("{0},{1},{2}", e.GetType().Name, e.Message, e.StackTrace);
            }
            return false;
        }
    }
}