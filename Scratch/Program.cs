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

namespace Scratch {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using ClrPlus.Core.Collections;
    using ClrPlus.Powershell.Core;
    using ClrPlus.Scripting.Languages.PropertySheetV3;
    using ClrPlus.Scripting.Languages.PropertySheetV3.Mapping;
    
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;

    public class NuspecFields {

    };

    public class NuspecFiles {

    };

    public class Case {
        internal string Parameter;
        internal Project Project;

        private static XDictionary<string, Case> _cases = new XDictionary<string, Case>();

        public static IDictionary<string, Case> Create(Project parent) {

            return new DelegateDictionary<string, Case>(
                () => _cases.Keys,

                key => _cases.ContainsKey(key) ? _cases[key] : (_cases[key] = new Case {
                    Parameter = key,
                    Project = parent
                }),
                (s, c) => _cases[s] = new Case { Parameter = s, Project = parent },
                _cases.Remove);
        }
    }

    public class MList : ObservableList<string> {
        public MList(Func<string> getter, Action<string> setter) {
            var initial = getter();
            if (!string.IsNullOrEmpty(initial)) {
                foreach (var i in initial.Split(';')) {
                    Add(i);    
                }
            }

            ListChanged += (source, args) => setter(this.Reverse().Aggregate((current, each) => current + ";" + each));
        }
    }

    public class PackageScript  {
        private NuspecFields _fields = new NuspecFields();
        private NuspecFiles _files = new NuspecFiles();
        private Project _targets = new Project();
        private Project _props = new Project();

        public string Filename { get; set; }
        protected PropertySheet _sheet;

        public void Save() {
            _sheet.SaveFile(Filename);
        }

        public PackageScript(string filename) {
            
            _sheet = new PropertySheet(this);
            _sheet.ParseFile(filename);

            _sheet.Route("nuget.nuspec".MapTo(() => _fields));
            _sheet.Route("nuget.files".MapTo(() => _files ));
            
            MapProject("nuget.props", _props);
            MapProject("nuget.targets", _targets);

            _sheet.View.CopyToModel();
        }

        private void MapProject(string location, Project project) {
            _sheet.Route(
                location.MapTo(() => project,

                    "ItemDefinitionGroup".MapTo(() => FindOrCreateIDG(project, ""), ItemDefinitionGroupChildren().ToArray()),
                    /*
                    "PropertyGroup".MapTo(),
                    "Import".MapTo(),
                    "ImportGroup".MapTo(),
                    "ItemGroup".MapTo(),
                    "
                     
                     */
                    "case".MapTo<Project, string, Case>(parent => Case.Create(parent), key => key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {'\\', ' '}, StringSplitOptions.RemoveEmptyEntries).OrderBy(each => each).Aggregate((c, e) => c + "\\" + e).Trim('\\'),

                        "ItemDefinitionGroup".MapTo<Case>(c => FindOrCreateIDG(c.Project, c.Parameter), ItemDefinitionGroupChildren().ToArray())

                        ))

                );
        }


        private IEnumerable<ToRoute> ItemDefinitionGroupChildren() {
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

            yield return ItemDefinitionRoute("ResourceCompile");


            yield return ItemDefinitionRoute("ClCompile", 
                MetadataListRoute("PreprocessorDefinitions", "%(PreprocessorDefinitions)"),
                MetadataListRoute("AdditionalIncludeDirectories", "%(AdditionalIncludeDirectories)")
               

                );

            yield return ItemDefinitionRoute("Link",
                MetadataListRoute("AdditionalDependencies", "%(AdditionalDependencies)")
              
               
                );
        }

        private ToRoute ItemDefinitionRoute(string name, params ToRoute[] children) {
            return name.MapTo<ProjectItemDefinitionGroupElement>(idg => {
                foreach(var i in idg.Children) {
                    var pide = (i as ProjectItemDefinitionElement);
                    if(pide != null) {
                        if(pide.ItemType == name ) {
                            return pide;
                        }
                    }
                }

                var c = idg.AddItemDefinition(name);
                return c;
            }, children );
        }

        private ToRoute MetadataRoute(string metadataName, string defaultValue = null) {
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

        private ToRoute MetadataListRoute(string metadataName, string defaultValue = null) {
            return metadataName.MapTo<ProjectItemDefinitionElement>(pide => {
                foreach(var m in pide.Metadata) {
                    var metadata = m;
                    if(metadata.Name == metadataName) {
                        return (IList) new MList(() => metadata.Value, v => metadata.Value = v);
                    }
                }
                var n = pide.AddMetadata(metadataName, defaultValue ?? "");
                return (IList) new MList(() => n.Value, v => n.Value = v);
            });
        }

        public void SaveNuspec() {

        }

        public void SaveTargets() {
            _targets.Save("test.targets");
        }

        public void SaveProps() {
            _targets.Save("test.props");
        }

        private ProjectItemDefinitionGroupElement FindOrCreateIDG(Project p, string condition) {
            // look it up or create it.
            if (string.IsNullOrEmpty(condition)) {
                var result = p.Xml.ItemDefinitionGroups.FirstOrDefault(each => string.IsNullOrEmpty(each.Label));
                if (result != null) {
                    return result;
                }
            } else {
                var result = p.Xml.ItemDefinitionGroups.FirstOrDefault(each => condition == each.Label);
                if(result != null) {
                    return result;
                }
            }

            var idg = p.Xml.AddItemDefinitionGroup();

            if (!string.IsNullOrEmpty(condition)) {
                idg.Label = condition;
                idg.Condition = condition;    
            }
            
            return idg;
        }

      //  private object FindOrCreate<TElement>(Project p, string elementType) {
        // "UsingTask".MapTo( FindOrCreate(_targets, "UsingTask") ),          
//        }
    }

    

    internal class Program {
        public object SomeLookup(string param) {
            return null;
        }

        private static void Main(string[] args) {
            new Program().Start(args);
        }

      

        private void Start(string[] args) {
            try {
                Console.WriteLine("Package script" );
                var script = new PackageScript("test.autopkg");
                script.SaveProps();
                script.SaveTargets();
                script.SaveNuspec();

                
            } catch (Exception e) {
                Console.WriteLine("{0} =>\r\n\r\nat {1}", e.Message, e.StackTrace.Replace("at ClrPlus.Scripting.Languages.PropertySheetV3.PropertySheetParser", "PropertySheetParser"));
            }
            return;
        }
    }

    [Cmdlet(AllVerbs.Add, "Nothing")]
    public class AddNothingCmdlet : PSCmdlet {
        protected override void ProcessRecord() {
            using (var ps = Runspace.DefaultRunspace.Dynamic()) {
                var results = ps.GetItemss("c:\\");
                foreach (var item in results) {
                    Console.WriteLine(item);
                }
            }
        }
    }
}
