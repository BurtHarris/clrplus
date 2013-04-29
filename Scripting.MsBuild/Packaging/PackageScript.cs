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
    using System.Reflection;
    using System.Xml.Linq;
    using Core.Collections;
    using Core.DynamicXml;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheet;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Evaluation;
    using Platform;
    using Powershell.Core;

    public class PackageScript : IDisposable {
        private static readonly string _requiredTemplate = Assembly.GetExecutingAssembly().ExtractFileResource("PackageScriptTemplate.autopkg");
        internal readonly Pivots Pivots;
        internal string PackageDirectory {
            get {
                return Directory.GetParent(FullPath).FullName;
            }
        }
        internal readonly string FullPath;
        private bool _initialized;
        private Dictionary<string, NugetPackage> _nugetPackages = new Dictionary<string, NugetPackage>();
        private bool _processed;
        private RootPropertySheet _sheet;

        private dynamic _nuspec;
        private dynamic _nuget;
        private dynamic _nugetfiles;
        private readonly List<string> _dependentNuGetPackageDirectories = new List<string>();

        public PackageScript(string filename) {
            Event<Trace>.Raise("PackageScript", "Constructor");
            _sheet = new RootPropertySheet(this);

            // get the full path to the .autopkgFile
            FullPath = filename.GetFullPath();

            // parse the script
            _sheet.ParseFile(filename);
            _sheet.ImportText(_requiredTemplate, "required");

            // ensure we have at least the package ID
            var packageName = _sheet.View.nuget.nuspec.id;
            if (string.IsNullOrEmpty(packageName)) {
                throw new ClrPlusException("the Field nuget.nuspec.id can not be null or empty. You must specify an id for a package.");
            }

            // set the package name macro 
            _sheet.AddMacro("pkgname", packageName);
            Pivots = new Pivots(_sheet.CurrentView.GetProperty("configurations"));
        }

        public void AddNuGetPackageDirectory(string directory) {
            _dependentNuGetPackageDirectories.Add(directory);
        }

        internal NugetPackage GetNugetPackage(string name) {
            return _nugetPackages[name];
        }

        public void Dispose() {
            _nugetPackages.Dispose();
            _sheet = null;
            _nugetPackages = null;
        }

        private string NormalizeOuptutKey(string key) {
            if (string.IsNullOrEmpty(key)) {
                return "default";
            }
            return key.Replace('/', ',').Replace('\\', ',').Replace('&', ',').SplitToList(',', ' ').OrderBy(each => each).Aggregate((current, each) => current + ',' + each).Trim(',');
        }

        public void SaveSource() {
            _sheet.SaveFile(FullPath);
        }

        /// <exception cref="ClrPlusException">Fatal Error.</exception>
        private void Fail(bool isFatal) {
            if (isFatal) {
                throw new ClrPlusException("Fatal Error.");
            }
        }

        /// <exception cref="ClrPlusException">Fatal Error.</exception>
        private void FailAlways(bool whocares) {
            throw new ClrPlusException("Fatal Error.");
        }

        private void InitializeNuget() {
            View nugetView = _sheet.View.nuget;
            _nuget = nugetView;

            if (!nugetView.HasChildren ) {
                FailAlways(Event<SourceError>.Raise("AP100", _sheet.CurrentView.SourceLocations, "script does not contain a declaration for a NuGet package"));
            }

            _nuspec = _nuget.nuspec;

            if(!_nuspec.HasChildren) {
                FailAlways(Event<SourceError>.Raise("AP102", nugetView.SourceLocations, "script does not contain a 'nuspec' declaration in 'nuget'"));
            }

            var outputs = nugetView.GetMetadataItems("output-packages.");
            if (!outputs.Any()) {
                FailAlways(Event<SourceError>.Raise("AP101", nugetView.SourceLocations, "script does not contain '#output-packages' declaration in 'nuget'"));
            }

            if(string.IsNullOrEmpty(_nuspec.id.Value)) {
                FailAlways(Event<SourceError>.Raise("AP103", _nuspec.SourceLocations, "script does not contain a 'id' declaration in 'nuspec'"));
            }

            foreach (var each in outputs.Keys) {
                _nugetPackages.Add(each, new NugetPackage(this, each, outputs[each]));
            }

            // initialize the nuget packages
            nugetView.AddChildRoutes(_nugetPackages.Values.SelectMany(each => each.Initialize()));


            // do the property sheet mapping
            var conditions = new XDictionary<string, string>();

            // map the file routes 
            nugetView.AddChildRoute("files".MapTo(new object() , new [] {
                "condition".MapTo(conditions, key => Pivots.GetExpressionFilepath(_nuspec.id, key)),
                "*".MapTo(conditions, key => Pivots.GetExpressionFilepath(_nuspec.id, key))
            }));
            var conditionFolderMacroHander = (GetMacroValueDelegate)((macro, context) => {
                if (macro == "conditionFolder") {
                    return Pivots.GetExpressionFilepath(_nuspec.id, ((View)context).GetMacroValue("ElementId"));
                }
                return null;
            });
            _nuget.props.AddMacro(conditionFolderMacroHander);
            _nuget.targets.AddMacro(conditionFolderMacroHander);

            nugetView.AddChildRoute("props".MapTo(() => GetPropsProject("default") /*, GetPropsProject("default").ProjectRoutes() */));
            
            // always need a targets
            nugetView.AddChildRoute("targets".MapTo(() => GetTargetsProject("default")/*, GetTargetsProject("default").ProjectRoutes() */));
            // other variants/frameworks

            nugetView.AddChildRoute(
                "condition".MapTo(_nugetPackages, key => NormalizeOuptutKey(key), new[] {
                    "targets".MapTo<NugetPackage>(package => GetTargetsProject(package.PkgRole)),
                    "props".MapTo<NugetPackage>(package => GetPropsProject(package.PkgRole))
                }));

            nugetView.AddChildRoute(
                "*".MapTo(_nugetPackages, key => NormalizeOuptutKey(key),new [] {
                    "targets".MapTo<NugetPackage>(package => GetTargetsProject(package.PkgRole)),
                    "props".MapTo<NugetPackage>(package => GetPropsProject(package.PkgRole))}));

         
        }



        public void Initialize(PackageTypes packageTypes = PackageTypes.All) {
            if (_initialized) {
                return;
            }

            Event<Trace>.Raise("PackageScript.Initialize", "Init package script");

            if (packageTypes.HasFlag(PackageTypes.NuGet) ) {
               InitializeNuget();
            }

            _initialized = true;
        }

        private void ProcessNuget() {
            _nugetfiles = _nuget.files;
            if(!_nugetfiles.HasChildren) {
                Fail(Event<SourceWarning>.Raise("AP200", _nuget.SourceLocations, "script does not contain a 'files' declaration in 'nuget'"));
            }
            Event<Trace>.Raise("PackageScript.ProcessNuget", "Processing Nuget Files");
            // process files
            ProcessNugetFiles(_nugetfiles, PackageDirectory, null);
            Event<Trace>.Raise("PackageScript.ProcessNuget", "Done Processing Nuget Files");
            // handle each package 
            foreach(var nugetPackage in _nugetPackages.Values) {
                nugetPackage.Process();
            }
        }

        private void ProcessCoApp() {
            
        }

        public void Process(PackageTypes packageTypes = PackageTypes.All) {
            if (!_initialized) {
                Initialize(packageTypes);
            }

            if (_processed) {
                return;
            }

            Event<Trace>.Raise("PackageScript.Process", "Processing Package Creation");

            // persist the propertysheet to the msbuild model.
            _sheet.View.CopyToModel();

            Event<Trace>.Raise("PackageScript.Process", "(copy to model, done)");

            if (packageTypes.HasFlag(PackageTypes.NuGet)) {
                ProcessNuget();
            }

            if(packageTypes.HasFlag(PackageTypes.CoApp)) {
                ProcessCoApp();
            }
            _processed = true;
        }

        public void Save(PackageTypes packageTypes, bool cleanIntermediateFiles) {
            if (!_processed) {
                Process();
            }

            if (packageTypes.HasFlag(PackageTypes.NuGet)) {
                foreach (var nugetPackage in _nugetPackages.Values) {
                    nugetPackage.Save(cleanIntermediateFiles);
                }
            }
        }

        private ProjectPlus GetTargetsProject(string key) {
            string packageName;
            string frameworkVariant;

            GetPackageNameAndFramework(key, out packageName, out frameworkVariant);
            return _nugetPackages[packageName].GetTargetsProject(frameworkVariant);
        }
        private ProjectPlus GetPropsProject(string key) {
            string packageName;
            string frameworkVariant;

            GetPackageNameAndFramework(key, out packageName, out frameworkVariant);
            return _nugetPackages[packageName].GetPropsProject(frameworkVariant);
        }

        private void GetPackageNameAndFramework(string key, out string packageName, out string frameworkVariant ) {
            key = NormalizeOuptutKey(key);

            var k = key.SplitToList(',', ' ');

            // if there are more than two items here, then this is not valid.
            packageName = "default";
            frameworkVariant = "native";

            switch (k.Count) {
                case 0:
                    // default name and framework.
                    break;

                case 1:
                    if (_nugetPackages.Keys.Contains(k[0])) {
                        packageName = k[0];
                    } else {
                        frameworkVariant = k[0];
                    }
                    break;
                case 2:
                    if (_nugetPackages.Keys.Contains(k[0])) {
                        packageName = k[0];
                        frameworkVariant = k[1];
                    } else {
                        if (_nugetPackages.Keys.Contains(k[1])) {
                            frameworkVariant = k[0];
                            packageName = k[1];
                        } else {
                            FailAlways(Event<SourceError>.Raise("AP104", SourceLocation.Unknowns, "Two parameters specified for project, neither of which is an output-package"));
                        }
                    }
                    break;
                default:
                    FailAlways(Event<SourceError>.Raise("AP105", SourceLocation.Unknowns, "Project references can only contain up to two pivots (output-package and framework)"));
                    break;
            }
        }
        
        private void ProcessNugetFiles(View filesView, string srcFilesRoot, string currentCondition) {
            currentCondition = Pivots.NormalizeExpression(currentCondition ?? "");

            foreach (var containerName in filesView.GetChildPropertyNames()) {
                View container = filesView.GetProperty(containerName);
                if (containerName == "condition" || containerName == "*") {
                    foreach (var condition in container.GetChildPropertyNames()) {
                        ProcessNugetFiles(container.GetElement(condition), srcFilesRoot, condition);
                    }
                    continue;
                }
                
                var filemasks = container.Values;
                var relativePaths = new Dictionary<string, string>();

                foreach (var mask in filemasks) {
                    if (string.IsNullOrEmpty(mask)) {
                        continue;
                    }
                    var fileset = mask.FindFilesSmarterComplex(srcFilesRoot).GetMinimalPathsToDictionary();

                    if (!fileset.Any()) {
                        Event<Warning>.Raise("ProcessNugetFiles","WARNING: file selection '{0}' failed to find any files ", mask);
                        continue;
                    }
                    foreach (var key in fileset.Keys) {
                        relativePaths.Add(key, fileset[key]);
                    }
                }

                var optionPackages = container.GetMetadataValuesHarder("output.package", currentCondition).Union(container.GetMetadataValuesHarder("output.packages", currentCondition)).ToArray();

                if (optionPackages.Length == 0) {
                    optionPackages = new [] {"default"};
                }

                var optionExcludes = container.GetMetadataValues("exclude", container, false).Union(container.GetMetadataValues("excludes", container, false)).ToArray();
             
              
                // var targets = package.GetTargetsProject(optionFramework); 

                // determine the destination location in the target package
                var optionDestination = container.GetMetadataValueHarder("destination", currentCondition);
                var destinationFolder = string.IsNullOrEmpty(optionDestination) ? (filesView.GetMacroValue("d_" + containerName) ?? "\\") : optionDestination;

                var optionFlatten = container.GetMetadataValueHarder("flatten", currentCondition).IsPositive();

              
                var addEachFiles = container.GetMetadataValuesHarder("add-each-file", currentCondition).ToArray();
                var addFolders = container.GetMetadataValuesHarder("add-folder", currentCondition).ToArray();

                if (addFolders.Length > 0 ) {
                    foreach (var addFolder in addFolders) {
                        var folderView = filesView.GetProperty(addFolder.Replace("${condition}", currentCondition));
                        if (folderView != null) {
                            var values = folderView.Values.ToList();
                            values.Add((filesView.GetMacroValue("pkg_root") + destinationFolder).Replace("\\\\", "\\"));
                            folderView.Values = values;
                        }
                    }
                }

                foreach (var optionPackage in optionPackages) {
                    if (!_nugetPackages.Keys.Contains(optionPackage)) {
                        FailAlways(Event<SourceError>.Raise("AP300", SourceLocation.Unknowns, "Unknown #output-package '{0}' in files section '{1}' ", optionPackage, containerName));
                    }

                    var package = _nugetPackages[optionPackage];

                    foreach (var src in relativePaths.Keys) {
                        if (optionExcludes.HasWildcardMatch(src)) {
                            continue;
                        }

                        Event<Trace>.Raise("ProcessNugetFiles (adding file)", "'{0}' + '{1}'", destinationFolder, relativePaths[src]);
                        string target = Path.Combine(destinationFolder, optionFlatten ? Path.GetFileName(relativePaths[src]) : relativePaths[src]).Replace("${condition}", currentCondition).Replace("\\\\", "\\");
                        package.AddFile(src, target);

                        if (addEachFiles.Length > 0) {
                            foreach (var addEachFile in addEachFiles) {
                                var fileListView = filesView.GetProperty(addEachFile.Replace("${condition}", currentCondition));
                                if (fileListView != null) {
                                    var values = fileListView.Values.ToList();
                                    values.Add((filesView.GetMacroValue("pkg_root") + target).Replace("${condition}", currentCondition).Replace("\\\\", "\\"));
                                    fileListView.Values = values;
                                }
                            }
                        }
                    }
                }
            }
        }


        private IEnumerable<ToRoute> MapDependencies() {
            yield break;
        }

        public bool Validate() {
            return true;
        }
    }
}

