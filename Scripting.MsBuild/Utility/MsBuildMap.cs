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
    using System.Collections.Generic;
    using System.Linq;
    using System.Collections;
    using System.Xml;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Core.Utility;
    using ClrPlus.Scripting.Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Construction;

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
                            if(values.Contains(val)) {
                                metadata.Value = val;
                            }
                        });
                    }
                }
                var n = pide.AddMetadata(name, "");
                return new Accessor(() => n.Value, (v) => {
                    string val = v.ToString();
                    if (values.Contains(val)) {
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

