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

namespace ClrPlus.Scripting.MsBuild {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation.Runspaces;
    using System.Xml;
    using System.Xml.Linq;
    using Core.Collections;
    using Core.DynamicXml;
    using Core.Exceptions;
    using Core.Extensions;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Evaluation;
    using Platform;
    using Powershell.Core;

    internal static class PackageScriptExtensions {
        internal static string SafeToString(this object value, string defaultValue = null) {
            if (value == null) {
                return defaultValue;
            }

            var v = value.ToString();
            return  v;
        }

        internal static Uri SafeToUri(this object value) {
            var v = SafeToString(value);
            return v == null ? null : v.ToUri();
        }
    }

    public class PackageScript : IDisposable {
        private const string RequiredTemplate = @"

#defines { condition = """"; }

nuget {
 	// built-in defines 
	#defines { 
		d_content   = \content\native,
		d_tools     = \tools\native,
		d_root      = \lib\native,
        
		d_include   = ${d_root}\include\${condition},
		d_docs      = ${d_root}\docs\${condition},
		d_bin       = ${d_root}\bin\${condition},  
		d_lib       = ${d_root}\lib\${condition},
		d_exes		= ${d_tools}\${condition},

		pkg_root    = $(MSBuildThisFileDirectory)..\..\,
	};
	
    targets {
        @alias Includes = ItemDefinitionGroup.ClCompile.AdditionalIncludeDirectories;
        @alias Defines = ItemDefinitionGroup.ClCompile.PreprocessorDefinitions;
        @alias Libraries = ItemDefinitionGroup.Link.AdditionalDependencies;
    }
    
    props {
        @alias Includes = ItemDefinitionGroup.ClCompile.AdditionalIncludeDirectories;
        @alias Libraries = ItemDefinitionGroup.Link.AdditionalDependencies;
        @alias Defines = ItemDefinitionGroup.ClCompile.PreprocessorDefinitions;
    }

	files {
        lib: { 
            #flatten : true;
            #destination : ${d_lib}; 
        };
        include: { 
            #destination : ${d_include}; };
		docs: {  
            #destination : ${d_docs}; 
        };
		bin: { 
            #destination : ${d_bin}; 
        };
	};
}";

        private dynamic _nuspec =new DynamicNode("package");

        private Project _targets = new Project();
        private Project _props = new Project();
        private string pkgName = "Package";
        private string autopkgFolder;
        private string nuspecPath;
        private string propsPath;
        private string targetsPath;


        public string Filename { get; set; }
        protected PropertySheet _sheet;

        public void Save() {
            _sheet.SaveFile(Filename);
        }

        public PackageScript(string filename) {

            _nuspec.metadata.id = "Package";
            _nuspec.metadata.version = "1.0.0";
            _nuspec.metadata.authors = "NAME";
            _nuspec.metadata.owners = "NAME";
            _nuspec.metadata.licenseUrl = "http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE";
            _nuspec.metadata.projectUrl = "http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE";
            _nuspec.metadata.iconUrl = "http://ICON_URL_HERE_OR_DELETE_THIS_LINE";
            _nuspec.metadata.requireLicenseAcceptance = "false";
            _nuspec.metadata.description = "Package description";
            _nuspec.metadata.releaseNotes = "Summary of changes made in this release of the package.";
            _nuspec.metadata.copyright = "Copyright 2013";

            _sheet = new PropertySheet(this);

            // get the full path to the .autopkgFile
            var fullPath = filename.GetFullPath();
            autopkgFolder = Directory.GetParent(fullPath).FullName;
       
            // parse the script
            _sheet.ParseFile(filename);
            _sheet.ImportText(RequiredTemplate, "required");

            // ensure we have at least the package ID
            pkgName = _sheet.View.nuget.nuspec.id;
            if (string.IsNullOrEmpty(pkgName)) {
                throw new ClrPlusException("the Field nuget.nuspec.id can not be null or empty. You must specify an id for a package.");
            }

            // set the package name macro 
            _sheet.AddMacro("pkgname", pkgName);

            // generate the relative output paths.
            nuspecPath = Path.Combine(autopkgFolder, "{0}.nuspec".format(pkgName));
            propsPath = Path.Combine(autopkgFolder, "{0}.props".format(pkgName));
            targetsPath = Path.Combine(autopkgFolder, "{0}.targets".format(pkgName));

            // do the property sheet mapping
            var conditions = new XDictionary<string, string>();

            _sheet.Route("nuget.nuspec".MapTo(new object() , MapNuspec().ToArray() ));
            _sheet.Route("nuget.files".MapTo( new object() ,
                "condition".MapTo(conditions, key => Configurations.NormalizeConditionKey(key, _sheet.View.configurations as View)),
                "*".MapTo(conditions, key => Configurations.NormalizeConditionKey(key, _sheet.View.configurations as View))
                ));

            var hasProps = (_sheet.View.nuget.HasChild("props") && _sheet.View.nuget.props.HasChildren );
            var hasTargets = (_sheet.View.nuget.HasChild("targets") && _sheet.View.nuget.targets.HasChildren);

            if (hasProps) {
                _sheet.MapProject("nuget.props", _props);
                _sheet.MapConfigurations("configurations", _props);
            }

            if (hasTargets) {
                _sheet.MapProject("nuget.targets", _targets);
                _sheet.MapConfigurations("configurations", _targets);
            }

            // persist the propertysheet to the msbuild model.
            _sheet.View.CopyToModel();

            // generate automatic rules for lib/bin/include
            var implictRules = _sheet.CurrentView.GetMetadataValue("options.implicit-rules").IsNegative();

            // process files
            ProcessFiles(_sheet.View.nuget.files, autopkgFolder, implictRules, null);
        }

        private void ProcessFiles(View files, string fileRoot, bool implictRules, string appliedCondition) {
            appliedCondition = Configurations.NormalizeConditionKey((appliedCondition ?? ""), _sheet.View.configurations);

            foreach(var containerName in files.GetChildPropertyNames()) {
                View container = files.GetProperty(containerName);
                if (containerName == "condition" || containerName == "*" ) {
                    foreach(var condition in container.GetChildPropertyNames()) {
                        ProcessFiles(container.GetElement(condition), fileRoot, implictRules, condition);
                    }
                    continue;
                }
                
                // get the destination directory
                var destinationDirectory = container.GetMetadataValue("destination", false);
                var dest = string.IsNullOrEmpty(destinationDirectory) ? files.GetMacroValue("d_" + containerName) : destinationDirectory;

                // locate all the files in the collection.  
                var filemasks = container.Values;
                var foundFiles = Enumerable.Empty<string>();

                foreach (var mask in filemasks) {
                    var fileset = mask.FindFilesSmarterComplex(fileRoot).ToCacheEnumerable();
                    
                    if (!fileset.Any()) {
                        Console.WriteLine("WARNING: file selection '{0}' failed to find any files ",mask);
                    }
                    foundFiles = foundFiles.Union(fileset);
                }

                if(implictRules && containerName == "include") {
                    // add this folder to the appropriate target    
                    _targets.LookupItemDefinitionGroup(appliedCondition).LookupItemDefinitionElement("ClCompile").LookupMetadataList("AdditionalIncludeDirectories").AddUnique((files.GetMacroValue("pkg_root") + dest).Replace("${condition}", appliedCondition).Replace("\\\\", "\\"));
                }

                var flatten = container.GetMetadataValue("flatten", false).IsPositive();

                var relativePaths = foundFiles.GetMinimalPathsToDictionary();

                foreach (var src in relativePaths.Keys) {
                    string target = Path.Combine(dest, flatten ? Path.GetFileName(relativePaths[src]) : relativePaths[src]).Replace("${condition}", appliedCondition).Replace("\\\\", "\\");
                    
                    var file = _nuspec.files.Add("file");
                    file.Attributes.src = src.GetFullPath().Replace("\\\\", "\\");
                    file.Attributes.target = target;

                    if (implictRules) {
                        switch (containerName) {
                            case "bin":
                                // add a per-file copy rule to the appropriate target
                                var tsk = _targets.LookupTarget("AfterBuild", appliedCondition).AddTask("Copy");
                                tsk.SetParameter("SourceFiles", (files.GetMacroValue("pkg_root") + target).Replace("\\\\", "\\"));
                                tsk.SetParameter("DestinationFolder", "$(TargetDir)");
                                tsk.SetParameter("SkipUnchangedFiles","true");
                                break;

                            case "lib":
                                // add a Libraries += rule to the appropriate target
                                _targets.LookupItemDefinitionGroup(appliedCondition).LookupItemDefinitionElement("Link").LookupMetadataList("AdditionalDependencies").AddUnique((files.GetMacroValue("pkg_root") + target).Replace("${condition}", appliedCondition).Replace("\\\\", "\\"));
                                break;
                        }
                    }
                }
                
                // Console.WriteLine("SET : {0}, => {1}", containerName, dest);
            }
        }

       
        private IEnumerable<ToRoute> MapNuspec() {
            yield return "id".MapTo(() => (string)_nuspec.metadata.id , v => _nuspec.metadata.id = v.SafeToString());
            yield return "version".MapTo(() => (string)_nuspec.metadata.version , v => _nuspec.metadata.version = v.SafeToString());
            yield return "title".MapTo(() => (string)_nuspec.metadata.title , v => _nuspec.metadata.title = v.SafeToString());
            yield return "authors".MapTo(() => (string)_nuspec.metadata.authors , v => _nuspec.metadata.authors = v.SafeToString());
            yield return "owners".MapTo(() => (string)_nuspec.metadata.owners , v => _nuspec.metadata.owners = v.SafeToString());
            yield return "description".MapTo(() => (string)_nuspec.metadata.description , v => _nuspec.metadata.description = v.SafeToString());
            yield return "summary".MapTo(() => (string)_nuspec.metadata.summary , v => _nuspec.metadata.summary = v.SafeToString());
            yield return "releaseNotes".MapTo(() => (string)_nuspec.metadata.releaseNotes , v => _nuspec.metadata.releaseNotes = v.SafeToString());
            yield return "copyright".MapTo(() => (string)_nuspec.metadata.copyright, v => _nuspec.metadata.copyright = v.SafeToString());
            yield return "language".MapTo(() => (string)_nuspec.metadata.language, v => _nuspec.metadata.language = v.SafeToString());
            yield return "tags".MapTo(() => (string)_nuspec.metadata.tags, v => _nuspec.metadata.tags = v.SafeToString());

            yield return "licenseUrl".MapTo(() => (string)_nuspec.metadata.licenseUrl, v => _nuspec.metadata.licenseUrl = v.SafeToString());
            yield return "projectUrl".MapTo(() => (string)_nuspec.metadata.projectUrl, v => _nuspec.metadata.projectUrl = v.SafeToString());
            yield return "iconUrl".MapTo(() => (string)_nuspec.metadata.iconUrl, v => _nuspec.metadata.iconUrl = v.SafeToString());

            yield return "requireLicenseAcceptance".MapTo(() => ((string) _nuspec.metadata.requireLicenseAcceptance).IsPositive() ? "true" : "false" , v => _nuspec.metadata.requireLicenseAcceptance = v.SafeToString().IsTrue().ToString().ToLower());

            yield return "dependencies".MapTo( new CustomPropertyList( (list) => {
                // when the list changes, set the value of the correct xml elements
                _nuspec.metadata.dependencies = null;
                _nuspec.metadata.Add("dependencies");
                foreach (var i in  list) {
                    var node = _nuspec.metadata.dependencies.Add("dependency");
                    var item = i.ToString();

                    var p = item.IndexOf('/') ;

                    if (p > -1) {
                        node.Attributes.id = item.Substring(0, p);
                        node.Attributes.version = item.Substring(p + 1);
                    } else {
                        node.Attributes.id = item;
                    }
                }
            })) ;

        }

        public void SaveNuspec() {
            XElement e = _nuspec.Element;
            e.Save(nuspecPath);
        }

        public void SaveTargets() {
            File.Delete(targetsPath);
            if (_targets.Xml.Children.Count > 0) {
                var file = _nuspec.files.Add("file");
                file.Attributes.src = autopkgFolder.RelativePathTo(targetsPath);
                file.Attributes.target = @"\build\native\" + pkgName + ".targets";

                _targets.Save(targetsPath);    
            }
        }

        public void SaveProps() {
            File.Delete(propsPath);
            if (_props.Xml.Children.Count > 0) {
                var file = _nuspec.files.Add("file");
                file.Attributes.src = autopkgFolder.RelativePathTo(propsPath);
                file.Attributes.target = @"\build\native\" + pkgName + ".props";
                _props.Save(propsPath);
            }
        }

        public void NuPack() {
            using (dynamic ps = Runspace.DefaultRunspace.Dynamic()) {
                foreach(var r in ps.InvokeExpression(@"nuget.exe pack ""{0}""".format(nuspecPath))) {
                    Console.WriteLine(r);
                }
            }
        }

        public bool Validate() {

            return true;
        }

        public void Dispose() {
            _sheet = null;
            _nuspec = null;
            
            if (MsBuildMap._projects.ContainsKey(_props)) {
                MsBuildMap._projects.Remove(_props);    
            }

            if(MsBuildMap._projects.ContainsKey(_targets)) {
                MsBuildMap._projects.Remove(_targets);
            }

            if (ProjectCollection.GlobalProjectCollection.LoadedProjects.Contains(_props)) {
                ProjectCollection.GlobalProjectCollection.UnloadProject(_props);
            }
            _props = null;
            if(ProjectCollection.GlobalProjectCollection.LoadedProjects.Contains(_targets)) {
                ProjectCollection.GlobalProjectCollection.UnloadProject(_targets);
            }
            _targets = null;
        }
    }
}