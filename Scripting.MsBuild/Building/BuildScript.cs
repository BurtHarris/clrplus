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
    using Core.Extensions;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Languages.PropertySheetV3.RValue;
    using Microsoft.Build.Construction;
    using Packaging;
    using Platform;
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
    }
}