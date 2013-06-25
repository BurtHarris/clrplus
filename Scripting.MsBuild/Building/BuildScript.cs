// ----------------------------------------------------------------------
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

namespace ClrPlus.Scripting.MsBuild.Building {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Management.Automation.Runspaces;
    using System.Reflection;
    using System.Threading;
    using CSharpTest.Net.RpcLibrary;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Languages.PropertySheetV3.RValue;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Framework;
    using Packaging;
    using Platform;
    using Powershell.Core;
    using Remoting;
    using Utility;

    public class BuildScript : IDisposable, IProjectOwner {
        private readonly Pivots _pivots;
        private readonly ProjectPlus _project;
        protected RootPropertySheet _sheet;
        internal IDictionary<string, IValue> productInformation;
        private IDictionary<string, string> _macros = new Dictionary<string, string>() ;
        public BuildScript(string filename) {
            try {
                Filename = filename.GetFullPath();
                _project = new ProjectPlus(this, Filename + ".msbuild");
                _project.Xml.AddImport(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "etc", "MSBuild.ExtensionPack.tasks"));

                _sheet = new RootPropertySheet(_project);
                _sheet.ParseFile(Filename);

                _pivots = new Pivots(_sheet.View.configurations);

                _sheet.AddChildRoutes(_project.MemberRoutes);
                // _sheet.AddChildRoutesForType(typeof (ProjectTargetElement), _project.TargetRoutes);

                _sheet.CurrentView.AddMacroHandler((name, context) => _macros.ContainsKey(name.ToLower()) ? _macros[name.ToLower()] : null);
                _sheet.CurrentView.AddMacroHandler((name, context) => System.Environment.GetEnvironmentVariable(name));
                // convert #product-info into a dictionary.
                productInformation = _sheet.Metadata.Value.Keys.Where(each => each.StartsWith("product-info")).ToXDictionary(each => each.Substring(12), each => _sheet.Metadata.Value[each]);
            } catch {
                Dispose();
            }
        }

        public int MaxThreads = Environment.ProcessorCount;

        public void AddMacro(string key, string value ) {
            _macros.AddOrSet(key.ToLower(), value);
        }

        public string Filename {get; set;}

        private static IEnumerable<ToRoute> PtkRoutes {
            get {
                /*
                yield return "set".MapTo<ProjectTargetElement>(tgt => tgt.EnvironmentList());
                yield return "uses".MapTo<ProjectTargetElement>(tgt => tgt.Uses());
                yield return "use".MapTo<ProjectTargetElement>(tgt => tgt.Uses());
                yield return "default".MapTo<ProjectTargetElement>(tgt => tgt.DefaultFlag());
                yield return "build-command".MapTo<ProjectTargetElement>(tgt => tgt.BuildCommand());
                yield return "clean-command".MapTo<ProjectTargetElement>(tgt => tgt.CleanCommand());
                yield return "platform".MapTo<ProjectTargetElement>(tgt => tgt.Platform());
                yield return "compiler".MapTo<ProjectTargetElement>(tgt => tgt.UsesTool());
                yield return "sdk".MapTo<ProjectTargetElement>(tgt => tgt.UsesTool());
                yield return "targets".MapTo<ProjectTargetElement>(tgt => tgt.ProducesTargets());
                yield return "generate".MapTo<ProjectTargetElement>(tgt => tgt.GenerateFiles());
                yield return "requires".MapTo<ProjectTargetElement>(tgt => tgt.RequiresPackages());
                */

                yield return "condition".MapTo<ProjectTargetElement>(tgt => tgt.Condition());
                yield return "*".MapTo<ProjectTargetElement>(tgt => tgt.Condition());

                yield return "$$INDEXED".MapIndexedChildrenTo<ProjectTargetElement>((tgt, child) => tgt.GetTargetItem(child)); // .tasks 
            }
        }

        public void Dispose() {
            _sheet = null;
            _project.Dispose();
        }

        public Pivots Pivots {
            get {
                return _pivots;
            }
        }

        public string ProjectName {
            get {
                return Path.GetFileNameWithoutExtension(Filename);
            }
        }

        public string Directory {
            get {
                return Path.GetDirectoryName(Filename);
            }
        }

        public string EmitScript() {
            _sheet.CopyToModel();
            return Save();
        }

        public string ScriptText() {
            _sheet.CopyToModel();
            var path = Save();
            var result = File.ReadAllText(path);
            path.TryHardToDelete();
            return result;
        }

        private static string[] filterMessages = new string[] { "due to false condition" , "Environment Variables passed to tool" };

        private bool _stop;
        public void Stop() {
            _stop = true;
        }

        public void Execute(string[] targets = null) {
            _sheet.CopyToModel();

            targets = targets ?? new string[0];
            var messages = new Queue<BuildMessage>();
            
            var path = Save();

            Guid iid = Guid.NewGuid();
            // Guid iid = new Guid("12345678123456781234567812345678");

            string pipeName = @"\pipe\ptk_{0}".format(System.Diagnostics.Process.GetCurrentProcess().Id);
            // string pipeName = @"\pipe\ptk_1".format(System.Diagnostics.Process.GetCurrentProcess().Id);

            using (var server = new RpcServerApi(iid)) {
                //Allow up to 5 connections over named pipes
                server.AddProtocol(RpcProtseq.ncacn_np, pipeName, 5);
                //Authenticate via WinNT
                // server.AddAuthentication(RpcAuthentication.RPC_C_AUTHN_WINNT);

                //Start receiving calls
                server.StartListening();
                //When a call comes, do the following:
                server.OnExecute +=
                    (client, arg) => {
                        lock (messages) {
                            messages.Enqueue(ServiceStack.Text.JsonSerializer.DeserializeFromString<BuildMessage>(arg.ToUtf8String()));
                        }
                        return new byte[0];
                    };

                // Event<Verbose>.Raise("script", "\r\n\r\n{0}\r\n\r\n", File.ReadAllText(path));
                
                var targs = targets.IsNullOrEmpty() ? string.Empty : targets.Aggregate("/target:", (cur, each) => cur + each + ";").TrimEnd(';');

                var etcPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "etc")+ "/";
                Environment.SetEnvironmentVariable("CoAppEtcDirectory", etcPath);

                EnvironmentUtility.EnvironmentPath = EnvironmentUtility.EnvironmentPath.Append(EnvironmentUtility.DotNetFrameworkFolder);


                Event<Verbose>.Raise("msbuild", @"{6}\MSBuild.exe",
                    @" /nologo /noconsolelogger ""/logger:ClrPlus.Scripting.MsBuild.Building.Logger,{0};{1};{2}""  /m:{7} /p:MaxThreads={7} ""/p:CoAppEtcDirectory={3}"" {4} ""{5}""".format(Assembly.GetExecutingAssembly().Location, pipeName, iid, etcPath, targs, path, EnvironmentUtility.DotNetFrameworkFolder, MaxThreads > 0 ? MaxThreads : Environment.ProcessorCount));

                var proc = Process.Start(new ProcessStartInfo(@"{0}\MSBuild.exe".format(EnvironmentUtility.DotNetFrameworkFolder),
                    @" /nologo /noconsolelogger ""/logger:ClrPlus.Scripting.MsBuild.Building.Logger,{0};{1};{2}"" /m:{6} /p:MaxThreads={6} ""/p:CoAppEtcDirectory={3}"" {4} ""{5}""".format(Assembly.GetExecutingAssembly().Location, pipeName, iid, etcPath, targs, path, MaxThreads > 0 ? MaxThreads : Environment.ProcessorCount)) {
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                    });
                
                while (!proc.HasExited) {
                    // check our messages -- we need to work on the calling thread. 
                    // Thanks powershell, I appreciate working like it's 1989 again. 
                    Thread.Sleep(20); // not so tight of loop. 

                    lock (messages) {
                        while (messages.Any()) {
                            var obj = messages.Dequeue();
                            switch (obj.EventType) {

                                case "WarningRaised":
                                    Event<SourceWarning>.Raise("WARNING", obj.SourceLocation.SingleItemAsEnumerable(), obj.Message);
                                    break;

                                case "ErrorRaised":
                                    Event<SourceError>.Raise("ERROR", obj.SourceLocation.SingleItemAsEnumerable(), obj.Message);
                                    break;

                                case "ProjectStarted":
                                case "ProjectFinished":
                                case "TaskStarted":
                                case "TaskFinished":
                                case "TargetStarted":
                                case "TargetFinished":
                                case "BuildStarted":
                                case "BuildFinished":
                                    Event<Verbose>.Raise(obj.EventType, obj.Message);
                                    break;

                                case "MessageRaised":
                                    if (filterMessages.Any(each => obj.Message.IndexOf(each) > -1)) {
                                        Event<Verbose>.Raise("", obj.Message);
                                    }
                                    else {
                                        Event<Message>.Raise("", obj.Message);
                                    }

                                    break;

                                default:
                                    Event<Message>.Raise(obj.EventType, obj.Message);
                                    break;
                            }
                        }
                    }
                }
                while (!proc.WaitForExit(20)) {
                    if (_stop) {
                        proc.Kill();
                    }
                }
                

                var stderr = proc.StandardError.ReadToEnd();
                if (stderr.Is()) {
                    Event<Error>.Raise("stderr", stderr);
                }

                var stdout = proc.StandardOutput.ReadToEnd();
                if(stdout.Is()) {
                    Event<Verbose>.Raise("stdout", stdout);
                }

                path.TryHardToDelete();
            }
        }

        public string Save(string filename=null) {
            filename = filename ?? Filename + ("." + DateTime.Now.Ticks + ".msbuild").MakeSafeFileName(); //  filename ?? "pkt.msbuild".GenerateTemporaryFilename();
            _project.Save(filename);
            return filename;
        }
    }
}