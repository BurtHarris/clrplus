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

namespace ClrPlus.Scripting.MsBuild.Packaging {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Core.Collections;
    using Core.Extensions;
    using Core.Tasks;
    using Core.Utility;
    using Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Platform;

    public class ProjectPlus : Project, IDisposable {
        private readonly IProjectOwner _owner;
        internal List<string> Conditions = new List<string>();
        internal IDictionary<string, string> Conditions2;
        internal View ConfigurationsView;

        internal StringPropertyList InitialTargets;

        internal Action BeforeSave;

        public ProjectPlus(IProjectOwner owner, string filename) {
            _owner = owner;
            FullPath = Path.Combine(_owner.Directory, filename);

            InitialTargets = new StringPropertyList(() => Xml.InitialTargets, v => Xml.InitialTargets = v, target => LookupTarget(target, null));
        }

        internal string Name {
            get {
                return _owner.ProjectName;
            }
        }

        internal string SafeName {
            get {
                return Name.MakeSafeFileName().Replace(".", "_");
            }
        }

        internal Pivots Pivots {
            get {
                return _owner.Pivots;
            }
        }

        public string Filename {
            get {
                return Path.GetFileName(FullPath);
            }
        }

        public IEnumerable<ToRoute> MemberRoutes {
            get {
                yield return "InitialTargets".MapTo(InitialTargets);

                yield return "ItemDefinitionGroup".MapTo(() => LookupItemDefinitionGroup(""), ItemDefinitionGroupChildren);
                yield return "Target".MapTo(new DelegateDictionary<string, ProjectTargetElement>(
                    () => Targets.Keys,
                    key => LookupTarget(key, ""),
                    (name, value) => LookupTarget(name, ""),
                    key => Targets.Remove(key)
                    ),
                    "CHILDREN".MapChildTo<ProjectTargetElement>((target, child) => target.GetTargetItem(child)));

                /*
                "PropertyGroup".MapTo(),
                "Import".MapTo(),
                "ImportGroup".MapTo(),
                "ItemGroup".MapTo(),
                */
                yield return "condition".MapTo(() => ConditionCreate(), key => Pivots.NormalizeExpression(key), ConditionRoutes());
                yield return "*".MapTo(() => ConditionCreate(), key => Pivots.NormalizeExpression(key), ConditionRoutes());
            }
        }

        private static IEnumerable<ToRoute> ItemDefinitionGroupChildren {
            get {
                yield return "PostBuildEvent".ItemDefinitionRoute();
                //Command
                //Message

                yield return "Midl".ItemDefinitionRoute();
                //TypeLibraryName

                yield return "ResourceCompile".ItemDefinitionRoute();
                //Culture
                //ResourcOutputFileName
                //AdditionalIncludeDirectories
                //PreprocessorDefinitions

                yield return "BcsMake".ItemDefinitionRoute();
                //SuppressStartupBanner
                //OutputFile

                yield return "ClCompile".ItemDefinitionRoute(
                    "PreprocessorDefinitions".MetadataListRoute("%(PreprocessorDefinitions)"),
                    "AdditionalIncludeDirectories".MetadataPathListRoute("%(AdditionalIncludeDirectories)")
                    );

                yield return "Link".ItemDefinitionRoute(
                    "AdditionalDependencies".MetadataPathListRoute("%(AdditionalDependencies)")
                    );
            }
        }

        public void Dispose() {
            if (ProjectCollection.GlobalProjectCollection.LoadedProjects.Contains(this)) {
                ProjectCollection.GlobalProjectCollection.UnloadProject(this);
            }
        }

        public new bool Save() {
            if (Xml.Children.Count > 0) {
                AddConfigurations();
                if (BeforeSave != null) {
                    BeforeSave();
                }

                Event<Trace>.Raise("ProjectPlus.Save", "Saving msbuild project file [{0}].", FullPath);
                FullPath.TryHardToDelete();
                base.Save();
                return true;
            }
            return false;
        }



        public ProjectTargetElement AddContainsTaskDefinition() {
            // add the startup/init tasks (and the task for the Contains function) 
            var task = Xml.AddUsingTask(SafeName + "_Contains", @"$(MSBuildToolsPath)\Microsoft.Build.Tasks.v4.0.dll", null);
            task.TaskFactory = "CodeTaskFactory";
            var pgroup = task.AddParameterGroup();
            pgroup.AddParameter("Text", "false", string.Empty, "System.String");
            pgroup.AddParameter("Library", "false", "true", "System.String");
            pgroup.AddParameter("Value", "false", "true", "System.String");
            pgroup.AddParameter("Result", "true", string.Empty, "System.String");

            var body = task.AddUsingTaskBody(string.Empty, string.Empty);

            // thank you.
            body.XmlElement().Append("Code").InnerText = @"Result = ((Text ?? """").Split(';').Contains(Library) ) ? Value : String.Empty;";

            return AddInitTarget("init");
        }

        public IDictionary<string, string> ConditionCreate() {
#if false
            var dic = parent.Conditions2;
            if (dic == null) {
                Event<Trace>.Raise("", "ConditionCreate");
                var list = parent.Conditions;

                dic = parent.Conditions2 = new DelegateDictionary<string, string>(
                () => list,
                key => key,
                (s, c) => {
                    if(!list.Contains(s)) {
                        list.Add(s);
                    }
                },
                list.Remove,
                list.Clear); 
            }

            return dic;

#else
            var list = Conditions;
            return new DelegateDictionary<string, string>(
                () => list,
                key => key,
                (s, c) => {
                    if (!list.Contains(s)) {
                        list.Add(s);
                    }
                },
                list.Remove,
                list.Clear);
#endif
        }

        internal ProjectItemDefinitionGroupElement LookupItemDefinitionGroup(string condition) {
            // look it up or create it.
            var label = Pivots.GetExpressionLabel(condition);
            if (string.IsNullOrEmpty(condition)) {
                var result = Xml.ItemDefinitionGroups.FirstOrDefault(each => string.IsNullOrEmpty(each.Label));
                if (result != null) {
                    return result;
                }
            } else {
                var result = Xml.ItemDefinitionGroups.FirstOrDefault(each => label == each.Label);
                if (result != null) {
                    return result;
                }
            }

            var idg = Xml.AddItemDefinitionGroup();

            if (!string.IsNullOrEmpty(condition)) {
                idg.Label = label;
                idg.Condition = Pivots.GetMSBuildCondition(Name, condition);
            }
            return idg;
        }

        internal ProjectTargetElement LookupTarget(string name, string condition = null, bool scopeToProject = false ) {
            var originalName = name;

            if (string.IsNullOrEmpty(condition)) {
                switch (name) {
                    case "AfterBuild":
                    case "BeforeBuild":
                    case "Build":
                    case "Rebuild":
                    case "_PrepareForBuild":
                    case "_PrepareForRebuild":
                    case "_PrepareForClean":
                    case "LibLinkOnly":
                    case "AfterBuildCompile":
                    case "AfterBuildGenerateSources":
                        // these are predefined tasks, we'll make our own local one, and ensure we're chained to upstream one.
                        var tgt = LookupTarget(SafeName + "_" + name, null, false);
                        tgt.AfterTargets = tgt.AfterTargets.AppendToSemicolonList(name);
                        return tgt;
                }
            } else {
                if (scopeToProject) {
                    name = SafeName + "_" + name;
                }
            }

            var label = Pivots.GetExpressionLabel(condition);
            name = name.Replace(".", "_");

            if (string.IsNullOrEmpty(condition)) {
                var result = Xml.Targets.FirstOrDefault(each => name == each.Name && string.IsNullOrEmpty(each.Condition));
                if (result != null) {
                    return result;
                }
                return Xml.AddTarget(name);
            }

            var modifiedname = "{0}_{1}".format(name, label).Replace(" ", "_").MakeSafeFileName();

            var conditionedResult = Xml.Targets.FirstOrDefault(each => modifiedname == each.Name);
            if (conditionedResult != null) {
                return conditionedResult;
            }

            // ensure a non-conditioned gets created that we can chain to.
            LookupTarget(originalName, null, false);

            var target = Xml.AddTarget(modifiedname);

            target.Label = label;
            target.Condition = Pivots.GetMSBuildCondition(Name, condition);
            target.AfterTargets = name;

            return target;
        }

        public ProjectTargetElement AddInitTarget(string name) {
            var tgt = LookupTarget(name,null, true);
            
            InitialTargets.Add(tgt.Name);
            return tgt;
        }

        internal IEnumerable<ToRoute> ConditionRoutes() {
            yield return "ItemDefinitionGroup".MapTo<string>(condition => LookupItemDefinitionGroup(condition), ItemDefinitionGroupChildren);
            yield return "Target".MapTo<string, string, ProjectTargetElement>(condition => new DelegateDictionary<string, ProjectTargetElement>(
                () => Targets.Keys,
                key => LookupTarget(key, condition),
                (name, value) => LookupTarget(name, ""),
                key => Targets.Remove(key)
                ), "CHILDREN".MapChildTo<ProjectTargetElement>((target, child) => target.GetTargetItem(child)));
        }

        public void AddConfigurations() {
            // generate init target
            var initTarget = AddContainsTaskDefinition();

            // add property value initialization steps
            GenerateConfigurationPropertyInitializers(initTarget);
        }

        private void GenerateConfigurationPropertyInitializers(ProjectTargetElement initTarget) {
            var pivots = Pivots;
            ProjectPropertyGroupElement pg = null;
            
            foreach (var pivot in pivots.Values) {
                // dynamic cfg = configurationsView.GetProperty(pivot);
                IEnumerable<string> choices = pivot.Choices.Keys;

                if (string.IsNullOrEmpty(pivot.Key)) {
                    // add init steps for this.
                    var finalPropName = "{0}-{1}".format(pivot.Name, SafeName);

                    foreach (var choice in choices.Distinct()) {
                        var choicePropName = "{0}-{1}".format(pivot.Name, choice);

                        var tsk = initTarget.AddTask(SafeName + "_Contains");
                        tsk.SetParameter("Text", choicePropName);
                        tsk.SetParameter("Library", SafeName);
                        tsk.SetParameter("Value", choice);
                        tsk.Condition = @"'$({0})'==''".format(finalPropName);
                        tsk.AddOutputProperty("Result", finalPropName);
                    }
                    pg = pg ?? Xml.AddPropertyGroup();
                    pg.Label = "Default initializers for properties";
                    pg.AddProperty(finalPropName, choices.FirstOrDefault()).Condition = @"'$({0})' == ''".format(finalPropName);
                }
            }
        }

        internal ProjectPropertyGroupElement AddPropertyInitializer(string propertyName, string conditionExpression, string value,ProjectPropertyGroupElement ppge = null) {
            ppge = ppge ?? Xml.AddPropertyGroup();
            ppge.Label = "Additional property initializers";
            ppge.AddProperty(propertyName, value).Condition = conditionExpression;
            return ppge;
        }
    }
}