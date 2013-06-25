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
    using System.Linq;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Platform.Process;

    public class ExecEx : MsBuildTaskBase {
        [Required]
        public ITaskItem Executable {get; set;}

        public ITaskItem[] Parameters {get; set;}

        public bool Echo {get; set;}

        [Output]
        public ITaskItem[] StdOut {get; set;}

        [Output]
        public ITaskItem[] StdErr {get; set;}

        public override bool Execute() {
            try {
                var parameters = Parameters == null ? "" : Parameters.Select(each => each.ItemSpec).Aggregate((cur, each) => cur + @" ".format(each));
                var proc = AsyncProcess.Start(
                    new ProcessStartInfo(Executable.ItemSpec, parameters) {
                        WindowStyle = ProcessWindowStyle.Normal,
                    });

                if (Echo) {
                    proc.StandardOutput.ForEach(each => LogMessage(each));
                    proc.StandardError.ForEach(each => LogError(each));
                }

                StdErr = proc.StandardError.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                StdOut = proc.StandardOutput.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                return true;
            } catch (Exception e) {
                Console.WriteLine("{0},{1},{2}", e.GetType().Name, e.Message, e.StackTrace);
                return false;
            }
        }
    }
}