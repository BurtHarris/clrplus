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

namespace ClrPlus.Scripting.MsBuild {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Collections;
    using Core.Extensions;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Languages.PropertySheetV3.RValue;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;

    internal static class ProjectTargetElementExtensions {
        
        internal static CustomPropertyList EnvironmentList(this ProjectTargetElement target) {
            return null;
        }

        internal static CustomPropertyList Uses(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor DefaultFlag(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor BuildCommand(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor CleanCommand(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor Platform(this ProjectTargetElement target) {
            return null;
        }
        internal static Accessor UsesTool(this ProjectTargetElement target) {
            return null;
        }
        internal static CustomPropertyList ProducesTargets(this ProjectTargetElement target) {
            return null;
        }
        internal static CustomPropertyList GenerateFiles(this ProjectTargetElement target) {
            return null;
        }
        internal static CustomPropertyList RequiresPackages(this ProjectTargetElement target) {
            return null;
        }
        internal static Accessor Condition(this ProjectTargetElement target) {
            return null;
        }
    }

    public class BuildScript : IDisposable {
        public string Filename { get; set; }
        protected PropertySheet _sheet;
        internal IDictionary<string, IValue> productInformation; 
        private Project _project = new Project();
        
        public BuildScript(string filename) {
            _sheet = new PropertySheet(_project);
            _sheet.ParseFile(filename);

            foreach(var target in _sheet.CurrentView.ReplaceableChildren ) {
                target.MapTo(_project.LookupTarget(target),PtkRoutes); 
            }

            _sheet.Route(_project.ProjectRoutes());

            // convert #product-info into a dictionary.
            productInformation = _sheet.Metadata.Value.Keys.Where(each => each.StartsWith("product-info")).ToXDictionary(each => each.Substring(12), each => _sheet.Metadata.Value[each]);
            
            _sheet.CopyToModel();
        }

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
                yield return "CHILDREN".MapChildTo<ProjectTargetElement>((tgt, child) => tgt.GetTargetItem(child));// .tasks 
            }
        }

        public void Dispose() {
            _sheet = null;
        }
    }
}