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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Scripting.MsBuild {
    using System.Collections;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;

    public class Configurations {
       
        public static void Add(View view, Project project) {
            if (_projects.ContainsKey(project)) {
                throw new ClrPlusException("Configurations already registered for project");
            }
            _projects.Add(project, view);
        }
        

        private static XDictionary<Project, View> _projects = new XDictionary<Project, View>();

        public static string NormalizeConditionKey(Project project, string key) {
            if (!_projects.ContainsKey(project)) {
                return key;
            }
            var view = _projects[project];
            var pivots = view.PropertyNames;
            
            var options = key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {
                '\\', ' '
            }, StringSplitOptions.RemoveEmptyEntries).ToList();
            
            var ordered = new List<string>();


            foreach (var pivot in pivots) {
                foreach(var option in options) {
                    dynamic cfg = view.GetProperty(pivot);
                    if (cfg.choices.Values.Contains(option)) {
                        ordered.Add(option);
                        options.Remove(option);
                        break;
                    }
                }
                
            }
            if(options.Any()) {
                throw new ClrPlusException("Unknown configuration choice: {0}".format(options.FirstOrDefault()));
            }


            return ordered.Aggregate((c, e) => c + "\\" + e).Trim('\\');
        }

        public static string GenerateCondition(Project project, string key) {
            var view = _projects[project];
            var pivots = view.PropertyNames;


            var options = key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {
                '\\', ' '
            }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var conditions = new List<string>();

            foreach(var pivot in pivots) {
                foreach(var option in options) {
                    dynamic cfg = view.GetProperty(pivot);
                    IEnumerable<string> choices = cfg.choices;
                    if(choices.Contains(option)) {
                        if (((View)cfg).PropertyNames.Contains("key")) {
                            // this is a standard property, us
                            conditions.Add("'$({0})' == '{1}'".format((string)cfg.Key, option));
                        } else {
                            conditions.Add("'$({0}-{1})' == '{2}'".format((string)pivot, view.ResolveMacrosInContext("${pkgname}"), option));
                        }

                        options.Remove(option);
                        break;
                    }
                }

            }

            if (options.Any()) {
                throw new ClrPlusException("Unknown configuration choice: {0}".format(options.FirstOrDefault()));
            }

            return conditions.Aggregate((current, each) => (string.IsNullOrEmpty(current) ? current : (current + " and ")) + each);
        }
    }

    public class Condition {
        internal string Parameter;
        internal Project Project;

        private static XDictionary<Project, List<string> > _projects = new XDictionary<Project,List<string>>();

        public static IDictionary<string, string> Create(Project parent) {
            if (!_projects.ContainsKey(parent)) {
                _projects.Add(parent, new List<string>());
            }

            var list = _projects[parent];

            return new DelegateDictionary<string, string>(
                () => list,
                key =>  key ,
                (s, c) => {
                    if (!list.Contains(s)) {
                        list.Add(s);
                    }
                },
                list.Remove,
                list.Clear);
        }

        public static string NormalizeConfigurationKey(string key) {
            return key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {
                '\\', ' '
            }, StringSplitOptions.RemoveEmptyEntries).OrderBy(each => each).Aggregate((c, e) => c + "\\" + e).Trim('\\');
        }
    }

    public static class MsBuildMap {

        public static DictionaryRoute<Tp, Tk, Tv> XYZ<Tp, Tk, Tv>( DictionaryRoute<Tp, Tk, Tv> aRoute ) {
            DictionaryRoute<Tp, Tk, Tv> route;
            route = 
                parent => {
                    var result = aRoute(parent);
                    route = (p) => result;
                    return result;
                };



            return route;

        }

        public static void MapProject(this PropertySheet propertySheet, string location, Project project ) {
            propertySheet.Route(
                location.MapTo(() => project,

                  "ItemDefinitionGroup".MapTo(() => LookupItemDefinitionGroup(project, ""), ItemDefinitionGroupChildren().ToArray()),
                  "Target".MapTo(new DelegateDictionary<string,ProjectTargetElement>(
                      () => project.Targets.Keys,
                      key => LookupTarget( project, key, "" ),
                      (name, value) => LookupTarget( project, name, "" ),
                      key => project.Targets.Remove(key )
                      ),"0".MapTo<ProjectTargetElement>( target => target.  ))
                   ,
                  
                  
                /*
                "PropertyGroup".MapTo(),
                "Import".MapTo(),
                "ImportGroup".MapTo(),
                "ItemGroup".MapTo(),
                */
                    "condition".MapTo(() => Condition.Create(project), key => Configurations.NormalizeConditionKey(project,key) ,

                        "Target".MapTo<string, string, ProjectTargetElement>(condition => new DelegateDictionary<string, ProjectTargetElement>(
                              () => project.Targets.Keys,
                              key => LookupTarget(project, key, condition),
                              (name, value) => LookupTarget(project, name, ""),
                              key => project.Targets.Remove(key)
                              )),



                        "ItemDefinitionGroup".MapTo<string>(condition => LookupItemDefinitionGroup(project, condition), ItemDefinitionGroupChildren().ToArray())

                        ))

                );
        }

        public static void MapConfigurations(this PropertySheet propertySheet, string location, Project project) {
            Configurations.Add((propertySheet.View as View).GetProperty(location), project);
        }

        private static IEnumerable<ToRoute> ItemDefinitionGroupChildren() {
            yield return ItemDefinitionRoute("PostBuildEvent");
            //Command
            //Message

            yield return ItemDefinitionRoute("Midl");
            //TypeLibraryName

            yield return ItemDefinitionRoute("ResourceCompile");
            //Culture
            //ResourcOutputFileName
            //AdditionalIncludeDirectories
            //PreprocessorDefinitions

            yield return ItemDefinitionRoute("BcsMake");
            //SuppressStartupBanner
            //OutputFile

            yield return ItemDefinitionRoute("ClCompile",
                MetadataListRoute("PreprocessorDefinitions", "%(PreprocessorDefinitions)"),
                MetadataListRoute("AdditionalIncludeDirectories", "%(AdditionalIncludeDirectories)")


                );

            yield return ItemDefinitionRoute("Link",
                MetadataListRoute("AdditionalDependencies", "%(AdditionalDependencies)")


                );
        }

        private static ToRoute ItemDefinitionRoute(string name, params ToRoute[] children) {
            return name.MapTo<ProjectItemDefinitionGroupElement>(idg => {
                foreach(var i in idg.Children) {
                    var pide = (i as ProjectItemDefinitionElement);
                    if(pide != null) {
                        if(pide.ItemType == name) {
                            return pide;
                        }
                    }
                }

                var c = idg.AddItemDefinition(name);
                return c;
            }, children);
        }

        private static ToRoute MetadataRoute(string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => {
                foreach(var m in pide.Metadata) {
                    var metadata = m;
                    if(metadata.Name == metadataName) {
                        return new Accessor(() => metadata.Value, (v) => metadata.Value = v.ToString());
                    }
                }
                var n = pide.AddMetadata(metadataName, defaultValue ?? "");
                return new Accessor(() => n.Value, (v) => n.Value = v.ToString());
            });
        }

        private static ToRoute MetadataListRoute(string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => {
                foreach(var m in pide.Metadata) {
                    var metadata = m;
                    if(metadata.Name == metadataName) {
                        return (IList)new StringPropertyList(() => metadata.Value, v => metadata.Value = v);
                    }
                }
                var n = pide.AddMetadata(metadataName, defaultValue ?? "");
                return (IList)new StringPropertyList(() => n.Value, v => n.Value = v);
            });
        }

        private static ProjectItemDefinitionGroupElement LookupItemDefinitionGroup(Project p, string condition) {
            // look it up or create it.
            if(string.IsNullOrEmpty(condition)) {
                var result = p.Xml.ItemDefinitionGroups.FirstOrDefault(each => string.IsNullOrEmpty(each.Label));
                if(result != null) {
                    return result;
                }
            }
            else {
                var result = p.Xml.ItemDefinitionGroups.FirstOrDefault(each => condition == each.Label);
                if(result != null) {
                    return result;
                }
            }

            var idg = p.Xml.AddItemDefinitionGroup();

            if(!string.IsNullOrEmpty(condition)) {
                idg.Label = condition;
                idg.Condition = Configurations.GenerateCondition(p,condition);
            }

            return idg;
        }


        private static ProjectTargetElement LookupTarget(Project p, string name,  string condition) {
            if (string.IsNullOrEmpty(condition)) {
                var result = p.Xml.Targets.FirstOrDefault(each => name == each.Name && string.IsNullOrEmpty(each.Condition));
                if (result != null) {
                    return result;
                }
            } else {
                var result = p.Xml.Targets.FirstOrDefault(each => name == each.Name && each.Condition == condition);
                if(result != null) {
                    return result;
                }
            }
                 

            var target = p.Xml.AddTarget(name);
            
            if(!string.IsNullOrEmpty(condition)) {
                target.Label = condition;
                target.Condition = Configurations.GenerateCondition(p, condition);
            }
            return target;
        }

        private static IEnumerable<ToRoute> TargetChildren() {
            yield break;
        }
    }
}
