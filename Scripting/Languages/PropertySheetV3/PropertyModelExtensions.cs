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
    using Core.Exceptions;
    using PropertySheet;

    public static class PropertyModelExtensions {
        public static void ParseFile(this IModel model, string filename) {
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(File.ReadAllText(filename), TokenizerVersion.V3), model, filename).Parse();
        }

        public static void SaveFile(this IModel model, string filename) {
            var text = GetPropertySheetSource(model);
            File.WriteAllText(filename, text);
        }

        public static void ParseText(this IModel model, string propertySheetText, string originalFilename) {
            new PropertySheetParser(PropertySheetTokenizer.Tokenize(propertySheetText,TokenizerVersion.V3), model, originalFilename).Parse();
        }

        public static void ImportFile(this IModel model, string filename) {
            ParseFile( model.Imports[filename], filename);
        }
        public static void ImportText(this IModel model, string propertySheetText, string originalFilename) {
            ParseText(model.Imports[originalFilename], propertySheetText, originalFilename);
        }

        public static string GetPropertySheetSource(this IModel model) {
            return "";
        }

        public static Selector ResolveAliasesInPath(this IModel model, Selector path) {
            return path;
            //return null;
        }

        internal static string GetAliasForPath(this IModel model, Selector path) {
            return null;
        }

        public static IEnumerable<IModel> GetImportedPropertySheets(this IModel rootModel) {
            return ((IDictionary<string, IModel>)rootModel.Imports).Values;
        }

        public static INode GetView(this IModel rootModel) {
            // this only works from the Root; Access should go thru the resolved root view.
            if(rootModel != rootModel.Root) {
                throw new ClrPlusException("Access datamodel view thru the root object only.");
            }
            var result = new ViewNode(rootModel);

            foreach (var sheet in GetImportedPropertySheets(rootModel)) {
                sheet.AddChildrenToViewNode(null, result);
            }

            foreach (var child in rootModel.Keys) {
                
            }

            return result;
        }

        internal static void AddChildrenToViewNode(this INode node, Selector parentSelector, ViewNode viewNode) {
            foreach (var key in node.Keys) {
                // foreach()
                // viewNode[key].Properties[]
            }
        }
    }
}