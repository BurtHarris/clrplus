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
    using ClrPlus.Scripting.MsBuild;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;

    public class NuspecFields {

    };

    public class NuspecFiles {

    };

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

            _sheet.MapConfigurations("configurations",_props);
            _sheet.MapConfigurations("configurations", _targets);

            _sheet.MapProject("nuget.props", _props);
            _sheet.MapProject("nuget.targets", _targets);

            
            // 

            _sheet.View.CopyToModel();

            
            for(int i = 0; i <= _sheet.View.nuget.targets.Target["AfterBuild"].Count; i++) {
                Console.WriteLine( _sheet.View.nuget.targets.Target["AfterBuild"][i].Copy.SourceFiles);
            }
        }

      

        public void SaveNuspec() {

        }

        public void SaveTargets() {
            _targets.Save("test.targets");
        }

        public void SaveProps() {
            _targets.Save("test.props");
        }

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
