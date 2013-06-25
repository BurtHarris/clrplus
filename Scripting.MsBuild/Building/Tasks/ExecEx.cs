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
    using Packaging;
    using Platform;
    using Powershell.Core;

    public class ExecEx : ITask {
        public bool Execute() {
            try {
                var parameters = Parameters == null ? "" : Parameters.Select(each => each.ItemSpec).Aggregate((cur, each) => cur + @" ".format(each));
                var proc = AsyncProcess.Start(
                    new ProcessStartInfo(Executable.ItemSpec, parameters) {
                        WindowStyle = ProcessWindowStyle.Normal,
                    });
                proc.WaitForExit();
                StdErr = proc.StandardError.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                StdOut = proc.StandardOutput.Where(each => each.Is()).Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                return true;
            } catch (Exception e) {
                Console.WriteLine("{0},{1},{2}", e.GetType().Name, e.Message, e.StackTrace);
                return false;
            }
        }

        [Required]
        public ITaskItem Executable {get; set;}

        public ITaskItem[] Parameters {get; set;}

        [Output]
        public ITaskItem[] StdOut {get; set;}

        [Output]
        public ITaskItem[] StdErr {get; set;}

        public IBuildEngine BuildEngine {get; set;}
        public ITaskHost HostObject {get; set;}
    }

    public class Script : ITask {
        public IBuildEngine BuildEngine {get; set;}
        public ITaskHost HostObject {get; set;}

        public string Batch {get; set;}
        public string Powershell {get; set;}
        public string CSharp {get; set;}

        public bool FailOnNonzeroExit {get; set;}

        [Output]
        public ITaskItem[] StdOut {get; set;}

        [Output]
        public ITaskItem[] StdErr {get; set;}

        [Output]
        public int ExitCode {get; set;}

        public bool Execute() {
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

    public class WriteNugetPackage : ITask {
        public IBuildEngine BuildEngine {get; set;}
        public ITaskHost HostObject {get; set;}

        [Required]
        public ITaskItem Package {get; set;}

        [Output]
        public ITaskItem[] AllPackages {get; set;}

        [Output]
        public ITaskItem[] MainPackages {get; set;}

        [Output]
        public ITaskItem[] RedistPackages {get; set;}

        [Output]
        public ITaskItem[] SymbolsPackages {get; set;}

        [Output]
        public bool NuGetSuccess {
            get;
            set;
        }

        public TaskItem[] Defines {get; set;}

        public string PackageDirectory {get; set;}

        public bool Execute() {
            var pkgPath = Package.ItemSpec;

            var defines = Defines.IsNullOrEmpty() ? new string[0]: Defines.Select(each => each.ItemSpec).ToArray();

            try {
                using (var script = new PackageScript(pkgPath)) {
                    if (PackageDirectory.Is()) {
                        script.AddNuGetPackageDirectory(PackageDirectory.GetFullPath());
                    }
                    if (defines != null) {
                        foreach (var i in defines) {
                            var p = i.IndexOf("=");
                            var k = p > -1 ? i.Substring(0, p) : i;
                            var v = p > -1 ? i.Substring(p + 1) : "";
                            script.AddMacro(k, v);
                        }
                    }

                    script.Save(PackageTypes.NuGet, true);

                    AllPackages = script.Packages.Select(each => (ITaskItem)new TaskItem(each)).ToArray();
                    
                    RedistPackages = AllPackages.Where(each => each.ItemSpec.ToLower().IndexOf("redist") > -1).ToArray();
                    SymbolsPackages = AllPackages.Where(each => each.ItemSpec.ToLower().IndexOf("symbols") > -1).ToArray();
                    MainPackages = AllPackages.Where(each => each.ItemSpec.ToLower().IndexOf("redist") > -1 && each.ItemSpec.ToLower().IndexOf("symbols") == -1).ToArray();
                    
                    foreach (var p in RedistPackages) {
                        var n = Path.GetFileNameWithoutExtension(p.ItemSpec);
                        var o = n.IndexOf(".redist.");

                        p.SetMetadata("pkgIdentity", "{0} {1}".format(n.Substring(0, o+7), n.Substring(o + 8)));
                    }
                    
                    NuGetSuccess = true;
                    return true;
                }
            } catch {
            }
            NuGetSuccess = false;
            return false;
        }
    }
}