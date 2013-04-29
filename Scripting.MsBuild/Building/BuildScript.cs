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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation.Runspaces;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Languages.PropertySheetV3.RValue;
    using Microsoft.Build.Construction;
    using Packaging;
    using Platform;
    using Powershell.Core;
    using Utility;

    public class BuildScript : IDisposable, IProjectOwner {
        private readonly Pivots _pivots;
        private readonly ProjectPlus _project;
        protected RootPropertySheet _sheet;
        internal IDictionary<string, IValue> productInformation;

        public BuildScript(string filename) {
            Filename = filename.GetFullPath();

            _sheet = new RootPropertySheet(_project);
            _sheet.ParseFile(Filename);
            _project = new ProjectPlus(this, Filename + ".msbuild");
            _pivots = new Pivots(_sheet.View.configurations);

            foreach (var target in _sheet.CurrentView.ReplaceableChildren) {
                target.MapTo(_project.LookupTarget(target), PtkRoutes);
            }

            _sheet.AddChildRoutes(_project.MemberRoutes);

            // convert #product-info into a dictionary.
            productInformation = _sheet.Metadata.Value.Keys.Where(each => each.StartsWith("product-info")).ToXDictionary(each => each.Substring(12), each => _sheet.Metadata.Value[each]);

            _sheet.CopyToModel();
        }

        public string Filename {get; set;}

        private static IEnumerable<ToRoute> PtkRoutes {
            get {
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

                yield return "condition".MapTo<ProjectTargetElement>(tgt => tgt.Condition());
                yield return "*".MapTo<ProjectTargetElement>(tgt => tgt.Condition());

                yield return "CHILDREN".MapIndexedChildrenTo<ProjectTargetElement>((tgt, child) => tgt.GetTargetItem(child)); // .tasks 
            }
        }

        public void Dispose() {
            _sheet = null;
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

        public void Execute(string[] Targets) {
            var path = Save();

            using (dynamic ps = Runspace.DefaultRunspace.Dynamic()) {
                var results = ps.InvokeExpression(@"msbuild.exe pack ""{0}"" 2>&1".format(path));
                bool lastIsBlank = false;
                foreach (var r in results) {
                    string s = r.ToString();
                    if (string.IsNullOrWhiteSpace(s)) {
                        if (lastIsBlank) {
                            continue;
                        }
                        lastIsBlank = true;
                    } else {
                        if (s.IndexOf("Issue: Assembly outside lib folder") > -1) {
                            continue;
                        }
                        if (s.IndexOf("folder and hence it won't be added as reference when the package is installed into a project") > -1) {
                            continue;
                        }
                        if (s.IndexOf("Solution: Move it into the 'lib' folder if it should be referenced") > -1) {
                            continue;
                        }
                        if (s.IndexOf("issue(s) found with package") > -1) {
                            continue;
                        }

                        lastIsBlank = false;
                    }

                    // Issue: Assembly outside lib folder.
                    // Description: The assembly 'build\native\bin\Win32\v110\Release\WinRT\casablanca110.winrt.dll' is not inside the 'lib' folder and hence it won't be added as reference when the package is installed into a project.
                    // Solution: Move it into the 'lib' folder if it should be referenced.

                    Event<Message>.Raise(" >", "{0}", s);
                }
            }
        }


        public string Save(string filename=null) {
            filename = filename ?? "pkt.msbuild".GenerateTemporaryFilename();

            return filename;
        }

        
    }

    
}