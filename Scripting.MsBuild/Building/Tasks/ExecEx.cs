namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Diagnostics;
    using System.Management.Automation.Runspaces;
    using CSScriptLibrary;
    using ClrPlus.Core.Extensions;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using ClrPlus.Platform.Process;
    using Platform;
    using Powershell.Core;
    using ProcessStartInfo = Platform.Process.ProcessStartInfo;

    public class ExecEx : ITask {
        public bool Execute() {
            try {
                var parameters = Parameters == null ? "" : Parameters.Select(each => each.ItemSpec).Aggregate((cur, each) => cur + @" ".format(each));
                var proc  = AsyncProcess.Start(
                    new ProcessStartInfo(Executable.ItemSpec,parameters) {
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

        public ITaskItem[] Parameters { get; set; }

        [Output]
        public ITaskItem[] StdOut { get; set;}

        [Output]
        public ITaskItem[] StdErr { get; set; }

        public IBuildEngine BuildEngine {get; set;}
        public ITaskHost HostObject { get; set; }
    }

    public class Script : ITask {
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        public string Batch { get; set;}
        public string Powershell { get; set; }
        public string CSharp { get; set; }

        public bool FailOnNonzeroExit {get; set;}
        
        [Output]
        public ITaskItem[] StdOut { get; set; }

        [Output]
        public ITaskItem[] StdErr { get; set; }

        [Output]
        public int ExitCode { get; set; }

        public bool Execute() {
            ExitCode = -2;

            if (Batch.Is()) {
                // create a batch file and execute it.
                var batchfile = Path.Combine(Environment.CurrentDirectory, "__msbuild__{0}__.cmd".format(DateTime.Now.Ticks));

                try {
                    File.WriteAllText(batchfile,"@echo off \r\n" +  Batch+ @"
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
                    proc.WaitForExit();
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

                    StdErr = results.Errors.Select(each => each.ToString()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    StdOut = results.Select( each => each.ToString() ).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    ExitCode = results.Errors.Any() ? -1 : 0 ;
                    return true;
                }
            }

            if(CSharp.Is()) {
                try {
                    var o = new List<string>();
                    var e = new List<string>();
                    dynamic obj = CSScript.Evaluator.LoadMethod( @"int eval( System.Collections.Generic.List<string> StdErr, System.Collections.Generic.List<string> StdOut ) {" + CSharp + @" return 0; }");
                    ExitCode = obj.eval(o,e);
                    StdErr = e.Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    StdOut = o.Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    return true;
                } catch(Exception e) {
                    ExitCode = -1;
                    StdErr = ((ITaskItem)new TaskItem("{0}/{1}/{2}".format(e.GetType().Name, e.Message, e.StackTrace))).SingleItemAsEnumerable().ToArray();
                    return true;
                }

            }

            return false;
        }
    }

}
