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
    using Core.Extensions;
    using Languages.PropertySheet;
    using Mapping;

    public class PropertySheet : ObjectNode {
        public StringExtensions.GetMacroValueDelegate PreprocessProperty;
        public StringExtensions.GetMacroValueDelegate PostprocessProperty;
        internal View _view;

        public dynamic View {
            get {
                return _view;
            }
        }

        private Route<object> _backingObjectAccessor;

        public PropertySheet(Route<object> backingObjectAccessor)
            : base() {
            _backingObjectAccessor = backingObjectAccessor;
            // only propertysheets get to have a view
            Root = this;
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
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(File.ReadAllText(filename), TokenizerVersion.V3), this, filename).Parse();
            _view = new View<object>(this, _backingObjectAccessor);
            if (Root._view != null) {
                Root._view.AddChildRoute(Routes);
                foreach(var i in Imports) {
                    AddRoutesForImport(i);
                }
            }
           
        }

        public void ParseText(string propertySheetText, string originalFilename) {
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(propertySheetText, TokenizerVersion.V3), this, originalFilename).Parse();
            _view = new View<object>(this, _backingObjectAccessor);
            if(Root._view != null) {
                Root._view.AddChildRoute(Routes);
                foreach(var i in _imports) {
                    AddRoutesForImport(i);
                }
            }
        }

        public void ImportFile(string filename) {
            var propertySheet = new PropertySheet(this);
            propertySheet.ParseFile(filename);
            _imports.Add(propertySheet);
        }

        public void ImportText(string propertySheetText, string originalFilename) {
            var propertySheet = new PropertySheet(this);
            propertySheet.ParseText(propertySheetText, originalFilename);
            _imports.Add(propertySheet);
        }

        public void Route(params ToRoute[] routes) {
            _view.AddChildRoute(routes);
        }

        public IEnumerable<PropertySheet> AllImportedSheets {
            get {
                return Imports.Concat(Imports.SelectMany(each => each.Imports));
            }
        }

    }
}