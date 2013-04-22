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
    using System.Management.Automation.Runspaces;
    using System.Text;
    using System.Xml.Linq;
    using Core.DynamicXml;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;
    using Platform;
    using Powershell.Core;
    using Utility;

    internal class NugetPackage : IProjectOwner {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
        private readonly PackageScript _packageScript;
        private readonly string _pkgName;  // whole name of the package (ie 'zlib' or 'zlib.redist')
        internal readonly string PkgRole;  // role of the package (ie 'default' or 'redist')

        private readonly Dictionary<string, ProjectPlus> _props = new Dictionary<string, ProjectPlus>();
        private readonly Dictionary<string, ProjectPlus> _targets = new Dictionary<string, ProjectPlus>();

        public string ProjectName {
            get {
            return _pkgName;
        }}

        internal string SafeName {
            get {
                return ProjectName.MakeSafeFileName().Replace(".", "_");
            }
        }

        internal string NuspecFilename { get {
            return _pkgName + ".nuspec";
        }}

        public string FullPath { get {
            return Path.Combine(Directory, NuspecFilename);
        }}

        public string Directory {
            get {
                return _packageScript.PackageDirectory;
            }
        }

        private dynamic _nuSpec = new DynamicNode("package");

        internal NugetPackage(PackageScript packageScript, string packageRole, string packageName) {
            _packageScript = packageScript;
            _pkgName = packageName;
            PkgRole = packageRole;
           
            _nuSpec.metadata.id = "Package";
            _nuSpec.metadata.version = "1.0.0";
            _nuSpec.metadata.authors = "NAME";
            _nuSpec.metadata.owners = "NAME";
            _nuSpec.metadata.licenseUrl = "http://LICENSE_URL_HERE_OR_DELETE_THIS_LINE";
            _nuSpec.metadata.projectUrl = "http://PROJECT_URL_HERE_OR_DELETE_THIS_LINE";
            _nuSpec.metadata.iconUrl = "http://ICON_URL_HERE_OR_DELETE_THIS_LINE";
            _nuSpec.metadata.requireLicenseAcceptance = "false";
            _nuSpec.metadata.description = "Package description";
            _nuSpec.metadata.releaseNotes = "Summary of changes made in this release of the package.";
            _nuSpec.metadata.copyright = "Copyright 2013";
        }

        public Pivots Pivots {
            get {
                return _packageScript.Pivots;
            }
        }

        public void Dispose() {
            // properly dispose of the projects (ie, unload them)
            _props.Dispose();
            _targets.Dispose();
        }

        public void AddFile(string sourcePath, string destinationPath) {
            if (_files.ContainsKey(destinationPath)) {
                Event<Error>.Raise("AP100", "Duplicate file '{0}' added to NuGet package from source '{1}'", destinationPath, sourcePath);
            }

            _files.Add(destinationPath, sourcePath);
        }

        public bool IsDefault {
            get {
                return PkgRole == "default";
            }
        }

        internal IEnumerable<ToRoute> Initialize() {
            var results = Enumerable.Empty<ToRoute>();

            switch (PkgRole) {
                case "default":
                    // only the default package gets to map to the propertysheet directly.
                    results = results.Concat(MapNugetNode());
                    break;
                case "redist":
                    break;
                default:
                    break;
            }
            return results;
        }

        private IEnumerable<ToRoute> MapNugetNode() {
            // only the default package gets to do this.
            yield return "nuspec".MapTo(new object(), new [] {
             "id".MapTo(() => (string)_nuSpec.metadata.id, v => _nuSpec.metadata.id = v.SafeToString()),
             "version".MapTo(() => (string)_nuSpec.metadata.version, v => _nuSpec.metadata.version = v.SafeToString()),
             "title".MapTo(() => (string)_nuSpec.metadata.title, v => _nuSpec.metadata.title = v.SafeToString()),
             "authors".MapTo(() => (string)_nuSpec.metadata.authors, v => _nuSpec.metadata.authors = v.SafeToString()),
             "owners".MapTo(() => (string)_nuSpec.metadata.owners, v => _nuSpec.metadata.owners = v.SafeToString()),
             "description".MapTo(() => (string)_nuSpec.metadata.description, v => _nuSpec.metadata.description = v.SafeToString()),
             "summary".MapTo(() => (string)_nuSpec.metadata.summary, v => _nuSpec.metadata.summary = v.SafeToString()),
             "releaseNotes".MapTo(() => (string)_nuSpec.metadata.releaseNotes, v => _nuSpec.metadata.releaseNotes = v.SafeToString()),
             "copyright".MapTo(() => (string)_nuSpec.metadata.copyright, v => _nuSpec.metadata.copyright = v.SafeToString()),
             "language".MapTo(() => (string)_nuSpec.metadata.language, v => _nuSpec.metadata.language = v.SafeToString()),
             "tags".MapTo(() => (string)_nuSpec.metadata.tags, v => _nuSpec.metadata.tags = v.SafeToString()),

             "licenseUrl".MapTo(() => (string)_nuSpec.metadata.licenseUrl, v => _nuSpec.metadata.licenseUrl = v.SafeToString()),
             "projectUrl".MapTo(() => (string)_nuSpec.metadata.projectUrl, v => _nuSpec.metadata.projectUrl = v.SafeToString()),
             "iconUrl".MapTo(() => (string)_nuSpec.metadata.iconUrl, v => _nuSpec.metadata.iconUrl = v.SafeToString()),

             "requireLicenseAcceptance".MapTo(() => ((string)_nuSpec.metadata.requireLicenseAcceptance).IsPositive() ? "true" : "false", v => _nuSpec.metadata.requireLicenseAcceptance = v.SafeToString().IsTrue().ToString().ToLower())});
            
                
                    // map the dependencies node into generating 
            yield return "dependencies.packages".MapTo(new CustomPropertyList((list) => {
                // when the list changes, set the value of the correct xml elements
                _nuSpec.metadata.dependencies = null;
                _nuSpec.metadata.Add("dependencies");
                foreach (var i in list) {
                    var node = _nuSpec.metadata.dependencies.Add("dependency");
                    var item = i.ToString();

                    var p = item.IndexOf('/');

                    if (p > -1) {
                        node.Attributes.id = item.Substring(0, p);
                        node.Attributes.version = item.Substring(p + 1);
                    } else {
                        node.Attributes.id = item;
                    }
                }
            }));

        }

        internal void Process() {
            Event<Trace>.Raise("NugetPackage.Process", "Processing nuget package [{0}].", NuspecFilename);
            
            switch(PkgRole) {
                case "default":
                    // add a dependency to the redist package.
                    var redistPkg = _packageScript.GetNugetPackage("redist");
                    if (redistPkg != null) {
                        // add the dependency to the list 
                        var node = _nuSpec.metadata.dependencies.Add("dependency");
                        node.Attributes.id = redistPkg._pkgName;
                        node.Attributes.version = (string)_nuSpec.metadata.version;;
                        var targets = GetTargetsProject("native");
                        targets.BeforeSave += () => {
                            ProjectPropertyGroupElement ppge = null;
                            foreach (var p in Pivots.Values) {
                                if (string.IsNullOrEmpty(p.Key)) {
                                    ppge = targets.AddPropertyInitializer("{0}-{1}".format(p.Name, redistPkg.SafeName), "", "$({0}-{1})".format(p.Name, SafeName), ppge);
                                }
                            }
                        };

                    }
                    break;
                case "redist":
                    //copy the nuspec fields from the default project (and change what's needed)
                    var defaultPkg = _packageScript.GetNugetPackage("default");
                    _nuSpec = new DynamicNode(new XElement(defaultPkg._nuSpec.Element));

                    _nuSpec.metadata.requireLicenseAcceptance = "false";
                    _nuSpec.metadata.title = "{0} Redist".format((string)defaultPkg._nuSpec.metadata.title);
                    _nuSpec.metadata.summary = "Redistributable components for for package '{0}'".format(defaultPkg._pkgName);
                    _nuSpec.metadata.id = _pkgName;
                    _nuSpec.metadata.description = "Redistributable components for package '{0}'. This package should only be installed as a dependency. \r\n(This is not the package you are looking for).".format(defaultPkg._pkgName);
                    _nuSpec.metadata.dependencies = null;
                    break;

                case "symbols":
                    defaultPkg = _packageScript.GetNugetPackage("default");
                    _nuSpec = new DynamicNode(new XElement(defaultPkg._nuSpec.Element));

                    _nuSpec.metadata.title = "{0} Symbols".format((string)defaultPkg._nuSpec.metadata.title);
                    _nuSpec.metadata.requireLicenseAcceptance = "false";
                    _nuSpec.metadata.summary = "Symbols for for package '{0}'".format(defaultPkg._pkgName);
                    _nuSpec.metadata.id = _pkgName;
                    _nuSpec.metadata.description = "Symbols for package '{0}'. This package should not likely be installed. \r\n(This is not the package you are looking for).".format(defaultPkg._pkgName);
                    _nuSpec.metadata.dependencies = null;
                    break;

                default:
                    defaultPkg = _packageScript.GetNugetPackage("default");
                    _nuSpec = new DynamicNode(new XElement(defaultPkg._nuSpec.Element));

                    _nuSpec.metadata.requireLicenseAcceptance = "false";
                    _nuSpec.metadata.id = _pkgName;
                    _nuSpec.metadata.description = "*unknown*";
                    _nuSpec.metadata.dependencies = null;
                    break;
            }
        }

        internal void Validate() {
            // Event<Trace>.Raise("NugetPackage.Validate", "Validating nuget package (nothing)");
        }

        internal void Save(bool cleanIntermediateFiles) {
            // clear out the nuspec files node.
            _nuSpec.files = null;
            var temporaryFiles = new List<string>();

            var files = _nuSpec.Add("files");

            var xaml = GenerateSettingsXaml();
            if (xaml != null) {
                var targetFilename = @"{0}-propertiesui-{1}.xml".format(_pkgName, Guid.NewGuid());
                var xamlPath = Path.Combine(Directory, targetFilename);
                xamlPath.TryHardToDelete();
                Event<Trace>.Raise("NugetPackage.Save", "Saving xaml file [{0}].", xamlPath);
                xaml.Save(xamlPath);
                temporaryFiles.Add(xamlPath);
                AddFileToNuSpec(xamlPath, @"\build\native\{0}".format(targetFilename));
                GetTargetsProject("native").Xml.AddItemGroup().AddItem("PropertyPageSchema", @"$(MSBuildThisFileDirectory)\{0}".format(targetFilename));
            }

            foreach (var framework in _props.Keys) {
                var prj = _props[framework];
                if(prj.Xml.Children.Count > 0) {
                    prj.FullPath.TryHardToDelete();
                    if (prj.Save()) {
                        temporaryFiles.Add(prj.FullPath);
                        AddFileToNuSpec(prj.FullPath, @"\build\{0}\{1}".format(framework, prj.Filename));
                    }
                }
            }

            foreach(var framework in _targets.Keys) {
                var prj = _targets[framework];
                if(prj.Xml.Children.Count > 0) {
                    prj.FullPath.TryHardToDelete();
                    if (prj.Save()) {
                        temporaryFiles.Add(prj.FullPath);
                        AddFileToNuSpec(prj.FullPath, @"\build\{0}\{1}".format(framework, prj.Filename));
                    }
                }
            }

            // save the /build/configurations.autopkg file 
            var configurationsFilename = @"configurations.autopkg";
            var cfgPath = Path.Combine(Directory, configurationsFilename );
            cfgPath.TryHardToDelete();
            SaveConfigurationFile(cfgPath);
            temporaryFiles.Add(cfgPath);
            AddFileToNuSpec(cfgPath, @"\build\{0}".format(configurationsFilename));

            Event<Trace>.Raise("NugetPackage.Save", "Saving nuget spec file to [{0}].", FullPath);

            foreach(var src in _files.Keys) {
                AddFileToNuSpec(_files[src], src );
            }
            _nuSpec.Save(FullPath);
            temporaryFiles.Add(FullPath);

            NuPack(FullPath);

            if (cleanIntermediateFiles) {
                temporaryFiles.ForEach( FilesystemExtensions.TryHardToDelete );
            }
        }

        private bool SaveConfigurationFile(string cfgPath) {
            var sb = new StringBuilder();
            sb.Append("configurations {\r\n");

            foreach (var pivot in Pivots.Values) {
                if (pivot.UsedChoices.Any()) {
                    var used = pivot.Choices.Keys.First().SingleItemAsEnumerable().Union(pivot.UsedChoices).ToArray();
                    // yep, do this one.
                    sb.Append("    ").Append(pivot.Name).Append(" { \r\n"); // Platform {
                    if (pivot.Key.Is()) {
                        sb.Append("        key : \"").Append(pivot.Key).Append("\";\r\n"); //    key : "Platform"; 
                    }
                    sb.Append("        choices : { ").Append(used.Aggregate((current, each) => current + ", " + each)).Append(" };\r\n"); // choices: { Win32, x64, ARM, AnyCPU };
                    foreach(var ch in used) {
                        if (pivot.Choices[ch].Count() > 1) {
                            sb.Append("        ").Append(ch).Append(".aliases : { ").Append(pivot.Choices[ch].Aggregate((current, each) => current + ", " + each)).Append(" };\r\n"); //Win32.aliases : { x86, win32, ia32, 386 };
                        }
                    }
                    sb.Append("    };\r\n");
                }
            }
            sb.Append("};\r\n");

            File.WriteAllText(cfgPath, sb.ToString());
            return true;
        }

        private void AddFileToNuSpec(string src, string dest) {
            var file = _nuSpec.files.Add("file");
            file.Attributes.src = src.GetFullPath().Replace("\\\\", "\\");
            file.Attributes.target = dest;
        }
   
        internal ProjectPlus GetTargetsProject(string frameworkVariant) {
            return GetOrCreateProject("targets",frameworkVariant);
        }

        internal ProjectPlus GetPropsProject(string frameworkVariant) {
            return GetOrCreateProject("props", frameworkVariant);
        }

        internal ProjectPlus GetOrCreateProject(string projectFileExtension, string frameworkVariant) {
            frameworkVariant = frameworkVariant.WhenNullOrEmpty("native");

            switch (projectFileExtension) {
                case "targets":
                    if (!_targets.ContainsKey(frameworkVariant)) {
                        Event<Trace>.Raise("NugetPackage.GetOrCreateProject", "Creating .targets for [{0}] in role [{1}].", frameworkVariant, PkgRole);
                        _targets.Add(frameworkVariant, new ProjectPlus(this, "{0}.targets".format(_pkgName)));
                    }
                    return _targets[frameworkVariant];


                case "props":
                    if (!_props.ContainsKey(frameworkVariant)) {
                        Event<Trace>.Raise("NugetPackage.GetOrCreateProject", "Creating .props for [{0}] in role [{1}].", frameworkVariant, PkgRole);
                        _props.Add(frameworkVariant, new ProjectPlus(this, "{0}.props".format(_pkgName)));
                    }
                    return _props[frameworkVariant];
            }

            throw new ClrPlusException("Unknown project extension '{0}' ".format(projectFileExtension));
        }

        public void NuPack(string path) {
            using(dynamic ps = Runspace.DefaultRunspace.Dynamic()) {
                var results = ps.InvokeExpression(@"nuget.exe pack ""{0}"" 2>&1".format(path));
                bool lastIsBlank = false;
                foreach(var r in results) {
                    string s = r.ToString();
                    if (string.IsNullOrWhiteSpace(s)) {
                        if (lastIsBlank) {
                            continue;
                        }
                        lastIsBlank = true;
                    } else {
                        if (s.IndexOf("Issue: Assembly outside lib folder") > -1) {
                            continue;
                        }
                        if(s.IndexOf("folder and hence it won't be added as reference when the package is installed into a project") > -1) {
                            continue;
                        }
                        if (s.IndexOf("Solution: Move it into the 'lib' folder if it should be referenced") > -1) {
                            continue;
                        }
                        if(s.IndexOf("issue(s) found with package") > -1) {
                            continue;
                        }
                        
                        lastIsBlank = false;
                    }
                    
                    // Issue: Assembly outside lib folder.
                    // Description: The assembly 'build\native\bin\Win32\v110\Release\WinRT\casablanca110.winrt.dll' is not inside the 'lib' folder and hence it won't be added as reference when the package is installed into a project.
                    // Solution: Move it into the 'lib' folder if it should be referenced.
                    
                    Event<Message>.Raise(" >", "{0}", s);
                }
            }
        }

        private dynamic InitXaml() {
            dynamic node = new DynamicNode("ProjectSchemaDefinitions", "clr-namespace:Microsoft.Build.Framework.XamlTypes;assembly=Microsoft.Build.Framework");
            var rule = node.Add("Rule");

            rule.Attributes.Name = "ReferencedPackages{0}".format(Guid.NewGuid());
            rule.Attributes.PageTemplate = "tool";
            rule.Attributes.DisplayName = "Referenced Packages";
            rule.Attributes.SwitchPrefix = "/";
            rule.Attributes.Order = "1";

            var categories = rule.Add("Rule.Categories");
            var category = categories.Add("Category");
            category.Attributes.Name = _pkgName;
            category.Attributes.DisplayName = _pkgName;

            var datasources = rule.Add("Rule.DataSource");
            var datasource = datasources.Add("DataSource");
            datasource.Attributes.Persistence = "ProjectFile";
            datasource.Attributes.ItemType = "";

            return node;
        }

        

        private dynamic GenerateSettingsXaml() {
            if (!IsDefault) {
                return null;
            }
            dynamic xaml = null;

            foreach (var pivot in Pivots.Values.Where(pivot => string.IsNullOrEmpty(pivot.Key))) {

                xaml = xaml ?? InitXaml();

                var defaultchoice = pivot.Choices.Keys.FirstOrDefault();

                // add the key
                var enumProperty = xaml.Rule.Add("EnumProperty");
                enumProperty.Attributes.Name = "{0}-{1}".format(pivot.Name, _pkgName);
                enumProperty.Attributes.DisplayName = pivot.Name;
                enumProperty.Attributes.Description = pivot.Description;
                enumProperty.Attributes.Category = _pkgName;

                // add the choices
                foreach (var v in pivot.UsedChoices) {
                    var enumValue = enumProperty.Add("EnumValue");
                    enumValue.Attributes.Name = (v == defaultchoice) ? "" : v; // store "" as the value for defaultchoice.
                    enumValue.Attributes.DisplayName = pivot.Descriptions[v];
                }
            }

            return xaml;
        }
    }
}