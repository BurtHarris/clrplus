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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Management.Automation.Runspaces;
    using CSScriptLibrary;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Platform;
    using Platform.Process;
    using Powershell.Core;

    public class Script : MsBuildTaskBase {
        public string Batch {get; set;}
        public string Powershell {get; set;}
        public string CSharp {get; set;}
        public bool Echo {get; set;}

        public bool FailOnNonzeroExit {get; set;}

        [Output]
        public ITaskItem[] StdOut {get; set;}

        [Output]
        public ITaskItem[] StdErr {get; set;}

        [Output]
        public int ExitCode {get; set;}

        public override bool Execute() {
            ExitCode = -2;

            if (Batch.Is()) {
                // create a batch file and execute it.
                var batchfile = Path.Combine(Environment.CurrentDirectory, "__msbuild__{0}__.cmd".format(DateTime.Now.Ticks));

                try {
                    File.WriteAllText(batchfile, "@echo off \r\n" + Batch + @"
REM ===================================================================
REM STANDARD ERROR HANDLING BLOCK
REM ===================================================================
REM Everything went ok!
:success
exit /b 0
        
REM ===================================================================
REM Something not ok :(
:failed
echo ERROR: Failure in script. aborting.
exit /b 1
REM ===================================================================
");
                    var cmd = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\system32\cmd.exe");

                    var args = @"/c ""{0}""".format(batchfile);

                    var proc = AsyncProcess.Start(
                        new ProcessStartInfo(cmd, args) {
                            WindowStyle = ProcessWindowStyle.Normal,
                        });

                    if (Echo) {
                        proc.StandardOutput.ForEach(each => LogMessage(each));
                        proc.StandardError.ForEach(each => LogError(each));
                    }

                    StdErr = proc.StandardError.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    StdOut = proc.StandardOutput.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    ExitCode = proc.ExitCode;

                    return true;
                } catch (Exception e) {
                    Console.WriteLine("{0},{1},{2}", e.GetType().Name, e.Message, e.StackTrace);
                    ExitCode = -3;
                    return false;
                } finally {
                    batchfile.TryHardToDelete();
                }
            }

            if (Powershell.Is()) {
                using (var ps = Runspace.DefaultRunspace.Dynamic()) {
                    DynamicPowershellResult results = ps.InvokeExpression(Powershell);

                    if (Echo) {
                        results.ForEach(each => LogMessage(each.ToString()));
                        results.Errors.ForEach(each => LogError(each.ToString()));
                    }

                    StdErr = results.Errors.Select(each => each.ToString()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    StdOut = results.Select(each => each.ToString()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    ExitCode = results.Errors.Any() ? -1 : 0;
                    return true;
                }
            }

            if (CSharp.Is()) {
                try {
                    var o = new List<string>();
                    var e = new List<string>();
                    dynamic obj = CSScript.Evaluator.LoadMethod(@"int eval( System.Collections.Generic.List<string> StdErr, System.Collections.Generic.List<string> StdOut ) {" + CSharp + @" return 0; }");
                    ExitCode = obj.eval(o, e);

                    if (Echo) {
                        o.ForEach(each => LogMessage(each.ToString()));
                        e.ForEach(each => LogError(each.ToString()));
                    }

                    StdErr = e.Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    StdOut = o.Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    return true;
                } catch (Exception e) {
                    ExitCode = -1;
                    StdErr = ((ITaskItem)new TaskItem("{0}/{1}/{2}".format(e.GetType().Name, e.Message, e.StackTrace))).SingleItemAsEnumerable().ToArray();
                    return true;
                }
            }

            return false;
        }
    }
}