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

namespace ClrPlus.Scripting.Languages.PropertySheetV3 {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Core.Extensions;
    using Languages.PropertySheet;
    using Mapping;
    using Platform;

    public delegate bool SourceWarning(string messageCode, IEnumerable<SourceLocation> sourceLocation, string message, params object[] args);
    public delegate bool SourceError(string messageCode, IEnumerable<SourceLocation> sourceLocation, string message, params object[] args);
    public delegate bool SourceDebug(string messageCode, IEnumerable<SourceLocation> sourceLocation, string message, params object[] args);
    

    public class PropertySheet : ObjectNode {
        internal string FullPath;

        internal PropertySheet(RootPropertySheet root)
            : base(root) {
            // used by imported sheets to bind themselves to the right root object.
        }

        public override View CurrentView {
            get {
                return Parent.CurrentView;
            }
        }

        public override RootPropertySheet Root {
            get {
                return Parent as RootPropertySheet;
            }
        }

        internal PropertySheet() {
            // called only by RootPropertySheet.
        }

        public virtual void ImportFile(string filename) {
            Root.ImportFile(filename);
        }

        public virtual void ImportText(string propertySheetText, string originalFilename) {
            Root.ImportText(propertySheetText, originalFilename);
        }

        public virtual void ParseFile(string filename) {
            FullPath = filename.GetFullPath();

            if(!File.Exists(FullPath)) {
                throw new FileNotFoundException("Can't find property sheet file '{0}'".format(filename), FullPath);
            }

            ParseText(File.ReadAllText(FullPath), FullPath);
        }

        public virtual void ParseText(string propertySheetText, string originalFilename) {
            FullPath = originalFilename;
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(propertySheetText, TokenizerVersion.V3), this, originalFilename).Parse();
        }

        public void SaveFile(string filename = null) {
            filename = filename ?? FullPath;
            File.WriteAllText(filename, GetPropertySheetSource());
        }

        public string GetPropertySheetSource() {
            return "";
        }


    }

    public class RootPropertySheet : PropertySheet {
        private View _view;

        private List<PropertySheet> _imports;
        private Route<object> _backingObjectAccessor;

        public IEnumerable<PropertySheet> Imports {
            get {
                return _imports ?? Enumerable.Empty<PropertySheet>();
            }
        }

        public dynamic View {
            get {
                return _view;
            }
        }

        public override View CurrentView {
            get {
                return _view;
            }
        }

        public override RootPropertySheet Root {
            get {
                return Parent == null ? this : Parent.Root ;
            }
        }

        public RootPropertySheet(Route<object> backingObjectAccessor) {
            _backingObjectAccessor = backingObjectAccessor;
            // only propertysheets get to have a view
            Parent = null;
            _imports = new List<PropertySheet>();
            _view = new View<object>(this, _backingObjectAccessor);
            _view.SourceLocations = _view.SourceLocations.UnionSingleItem(new SourceLocation {
                SourceFile = FullPath
            });
        }

        public RootPropertySheet(): this((Func<object>)null) {
        }

        public RootPropertySheet(object backingObject)
            : this((parent) => backingObject) {
        }
   
        public override void ParseText(string propertySheetText, string originalFilename) {
            base.ParseText(propertySheetText, originalFilename);
            AddChildRoutes(Routes);
            _view.InitializeAtRootLevel(this);
        }

        public override void ImportFile(string filename) {
            if (!Path.IsPathRooted(filename)) {
                // only try to search for the file if we're not passed an absolute location.
                var currentDir = Path.GetDirectoryName(FullPath);
                if (filename.IndexOfAny(new[] { '/', '\\' }) > -1) {
                    // does have a slash, is meant to be a relative path from the parent sheet.
                    
                    var fullPath = Path.Combine(currentDir, filename);
                    if (!File.Exists(fullPath)) {
                        throw new FileNotFoundException("Unable to locate imported property sheet '{0}'".format(filename), fullPath);
                    }
                    filename = fullPath;
                } else {
                    // just a filename. Scan up the tree and into known locations for it.
                    var paths = filename.GetAllCustomFilePaths(currentDir);

                    var chkPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),filename);
                    if(File.Exists(chkPath)) {
                        paths = paths.ConcatSingleItem(chkPath);
                    }

                    chkPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "etc" ,filename);
                    if (File.Exists(chkPath)) {
                        paths = paths.ConcatSingleItem(chkPath);
                    }
                    foreach (var i in paths) {
                        ImportFile(i);
                    }
                    return;
                }
            }

            if (Root._imports.Any(each => each.FullPath.Equals(filename, StringComparison.CurrentCulture))) {
                return;
            }

            // filename is now the absolute path.
            var importedSheet = new PropertySheet(this);
            importedSheet.ParseFile(filename);
            _imports.Add(importedSheet);
            AddChildRoutes(importedSheet.Routes);
            _view.InitializeAtRootLevel(importedSheet);
        }

        public override void ImportText(string propertySheetText, string originalFilename) {
            if(Root._imports.Any(each => each.FullPath.Equals(originalFilename, StringComparison.CurrentCulture))) {
                return;
            }

            var importedSheet = new RootPropertySheet(this);
            importedSheet.ParseText(propertySheetText, originalFilename);
            _imports.Add(importedSheet);
            AddChildRoutes(importedSheet.Routes);
            _view.InitializeAtRootLevel(importedSheet);
        }

        public void AddChildRoutes(IEnumerable<ToRoute> routes) {
            _view.AddChildRoutes(routes);
        }

        public void AddMacro(string name, string value) {
            _view.AddMacro(name, value);
        }

        public void CopyToModel() {
            _view.CopyToModel();
        }
    }
}