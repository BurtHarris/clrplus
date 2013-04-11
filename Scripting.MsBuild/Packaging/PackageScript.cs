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

        internal NugetPackage GetNugetPackage(string name) {
            return _nugetPackages[name];
        }

        public void Dispose() {
            _nugetPackages.Dispose();
            _sheet = null;
            _nugetPackages = null;
        }

        private string GetMetadataValue(string mdName) {
            return _sheet.CurrentView.GetMetadataValue(mdName);
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

            if (nugetView == null) {
                FailAlways(Event<SourceError>.Raise("AP100", _sheet.CurrentView.SourceLocations, "script does not contain a declaration for a NuGet package"));
            }

            _nuspec = _nuget.nuspec;

            if (_nuspec == null) {
                FailAlways(Event<SourceError>.Raise("AP102", nugetView.SourceLocations, "script does not contain a 'nuspec' declaration in 'nuget'"));
            }

            var outputs = nugetView.GetMetadataItems("output-packages.");
            if (!outputs.Any()) {
                FailAlways(Event<SourceError>.Raise("AP101", nugetView.SourceLocations, "script does not contain '#output-packages' declaration in 'nuget'"));
            }

            if (_nuspec.id == null) {
                FailAlways(Event<SourceError>.Raise("AP103", _nuspec.SourceLocations, "script does not contain a 'id' declaration in 'nuspec'"));
            }

            foreach (var each in outputs.Keys) {
                _nugetPackages.Add(each, new NugetPackage(this, each, outputs[each]));
            }

            // initialize the nuget packages
            _nuspec.AddChildRoutes(_nugetPackages.Values.SelectMany(each => each.Initialize()));

            // do the property sheet mapping
            var conditions = new XDictionary<string, string>();

            // map the file routes 
            nugetView.AddChildRoutes("files".MapTo(
                "condition".MapTo(conditions, key => Pivots.GetExpressionFilepath(_nuspec.id, key)),
                "*".MapTo(conditions, key => Pivots.GetExpressionFilepath(_nuspec.id, key))
                ));

            if (_nuget.props != null) {
                nugetView.AddChildRoutes("props".MapTo(() => GetPropsProject("default") /*, GetPropsProject("default").ProjectRoutes() */));
            }

            // always need a targets
            nugetView.AddChildRoutes("targets".MapTo(() => GetTargetsProject("default")/*, GetTargetsProject("default").ProjectRoutes() */));
            // other variants/frameworks

            nugetView.AddChildRoutes(
                "condition".MapTo(_nugetPackages, key => NormalizeOuptutKey(key),
                    "targets".MapTo<NugetPackage>(package => GetTargetsProject(package.PkgRole)),
                    "props".MapTo<NugetPackage>(package => GetPropsProject(package.PkgRole))),

                "*".MapTo(_nugetPackages, key => NormalizeOuptutKey(key),
                    "targets".MapTo<NugetPackage>(package => GetTargetsProject(package.PkgRole)),
                    "props".MapTo<NugetPackage>(package => GetPropsProject(package.PkgRole)))
                );
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
            if(_nugetfiles == null) {
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

        private string GetMetadataValueForNodeConditionedOrNot(string condition,string containerName ,View container, string metadataName) {
            var hasCondition = !string.IsNullOrEmpty(condition);
            var unconditionedView = hasCondition ? (container.ParentView.ParentView.HasChild(containerName) ? container.ParentView.ParentView.GetProperty(containerName) : null) : null;
            return (container.GetMetadataValue(metadataName, false) ?? (unconditionedView != null ? unconditionedView.GetMetadataValue(metadataName, false) : null));
        }

        private IEnumerable<View> GetConditionViews(string condition, string containerName, View container) {
            yield return container;
            if (!string.IsNullOrEmpty(condition) && container.ParentView.ParentView.HasChild(containerName)) {
                yield return container.ParentView.ParentView.GetProperty(containerName);
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
                        Console.WriteLine("WARNING: file selection '{0}' failed to find any files ", mask);
                        continue;
                    }
                    foreach (var key in fileset.Keys) {
                        relativePaths.Add(key, fileset[key]);
                    }
                }

                // at this point, we have a list of all the files in that section.
                var parameterViews = GetConditionViews(currentCondition, containerName, container).ToArray();

                var optionPackage = parameterViews.Select(each => each.GetMetadataValue("output-package", false)).FirstOrDefault(each => each != null);
                
                if (string.IsNullOrEmpty(optionPackage)) {
                    optionPackage = "default";
                }
                if (!_nugetPackages.Keys.Contains(optionPackage)) {
                    FailAlways(Event<SourceError>.Raise("AP300", SourceLocation.Unknowns, "Unknown #output-package '{0}' in files section '{1}' ", optionPackage,containerName ));
                }

                var package = _nugetPackages[optionPackage];
                var targets = package.GetTargetsProject("native"); // GS01 Fix this sometime later?

                // determine the destination location in the target package
                var optionDestination = container.GetMetadataValue("destination", false);
                var destinationFolder = string.IsNullOrEmpty(optionDestination) ? (filesView.GetMacroValue("d_" + containerName) ?? "\\") : optionDestination;

                
                var optionFlatten = parameterViews.Select(each => each.GetMetadataValue("flatten", false)).FirstOrDefault(each => each != null).IsPositive();
                var optionInclude = parameterViews.Select(each => each.GetMetadataValue("auto-include", false)).FirstOrDefault(each => each != null).IsPositive();
                var optionCopy = parameterViews.Select(each => each.GetMetadataValue("auto-copy", false)).FirstOrDefault(each => each != null).IsPositive();
                var optionLink = parameterViews.Select(each => each.GetMetadataValue("auto-link", false)).FirstOrDefault(each => each != null).IsPositive();

                
                if (optionInclude) {
                    // add this folder to the appropriate target    
                    targets.LookupItemDefinitionGroup(currentCondition)
                                      .LookupItemDefinitionElement("ClCompile")
                                      .LookupMetadataPathList("AdditionalIncludeDirectories", "%(AdditionalIncludeDirectories)")
                                      .Add((filesView.GetMacroValue("pkg_root") + destinationFolder).Replace("${condition}", currentCondition).Replace("\\\\", "\\"));
                }

                foreach (var src in relativePaths.Keys) {
                    string target = Path.Combine(destinationFolder, optionFlatten ? Path.GetFileName(relativePaths[src]) : relativePaths[src]).Replace("${condition}", currentCondition).Replace("\\\\", "\\");
                    package.AddFile( src, target);

                    if (optionCopy) {
                        var tsk = targets.LookupTarget("AfterBuild", currentCondition, true).AddTask("Copy");
                        tsk.SetParameter("SourceFiles", (filesView.GetMacroValue("pkg_root") + target).Replace("\\\\", "\\"));
                        tsk.SetParameter("DestinationFolder", "$(TargetDir)");
                        tsk.SetParameter("SkipUnchangedFiles", "true");
                    }

                    if (optionLink) {
                         targets.LookupItemDefinitionGroup(currentCondition)
                            .LookupItemDefinitionElement("Link")
                            .LookupMetadataPathList("AdditionalDependencies", "%(AdditionalDependencies)")
                            .Add((filesView.GetMacroValue("pkg_root") + target).Replace("${condition}", currentCondition).Replace("\\\\", "\\"));
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

#if false
        private string NuspecPath {
            get {
                return Path.Combine(_autopkgFolder, "{0}.nuspec".format(_pkgName));
            }
        }
        private string RedistNuspecPath {
            get {
                return Path.Combine(_autopkgFolder, "{0}.redist.nuspec".format(_pkgName));
            }
        }

        private string XamlPath {
            get {
                return Path.Combine(_autopkgFolder, "{0}-propertiesui.xml".format(_pkgName));
            }
        }

        private string PropsPath {
            get {
                return Path.Combine(_autopkgFolder, "{0}.props".format(_pkgName));
            }
        }

        private string TargetsPath {
            get {
                return Path.Combine(_autopkgFolder, "{0}.targets".format(_pkgName));
            }
        }

        private string RedistPropsPath {
            get {
                return Path.Combine(_autopkgFolder, "{0}.redist.props".format(_pkgName));
            }
        }

        private string RedistTargetsPath {
            get {
                return Path.Combine(_autopkgFolder, "{0}.redist.targets".format(_pkgName));
            }
        }

        private bool HasProps {
            get {
                return _hasProps ?? (_hasProps = (_sheet.View.nuget.HasChild("props") && _sheet.View.nuget.props.HasChildren)) == true;
            }
        }

        private bool HasTargets {
            get {
                return _hasTargets ?? (_hasTargets = (_sheet.View.nuget.HasChild("targets") && _sheet.View.nuget.targets.HasChildren)) == true;
            }
        }

        private bool HasRedist {
            get {
                return _hasRedist ?? (_hasRedist = (_sheet.View.nuget.HasChild("redist") && _sheet.View.nuget.targets.HasChildren)) == true;
            }
        }
        private bool HasDependencies {
            get {
                return _hasDependencies ?? (_hasDependencies = (_sheet.View.nuget.HasChild("dependencies") && _sheet.View.nuget.targets.HasChildren)) == true;
            }
        }

        private bool HasRedistProps {
            get {
                return _hasRedistProps ?? (_hasRedistProps = (HasRedist && _sheet.View.nuget.redist.HasChild("props") && _sheet.View.nuget.props.HasChildren)) == true;
            }
        }

        private bool HasRedistTargets {
            get {
                return _hasRedistTargets ?? (_hasRedistTargets = (HasRedist && _sheet.View.nuget.redist.HasChild("targets") && _sheet.View.nuget.targets.HasChildren)) == true;
            }
        }



        private bool? _hasProps;
        private bool? _hasTargets;
        private bool? _hasRedist;
        private bool? _hasDependencies;
        private bool? _hasRedistProps;
        private bool? _hasRedistTargets;
   /*
         * 
        private View _configurationsView;
        private readonly List<Project> _oldProjects = new List<Project>();
        private readonly Project _targets;
        private readonly Project _props;
        private readonly Project _redistTargets;
        private readonly Project _redistProps;
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _redistFiles = new Dictionary<string, string>();

        */
 private bool GenerateImplicitRules {
            get {
                return !GetMetadataValue("options.implicit-rules").IsNegative();
            }
        }

        private bool GenerateImplicitRedist {
            get {
                return !GetMetadataValue("options.implicit-redist").IsNegative();
            }
        }
 

        private IEnumerable<string> OutputPackageNames {
            get {
                if (_outputPackageNames == null) {
                    var metadata = (IDictionary<string, string>)_sheet.View.nuget.Metadata;
                    _outputPackageNames = (metadata.Keys).Where(each => each.StartsWith("output-packages.")).ToXDictionary(each => each.Substring("output-packages.".Length), each => metadata[each]);
                }
                return _outputPackageNames.Keys;
            }
        }


#if false
        public void SaveNuGetPackages(bool cleanUp) {
            Validate();

            SaveXaml();
            SaveProject(_props, _files, PropsPath);
            SaveProject(_targets, _files, TargetsPath);
            SaveProject(_redistProps, _redistFiles, RedistPropsPath);
            SaveProject(_redistTargets, _redistFiles, RedistTargetsPath);

            if (SaveRedistNuspec()) {
                NuPack(RedistNuspecPath);
                // add the redist package to the main package as a dependency

                var node = _nuspec.metadata.dependencies.Add("dependency");
                node.Attributes.id = _pkgName + ".redist";
                node.Attributes.version = _nuspec.version;
            }

            if (SaveNuspec()) {
                NuPack(NuspecPath);
            }

            if (cleanUp) {
                File.Delete(XamlPath);
                File.Delete(PropsPath);
                File.Delete(TargetsPath);
                File.Delete(RedistPropsPath);
                File.Delete(RedistTargetsPath);
                File.Delete(RedistNuspecPath);
                File.Delete(NuspecPath);
            }
        }

        private bool SaveNuspec() {
            // clear out the files first.
            _nuspec.files = null;
            var files = _nuspec.Add("files");
            foreach (var src in _redistFiles.Keys) {
                var file = files.Add("file");
                file.Attributes.src = src.GetFullPath().Replace("\\\\", "\\");
                file.Attributes.target = _redistFiles[src];
            }

            _nuspec.Save(NuspecPath);
            return true;
        }


        private bool SaveRedistNuspec() {
            if (HasRedist) {
                // copy the nuspec from the base package
                dynamic nuspec = new DynamicNode(new XElement(_nuspec.Element));

                // clear out any files in the tree.
                nuspec.files = null;
                var files = nuspec.Add("files");
                foreach (var src in _redistFiles.Keys) {
                    var file = files.Add("file");
                    file.Attributes.src = src.GetFullPath().Replace("\\\\", "\\");
                    file.Attributes.target = _redistFiles[src];
                }

                // reset the requireLicenseAcceptance 
                nuspec.metadata.requireLicenseAcceptance = "false";
                nuspec.metadata.id = _pkgName + ".redist";
                nuspec.metadata.description = "Redistributable components for package '{0}'. This package should only be installed as a dependency. \r\n(This is not the package you are looking for).";
                nuspec.metadata.dependencies = null; // redist packages 

                // add the *right* dependencies
                nuspec.Save(RedistNuspecPath);

                return true;
            }
            return false;
        }

        private bool SaveXaml() {
            File.Delete(XamlPath);
            if (_xaml != null) {
                _xaml.Save(XamlPath);

                var targetFilename = @"{0}-propertiesui-{1}.xml".format(_pkgName, Guid.NewGuid());
                _files.Add(XamlPath, @"\build\native\{0}".format(targetFilename));

                _targets.Xml.AddItemGroup().AddItem("PropertyPageSchema", @"$(MSBuildThisFileDirectory)\{0}".format(targetFilename));
                return true;
            }
            return false;
        }

        private bool SaveProject(Project project, IDictionary<string, string> fileContainer, string projectPath, string targetFolder = null) {
            targetFolder = targetFolder ?? @"\build\native\";

            File.Delete(projectPath);
            if (project.Xml.Children.Count > 0) {
                project.Save(projectPath);
                fileContainer.Add(projectPath, targetFolder + Path.GetFileName(projectPath));
                return true;
            }
            return false;
        }

 /*

        public static string NormalizeConditionKey(this Project project, string key) {
            if(!project.HasProject()) {
                return key;
            }

            var configurationsView = project.Lookup().ConfigurationsView;
            var choicesUsed = project.Lookup().ChoicesUsed;


            if(string.IsNullOrEmpty(key)) {
                return string.Empty;
            }

           


            var pivots = configurationsView.GetChildPropertyNames().ToArray();

            var opts = key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {
                '\\', ' '
            }, StringSplitOptions.RemoveEmptyEntries);

            var options = opts.ToList();

            var ordered = new List<string>();

            foreach(var pivot in pivots) {
                foreach(var option in options) {
                    dynamic cfg = configurationsView.GetProperty(pivot);
                    if(cfg.choices.Values.Contains(option)) {
                        ordered.Add(option);
                        options.Remove(option);
                        AddChoiceUsed(choicesUsed, pivot, option);
                        break;
                    }
                }
            }


            if(options.Any()) {
                // went thru one pass,and we had some that didn't resolve.
                // try again, this time cheat if we have to 
                var unfound = options;

                options = opts.ToList();
                foreach(var opt in unfound) {
                    switch(opt.ToLower()) {
                        case "x86":
                        case "win32":
                        case "ia32":
                        case "386":
                            options.Remove(opt);
                            options.Add("Win32");
                            break;

                        case "x64":
                        case "amd64":
                        case "em64t":
                        case "intel64":
                        case "x86-64":
                        case "x86_64":
                            options.Remove(opt);
                            options.Add("x64");
                            break;

                        case "woa":
                        case "arm":
                            options.Remove(opt);
                            options.Add("ARM");
                            break;

                        case "ia64":
                            options.Remove(opt);
                            options.Add("ia64");
                            break;
                    }
                }

                ordered = new List<string>();

                foreach(var pivot in pivots) {
                    foreach(var option in options) {
                        dynamic cfg = configurationsView.GetProperty(pivot);
                        IEnumerable<string> choices = cfg.choices.Values;
                        if(choices.ContainsIgnoreCase(option)) {
                            ordered.Add(option);
                            options.Remove(option);
                            AddChoiceUsed(choicesUsed, pivot, option);
                            break;
                        }
                    }
                }

                if(options.Any()) {
                    // STILL?! bail.
                    throw new ClrPlusException("Unknown configuration choice: {0}".format(options.FirstOrDefault()));
                }
            }
            return ordered.Aggregate((c, e) => c + "\\" + e).Trim('\\');
        }

        private static void AddChoiceUsed(this IDictionary<string, List<string>> choicesUsed, string pivot, string option) {
            if(!choicesUsed.ContainsKey(pivot)) {
                choicesUsed.Add(pivot, new List<string>());
            }
            choicesUsed[pivot].AddUnique(option);
        }
        

        public static string GenerateCondition(this Project project, string key) {
            var view = project.Lookup().ConfigurationsView;
            var pivots = view.GetChildPropertyNames();

            var options = key.Replace(",", "\\").Replace("&", "\\").Split(new char[] {
                '\\', ' '
            }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var conditions = new List<string>();

            foreach(var pivot in pivots) {
                foreach(var option in options) {
                    dynamic cfg = view.GetProperty(pivot);
                    IEnumerable<string> choices = cfg.choices;
                    if(choices.Contains(option)) {
                        if(((View)cfg).GetChildPropertyNames().Contains("key")) {
                            // this is a standard property, us
                            conditions.Add("'$({0})' == '{1}'".format((string)cfg.Key, option));
                        }
                        else {
                            conditions.Add("'$({0}-{1})' == '{2}'".format((string)pivot, view.GetMacroValue("pkgname"), option));
                        }

                        options.Remove(option);
                        break;
                    }
                }
            }

            if(options.Any()) {
                throw new ClrPlusException("Unknown configuration choice: {0}".format(options.FirstOrDefault()));
            }

            return conditions.Aggregate((current, each) => (string.IsNullOrEmpty(current) ? current : (current + " and ")) + each);
        }
        */
#endif 

#if false

            Dictionary<string,string> conditionsForOutput = new Dictionary<string, string>();

            nugetView.AddChildRoutes(
                 "condition".MapTo(_nugetPackages, key => NormalizeOuptutKey(key),
                     "targets".MapTo<NugetPackage>(package => GetTargetsProject(package.PkgRole)),
                     "props".MapTo<NugetPackage>(package => GetPropsProject(package.PkgRole))),

                 "*".MapTo(_nugetPackages, key => NormalizeOuptutKey(key),
                   "targets".MapTo<NugetPackage>(package => GetTargetsProject(package.PkgRole)),
                   "props".MapTo<NugetPackage>(package => GetPropsProject(package.PkgRole)))
                 );
        }

        internal ToRoute CustomProjectRoutes(string key,string location) {
            string packageName;
            string frameworkVariant;

            GetPackageNameAndFramework(key, out packageName, out frameworkVariant);

            var project = _nugetPackages[packageName].GetOrCreateProject(location, frameworkVariant);

            return location.MapTo(project, project.ProjectRoutes());
        }


        private List<string> _outputs = new List<string>();
        private IDictionary<string, string> OuptutPackageConditionCreate() {



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


            
            return new DelegateDictionary<string, string>(
                () => _outputs,
                key => key,
                (s, c) => {
                    if (!_outputs.Contains(s)) {
                        _outputs.Add(s);
                    }
                },
                _outputs.Remove,
                _outputs.Clear);

        }

#endif


#endif