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

namespace ClrPlus.Scripting.Languages.PropertySheetV3.RValue {
    using System.Collections.Generic;
    using Languages.PropertySheet;

    public interface IValue {
        string GetValue(IValueContext currentContext);
        IEnumerable<string> GetValues(IValueContext currentContext);
        IValueContext Context {get;set;}
        IEnumerable<string> SourceText {get;}
        IEnumerable<SourceLocation> SourceLocations {get;}
    }

    public interface IValueContext {
        IEnumerable<string> GetMacroValues(string macro, object[] items);
        string GetSingleMacroValue(string macro, object[] items);

        string ResolveMacrosInContext(string value, object[] items, bool itemsOnly);
        // IEnumerable<string> TryGetRValueInContext(string property);
    }
}