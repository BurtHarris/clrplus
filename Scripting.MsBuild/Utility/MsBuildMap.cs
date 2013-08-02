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

namespace ClrPlus.Scripting.MsBuild.Utility {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Collections;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Xml;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Core.Utility;
    using ClrPlus.Scripting.Languages.PropertySheetV3.Mapping;
    using Core.Tasks;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Framework;

    internal class MSBuildTaskType {
        public Type TaskClass;
        public string[] OptionalInputs;
        public string[] RequiredInputs;
        public string[] Outputs;
    }

    public static class MsBuildMap {
        internal static XDictionary<object,StringPropertyList>  _stringPropertyList = new XDictionary<object, StringPropertyList>();

        // private static MSBuildTaskUtility _taskUtility;

        
        private static string[] _ignoreProperties = new string[] {
            "BuildEngine",
            "BuildEngine2",
            "BuildEngine3",
            "BuildEngine4",
            "HostObject",
            "Log"
        };

        private static AsyncLazy<Dictionary<string, MSBuildTaskType>> TaskClasses;

        static MsBuildMap() {
            TaskClasses = new AsyncLazy<Dictionary<string, MSBuildTaskType>>(() => {
                var _taskClasses = new Dictionary<string, MSBuildTaskType>();

               // ensure a few assemblies are loaded.
                AppDomain.CurrentDomain.Load(new AssemblyName("Microsoft.Build.Tasks.v4.0, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
                AppDomain.CurrentDomain.Load(new AssemblyName("Microsoft.Build.Utilities.v4.0, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
                AppDomain.CurrentDomain.Load(new AssemblyName("Microsoft.Build.Framework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var asm in assemblies) {
                    var tasks = asm.GetTypes().Where(each => each.GetInterfaces().Contains(typeof (ITask))).Where(each => each.IsPublic);
                    foreach (var t in tasks) {
                        var properties = t.GetProperties().Where(each => !_ignoreProperties.Contains(each.Name)).ToArray();
                        if (!_taskClasses.Keys.Contains(t.Name)) {
                            _taskClasses.Add(t.Name.ToLower(), new MSBuildTaskType {
                                TaskClass = t,
                                Outputs = properties.Where(each => each.GetCustomAttributes(true).Any(attr => attr.GetType().Name == "OutputAttribute")).Select(each => each.Name).ToArray(),
                                RequiredInputs = properties.Where(each => each.GetCustomAttributes(true).Any(attr => attr.GetType().Name == "RequiredAttribute")).Select(each => each.Name).ToArray(),
                                OptionalInputs = properties.Where(each => each.GetCustomAttributes(true).All(attr => attr.GetType().Name != "OutputAttribute" && attr.GetType().Name != "RequiredAttribute")).Select(each => each.Name).ToArray()
                            });
                        }
                        
                    }
                }
                return _taskClasses;
            });
        }


        internal static ProjectElement GetTargetItem(this ProjectTargetElement target, View view) {
            // get the member name and data from the view, and create/lookup the item.
            // return the item.
            switch (view.MemberName) {
                case "PropertyGroup":
                    break;
                case "ItemGroup":
                    break;
                case "AfterTargets":
                    break;

                default:
                    // 
                    
                    var taskName = view.MemberName;
                    if (TaskClasses.Value.ContainsKey(taskName.ToLower())) {
                        // for tasks we recognize
                        var tskType = TaskClasses.Value[taskName.ToLower()];

                        var tsk = target.AddTask(taskName);
                        var required = tskType.RequiredInputs.ToList();


                        foreach (var n in view.GetChildPropertyNames()) {
                            var prop = view.GetProperty(n);

                            if (n == "Condition") {
                                tsk.Condition = prop;
                                continue;
                            }

                            if (required.Contains(n)) {
                                required.Remove(n);
                            } else {
                                if (!tskType.OptionalInputs.Contains(n)) {
                                    Event<Warning>.Raise("Unknown Parameter", "Task '{0}' does not appear to have an input parameter '{1}'", taskName, n);

                                    // could we set some item collection based on these?
                                    // TODO: maybe.
                                }
                            }

                            tsk.SetParameter(n, prop.Values.CollapseToString(";"));
                        }

                        foreach (var r in required) {
                            Event<Warning>.Raise("Missing Parameter", "Task '{0}' is missing required input parameter '{1}'", taskName, r);
                        }

                        var outputs = tskType.Outputs.ToList();

                        foreach (var n in view.GetIndexedPropertyNames()) {
                            var prop = view.GetProperty(n);
                            // an output paramter.
                            var nam = prop.GetProperty(prop.GetChildPropertyNames().FirstOrDefault());

                            if (!tskType.Outputs.Contains(nam.Value)) {
                                Event<Warning>.Raise("Unknown Parameter", "Task '{0}' does not appear to have an output parameter '{1}'", taskName, nam.Value);
                            }

                            if (outputs.Contains(nam.Value)) {
                                outputs.Remove(nam.Value);
                            }

                            tsk.AddOutputProperty(nam.Value, nam.MemberName);
                            tsk.AddOutputItem(nam.Value, nam.MemberName);
                        }

                        foreach (var output in outputs) {
                            // add in any unreferenced outputs as themselves.
                            tsk.AddOutputProperty(output, output);
                            tsk.AddOutputItem(output, output);
                        }
                        return tsk;
                    }


                    // for tasks we don't recognize
                    var tsk2 = target.AddTask(taskName);

                    Event<Warning>.Raise("Unrecognized Task", "Task '{0}' is not recognized.", taskName);

                    foreach (var n in view.GetChildPropertyNames()) {
                        var prop = view.GetProperty(n);
                        if(n == "Condition") {
                            tsk2.Condition = prop;
                            continue;
                        }
                        tsk2.SetParameter(n, prop.Values.CollapseToString(";"));
                    }

                    foreach (var n in view.GetIndexedPropertyNames()) {
                        var prop = view.GetProperty(n);
                        // an output paramter.
                        var nam = prop.GetProperty(prop.GetChildPropertyNames().FirstOrDefault());
                        tsk2.AddOutputProperty(nam.Value, nam.MemberName);
                        tsk2.AddOutputItem(nam.Value, nam.MemberName);
                    }
                    return tsk2;

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

        internal static ToRoute IntMetadataRoute(this string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => {
                foreach(var m in pide.Metadata) {
                    var metadata = m;
                    if(metadata.Name == metadataName) {
                        return new Accessor(() => metadata.Value.ToInt32(), (v) => metadata.Value = v.ToString().ToInt32().ToString());
                    }
                }
                var n = pide.AddMetadata(metadataName, defaultValue ?? "");
                return new Accessor(() => n.Value.ToInt32(), (v) => n.Value = v.ToString().ToInt32().ToString());
            });
        }
        internal static ToRoute BoolMetadataRoute(this string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => {
                foreach(var m in pide.Metadata) {
                    var metadata = m;
                    if(metadata.Name == metadataName) {
                        return new Accessor(() => metadata.Value.IsPositive(), (v) => metadata.Value = v.ToString().IsPositive().ToString());
                    }
                }
                var n = pide.AddMetadata(metadataName, defaultValue ?? "");
                return new Accessor(() => n.Value.IsPositive(), (v) => n.Value = v.ToString().IsPositive().ToString());
            });
        }

        internal static ToRoute PathMetadataRoute(this string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => {
                foreach(var m in pide.Metadata) {
                    var metadata = m;
                    if(metadata.Name == metadataName) {
                        return new Accessor(() => metadata.Value, (v) => metadata.Value = v.ToString().Replace(@"\\",@"\"));
                    }
                }
                var n = pide.AddMetadata(metadataName, defaultValue ?? "");
                return new Accessor(() => n.Value, (v) => n.Value = v.ToString().Replace(@"\\", @"\"));
            });
        }
        
        internal static ToRoute MetadataListRoute(this string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => pide.LookupMetadataList(metadataName, defaultValue));
        }
        internal static ToRoute MetadataPathListRoute(this string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => pide.LookupMetadataPathList(metadataName, defaultValue));
        }

        internal static ToRoute MapFolder(this string name) {
            return PathMetadataRoute(name);
        }
        internal static ToRoute MapFolderList(this string name) {
            return name.MapTo<ProjectItemDefinitionElement>(pide => pide.LookupMetadataPathList(name, "%({0})".format(name)));

        }
        internal static ToRoute MapFile(this string name) {
            return PathMetadataRoute(name);
        }
        internal static ToRoute MapFileList(this string name) {
            return name.MapTo<ProjectItemDefinitionElement>(pide => pide.LookupMetadataPathList(name, "%({0})".format(name)));
        }
        internal static ToRoute MapString(this string name) {
            return MetadataRoute(name);
        }
        internal static ToRoute MapStringList(this string name) {
            return MetadataListRoute(name, "%({0})".format(name));
        }
        internal static ToRoute MapBoolean(this string name) {
            return BoolMetadataRoute(name);
        }
        internal static ToRoute MapInt(this string name) {
            return IntMetadataRoute(name);
        }

        internal static ToRoute MapEnum(this string name, params string[] values) {
            return name.MapTo<ProjectItemDefinitionElement>(pide => {
                foreach(var m in pide.Metadata) {
                    var metadata = m;
                    if(metadata.Name == name) {
                        return new Accessor(() => metadata.Value, (v) => {
                            string val = v.ToString();
                            if (val != null) {
                                // allow them to set it to a propert value...
                                if (val.IndexOf("$(") > -1 || values.Contains(val)) {
                                    metadata.Value = val;
                                }
                            }
                        });
                    }
                }
                var n = pide.AddMetadata(name, "");
                return new Accessor(() => n.Value, (v) => {
                    string val = v.ToString();
                    if (val.IndexOf("$(") > -1 || values.Contains(val)) {
                        n.Value = val;
                    }
                });
            });
        }

        internal static ToRoute ItemDefinitionRoute(this string name, IEnumerable<ToRoute> children = null) {
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

