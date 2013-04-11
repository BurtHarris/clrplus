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

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Scripting.MsBuild {
    using System;
    using System.Collections;
    using System.Text.RegularExpressions;
    using System.Xml;
    using CSScriptLibrary;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Tasks;
    using Core.Utility;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Packaging;
    using Platform;

    

    public static class MsBuildMap {
        internal static XDictionary<object,StringPropertyList>  _stringPropertyList = new XDictionary<object, StringPropertyList>();

      



        internal static ProjectElement GetTargetItem(this ProjectTargetElement target, View view) {
            // get the member name and data from the view, and create/lookup the item.
            // return the item.
            switch (view.MemberName) {
                case "PropertyGroup":
                    break;
                case "ItemGroup":
                    break;
                default:
                    var tsk = target.AddTask(view.MemberName);

                    foreach (var n in view.GetChildPropertyNames()) {
                        tsk.SetParameter(n, view.GetProperty(n));
                    }
                    return tsk;
            }
            return null;
        }

       
        public static XmlElement XmlElement(this ProjectElement projectElement) {
            return projectElement.AccessPrivate().XmlElement;
        }


        internal static ToRoute MetadataRoute(this string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => {
                foreach (var m in pide.Metadata) {
                    var metadata = m;
                    if (metadata.Name == metadataName) {
                        return new Accessor(() => metadata.Value, (v) => metadata.Value = v.ToString());
                    }
                }
                var n = pide.AddMetadata(metadataName, defaultValue ?? "");
                return new Accessor(() => n.Value, (v) => n.Value = v.ToString());
            });
        }

        internal static ToRoute MetadataListRoute(this string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => pide.LookupMetadataList(metadataName, defaultValue));
        }
        internal static ToRoute MetadataPathListRoute(this string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => pide.LookupMetadataPathList(metadataName, defaultValue));
        }

        internal static ToRoute ItemDefinitionRoute(this string name, params ToRoute[] children) {
            return name.MapTo<ProjectItemDefinitionGroupElement>(pidge => pidge.LookupItemDefinitionElement(name), children);
        }

        internal static IList GetTaskList(this ProjectTargetElement target) {
            // get the member name and data from the view, and create/lookup the item.
            // return the item.
            return null;
        }
       

     

        internal static ProjectItemDefinitionElement LookupItemDefinitionElement(this ProjectItemDefinitionGroupElement pidge, string itemType) {
            return pidge.Children.OfType<ProjectItemDefinitionElement>().FirstOrDefault( each => each.ItemType == itemType) ?? pidge.AddItemDefinition(itemType);
        }

      

        internal static StringPropertyList LookupMetadataList(this ProjectItemDefinitionElement pide, string metadataName, string defaultValue = null) {
            foreach (var m in pide.Metadata.Where(metadata => metadata.Name == metadataName)) {
                var metadata = m;
                return _stringPropertyList.GetOrAdd(metadata, () => _stringPropertyList.AddOrSet(metadata, new StringPropertyList(() => metadata.Value, v => metadata.Value = v)));
            }
            var n = pide.AddMetadata(metadataName, defaultValue ?? "");
            return _stringPropertyList.GetOrAdd(n, () => _stringPropertyList.AddOrSet(n, new StringPropertyList(() => n.Value, v => n.Value = v)));
        }

    
        internal static StringPropertyList LookupMetadataPathList(this ProjectItemDefinitionElement pide, string metadataName, string defaultValue = null) {
            foreach(var m in pide.Metadata.Where(metadata => metadata.Name == metadataName)) {
                var metadata = m;
                return _stringPropertyList.GetOrAdd(metadata, () => _stringPropertyList.AddOrSet(metadata, new UniquePathPropertyList(() => metadata.Value, v => metadata.Value = v)));
            }
            var n = pide.AddMetadata(metadataName, defaultValue ?? "");
            return _stringPropertyList.GetOrAdd(n, () => _stringPropertyList.AddOrSet(n, new UniquePathPropertyList(() => n.Value, v => n.Value = v)));
        }
#if moved 
          public static void AddConfigurations(this ProjectPlus project, string projectName) {
            // generate init target
            var initTarget = project.AddContainsTaskDefinition(projectName);

            // add property value initialization steps
            project.GenerateConfigurationPropertyInitializers(projectName, initTarget);
        }

        private static void GenerateConfigurationPropertyInitializers(this ProjectPlus project, string pkgName, ProjectTargetElement initTarget) {
            var pivots = project.Pivots;

            
            foreach (var pivot in pivots.Values) {
                // dynamic cfg = configurationsView.GetProperty(pivot);
                IEnumerable<string> choices = pivot.Choices.Keys;

                if (string.IsNullOrEmpty(pivot.Key) ) {
                    // add init steps for this.
                    var finalPropName = "{0}-{1}".format(pivot, pkgName);

                    foreach (var choice in choices.Distinct()) {
                        var choicePropName = "{0}-{1}".format(pivot, choice);

                        var tsk = initTarget.AddTask(pkgName + "_Contains");
                        tsk.SetParameter("Text", choicePropName);
                        tsk.SetParameter("Library", pkgName);
                        tsk.SetParameter("Value", choice);
                        tsk.Condition = @"'$({0})'==''".format(finalPropName);
                        tsk.AddOutputProperty("Result", finalPropName);
                    }

                    project.Xml.AddPropertyGroup().AddProperty(finalPropName, choices.FirstOrDefault()).Condition = @"'$({0})' == ''".format(finalPropName);
                }
            }
        }



    /*    public static void MapProject(this RootPropertySheet propertySheet, string location, ProjectPlus project ) {
            propertySheet.AddChildRoutes(location.MapTo(() => project, ProjectRoutes(project).ToArray()));
        }
        */

         public static ProjectTargetElement AddContainsTaskDefinition(this ProjectPlus project, string projectName) {
            // add the startup/init tasks (and the task for the Contains function) 
            var task = project.Xml.AddUsingTask(projectName + "_Contains", @"$(MSBuildToolsPath)\Microsoft.Build.Tasks.v4.0.dll", null);
            task.TaskFactory = "CodeTaskFactory";
            var pgroup = task.AddParameterGroup();
            pgroup.AddParameter("Text", "false", string.Empty, "System.String");
            pgroup.AddParameter("Library", "false", "true", "System.String");
            pgroup.AddParameter("Value", "false", "true", "System.String");
            pgroup.AddParameter("Result", "true", string.Empty, "System.String");

            var body = task.AddUsingTaskBody(string.Empty, string.Empty);

            // thank you.
            body.XmlElement().Append("Code").InnerText = @"Result = ((Text ?? """").Split(';').Contains(Library) ) ? Value : String.Empty;";

            return project.AddInitTarget(projectName + "_init");
        }


         internal static IEnumerable<ToRoute>  ProjectRoutes(this ProjectPlus project) {
            
            yield return "InitialTargets".MapTo((IList)project.InitialTargets);

            yield return "ItemDefinitionGroup".MapTo(() => LookupItemDefinitionGroup(project, ""), ItemDefinitionGroupChildren );
            yield return "Target".MapTo(new DelegateDictionary<string, ProjectTargetElement>(
                () => project.Targets.Keys,
                key => LookupTarget(project, key, ""),
                (name, value) => LookupTarget(project, name, ""),
                key => project.Targets.Remove(key)
                ),
                "CHILDREN".MapChildTo<ProjectTargetElement>((target, child) => GetTargetItem(target, child)));
                   
                /*
                "PropertyGroup".MapTo(),
                "Import".MapTo(),
                "ImportGroup".MapTo(),
                "ItemGroup".MapTo(),
                */
            yield return "condition".MapTo(() => project.ConditionCreate(), key => project.Pivots.NormalizeExpression(key), project.ConditionRoutes());
            yield return "*".MapTo(() => project.ConditionCreate(), key => project.Pivots.NormalizeExpression(key), project.ConditionRoutes());
        }


           public static ProjectTargetElement AddInitTarget(this ProjectPlus project, string name) {
            project.InitialTargets.Add(name);
            return LookupTarget(project, name);
        }

         internal static IEnumerable<ToRoute> ConditionRoutes(this ProjectPlus project) {
            yield return "ItemDefinitionGroup".MapTo<string>(condition => LookupItemDefinitionGroup(project, condition), ItemDefinitionGroupChildren);
            yield return "Target".MapTo<string, string, ProjectTargetElement>(condition => new DelegateDictionary<string, ProjectTargetElement>(
                () => project.Targets.Keys,
                key => LookupTarget(project, key, condition),
                (name, value) => LookupTarget(project, name, ""),
                key => project.Targets.Remove(key)
                ), "CHILDREN".MapChildTo<ProjectTargetElement>((target, child) => GetTargetItem(target, child)));

        }

        internal static ProjectItemDefinitionGroupElement LookupItemDefinitionGroup(this ProjectPlus p, string condition) {
            // look it up or create it.
            var label = p.Pivots.GetExpressionLabel(condition);
            if(string.IsNullOrEmpty(condition)) {
                var result = p.Xml.ItemDefinitionGroups.FirstOrDefault(each => string.IsNullOrEmpty(each.Label));
                if(result != null) {
                    return result;
                }
            }
            else {

                var result = p.Xml.ItemDefinitionGroups.FirstOrDefault(each => label == each.Label);
                if(result != null) {
                    return result;
                }
            }

            var idg = p.Xml.AddItemDefinitionGroup();

            if(!string.IsNullOrEmpty(condition)) {
                idg.Label = label;
                idg.Condition = p.Pivots.GetMSBuildCondition(p.Name, condition);
            }
            return idg;
        }

         internal static ProjectTargetElement LookupTarget(this ProjectPlus p, string name,  string condition = null) {
            var label = p.Pivots.GetExpressionLabel(condition);

            if (string.IsNullOrEmpty(condition)) {
                var result = p.Xml.Targets.FirstOrDefault(each => name == each.Name && string.IsNullOrEmpty(each.Condition));
                if (result != null) {
                    return result;
                }
                return p.Xml.AddTarget(name);
            }

            var modifiedname = "{0}_{1}".format(name, label).Replace(" ","_").MakeSafeFileName();

            var conditionedResult = p.Xml.Targets.FirstOrDefault(each => modifiedname  == each.Name );
            if(conditionedResult != null) {
                return conditionedResult;
            }

            // ensure a non-conditioned gets created that we can chain to.
            LookupTarget(p, name, null);

            var target = p.Xml.AddTarget(modifiedname );
            

            target.Label = label;
            target.Condition = p.Pivots.GetMSBuildCondition(p.Name, condition);
            target.AfterTargets = name;
            
            
            return target;
        }
#endif

        internal static string AppendToSemicolonList(this string list, string item) {
            if (string.IsNullOrEmpty(list)) {
                return item;
            }
            return list.Split(';').UnionSingleItem(item).Aggregate((current, each) => current + ";" + each).Trim(';');
        }
        private static IEnumerable<ToRoute> TargetChildren() {
            yield break;
        }
    }
}



#if false
        
         public static IDictionary<string, string> ConditionCreate(this ProjectPlus parent) {

            
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
            var list = parent.Conditions;
            return new DelegateDictionary<string, string>(
                () => list,
                key => key,
                (s, c) => {
                    if(!list.Contains(s)) {
                        list.Add(s);
                    }
                },
                list.Remove,
                list.Clear);
#endif
        }

        internal static IEnumerable<ToRoute> ProjectRoutes2(Pivots pivots) {

            yield return "InitialTargets".MapTo<ProjectPlus>(project => (IList)project.InitialTargets);

            yield return "ItemDefinitionGroup".MapTo<ProjectPlus>(project => LookupItemDefinitionGroup(project, ""), ItemDefinitionGroupChildren);
            yield return "Target".MapTo<ProjectPlus>( project => new DelegateDictionary<string, ProjectTargetElement>(
                () => project.Targets.Keys,
                key => LookupTarget(project, key, ""),
                (name, value) => LookupTarget(project, name, ""),
                key => project.Targets.Remove(key)
                ),
                "CHILDREN".MapChildTo<ProjectTargetElement>((target, child) => GetTargetItem(target, child)));

            /*
            "PropertyGroup".MapTo(),
            "Import".MapTo(),
            "ImportGroup".MapTo(),
            "ItemGroup".MapTo(),
            */
            yield return "condition".MapTo<ProjectPlus>(project => ConditionCreate(project), key => pivots.NormalizeExpression(key), ConditionRoutes2());
            yield return "*".MapTo<ProjectPlus>(project => ConditionCreate(project), key => pivots.NormalizeExpression(key), ConditionRoutes2());
        }

        internal static IEnumerable<ToRoute> ConditionRoutes2() {
            yield return "ItemDefinitionGroup".MapTo<Condition>(condition => LookupItemDefinitionGroup(condition.Project, condition.Parameter), ItemDefinitionGroupChildren);
            yield return "Target".MapTo<string, string, ProjectTargetElement>(condition => new DelegateDictionary<string, ProjectTargetElement>(
                () => project.Targets.Keys,
                key => LookupTarget(project, key, condition),
                (name, value) => LookupTarget(project, name, ""),
                key => project.Targets.Remove(key)
                ), "CHILDREN".MapChildTo<ProjectTargetElement>((target, child) => GetTargetItem(target, child)));

        }
#endif