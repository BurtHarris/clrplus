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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using PropertySheet;

    public static class PropertyModelExtensions {
        public static void ParseFile(this IPropertyModel model, string filename) {
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(File.ReadAllText(filename), TokenizerVersion.V3), model, filename).Parse();
        }

        public static void SaveFile(this IPropertyModel model, string filename) {
            var text = GetPropertySheetSource(model);
            File.WriteAllText(filename, text);
        }

        public static void ParseText(this IPropertyModel model, string propertySheetText, string originalFilename) {
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(propertySheetText,TokenizerVersion.V3), model, originalFilename).Parse();
        }

        public static void ImportFile(this IPropertyModel model, string filename) {
            var imported = model.CreatePropertyModel();
            ParseFile( imported, filename);
            model.Imports.Add( filename , imported);
        }
        public static void ImportText(this IPropertyModel model, string propertySheetText, string originalFilename) {
            var imported = model.CreatePropertyModel();
            ParseFile(imported, propertySheetText);
            model.Imports.Add(originalFilename, imported);
        }

        public static string GetPropertySheetSource(this IPropertyModel model) {
            return "";
        }

        public static Selector ResolveAliasesInPath(this IPropertyModel model, Selector path) {
            return path;
            //return null;
        }

        internal static string GetAliasForPath(this IPropertyModel model, Selector path) {
            return null;
        }

    }
}