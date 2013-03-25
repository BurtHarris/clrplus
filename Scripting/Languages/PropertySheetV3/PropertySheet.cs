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
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Core.Extensions;
    using Languages.PropertySheet;
    using Mapping;
    using Platform;

    public class PropertySheet : ObjectNode {
        public StringExtensions.GetMacroValueDelegate PreprocessProperty;
        public StringExtensions.GetMacroValueDelegate PostprocessProperty;
        internal View _view;
        public string _fullPath;
        protected List<PropertySheet> _imports;
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
                return (Parent == null ? _view : Parent.CurrentView).GetChild(Selector);
            }
        }

        public override PropertySheet Root {
            get {
                return Parent == null ? this : Parent as PropertySheet;
            }
        }

        public PropertySheet(Route<object> backingObjectAccessor)
            : base() {
            _backingObjectAccessor = backingObjectAccessor;
            // only propertysheets get to have a view
            Parent = null;
            _imports = new List<PropertySheet>();
        }

        public PropertySheet(): this((Func<object>)null) {
        }

        public PropertySheet(object backingObject)
            : this((parent) => backingObject) {
        }

        private PropertySheet(PropertySheet root)
            : base(root) {
            // used by imported sheets to bind themselves to the right root object.
        }
       
        private void AddRoutesForImport(PropertySheet importedSheet) {
            Root._view.AddChildRoute(importedSheet.Routes);
            
            foreach(var i in importedSheet.Imports) {
                AddRoutesForImport(i);
            }
        }

        public void ParseFile(string filename) {
            _fullPath = filename.GetFullPath();
            if (!File.Exists(_fullPath)) {
                throw new FileNotFoundException("Can't find property sheet file '{0}'".format(filename), _fullPath);
            }

            new PropertySheetParser(PropertySheetTokenizer.Tokenize(File.ReadAllText(_fullPath), TokenizerVersion.V3), this, _fullPath).Parse();
            _view = new View<object>(this, _backingObjectAccessor);
            if (Root._view != null) {
                foreach(var i in Imports) {
                    AddRoutesForImport(i);
                }
                Root._view.AddChildRoute(Routes);
            }
        }

        public void ParseText(string propertySheetText, string originalFilename) {
            _fullPath = originalFilename;

            new PropertySheetParser(PropertySheetTokenizer.Tokenize(propertySheetText, TokenizerVersion.V3), this, originalFilename).Parse();
            _view = new View<object>(this, _backingObjectAccessor);
            if(Root._view != null) {
                foreach(var i in Imports) {
                    AddRoutesForImport(i);
                }
                Root._view.AddChildRoute(Routes);
            }
        }

        public void ImportFile(string filename) {
            if (!Path.IsPathRooted(filename)) {
                // only try to search for the file if we're not passed an absolute location.
                var currentDir = Path.GetDirectoryName(_fullPath);
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

            if (Root._imports.Any(each => each._fullPath.Equals(filename, StringComparison.CurrentCulture))) {
                return;
            }

            // filename is now the absolute path.
            var propertySheet = new PropertySheet(this);
            propertySheet.ParseFile(filename);
            Root._imports.Add(propertySheet);
        }

        public void ImportText(string propertySheetText, string originalFilename) {
            if(Root._imports.Any(each => each._fullPath.Equals(originalFilename, StringComparison.CurrentCulture))) {
                return;
            }

            var propertySheet = new PropertySheet(this);
            propertySheet.ParseText(propertySheetText, originalFilename);
            Root._imports.Add(propertySheet);
        }

        public void Route(params ToRoute[] routes) {
            _view.AddChildRoute(routes);
        }
        public void Route(IEnumerable<ToRoute> routes) {
            _view.AddChildRoute(routes);
        }
        public IEnumerable<PropertySheet> AllImportedSheets {
            get {
                return Imports;// .Concat(Imports.SelectMany(each => each.Imports));
            }
        }

        public void AddMacro(string name, string value) {
            _view.AddMacro(name, value);
        }

        public void CopyToModel() {
            _view.CopyToModel();
        }
    }
}