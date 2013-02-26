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
    using System.IO;
    using Languages.PropertySheet;

    public class PropertySheet : ObjectNode {
        public PropertySheet(object backingObject)
            : base(backingObject, null) {
        }

        public PropertySheet(object backingObject, object routes)
            : base(backingObject, routes) {
        }

        public void ParseFile(string filename) {
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(File.ReadAllText(filename), TokenizerVersion.V3), this, filename).Parse();
            _view.BuildRoutesFromNodes(this);
        }

        public void ParseText(string propertySheetText, string originalFilename) {
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(propertySheetText, TokenizerVersion.V3), this, originalFilename).Parse();
            _view.BuildRoutesFromNodes(this);
        }

        public void ImportFile(string filename) {
            var propertySheet = new PropertySheet(this, null);
            propertySheet.ParseFile(filename);
            _imports.Add(propertySheet);
        }

        public void ImportText(string propertySheetText, string originalFilename) {
            var propertySheet = new PropertySheet(this, null);
            propertySheet.ParseText(propertySheetText, originalFilename);
            _imports.Add(propertySheet);
        }
    }
}