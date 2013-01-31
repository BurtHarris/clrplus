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
    using Core.Collections;

    public interface IHasMetadata {
        XDictionary<string, RValue> Metadata {get;}
    }

    public interface IPropertyModel {
        IDictionary<string, IPropertyModel> Imports {get;}
        PropertyModel Parent { get; }
        IPropertyModel CreatePropertyModel();

        void AddAlias(string aliasName, Selector aliasReference);
        Alias GetAlias(string aliasName);

        RValue ResolveMacro(string key);

        void SetCollection(Selector path, RValue rvalue);
        void AddToCollection(Selector path, RValue rvalue);
        void SetValue(Selector path, RValue rvalue);

        IPropertyModel this[Selector index] {get;}

        void AddMetadata(Selector collectionSelector, string identifier, RValue rValue);
        void AddMetadata(Selector collectionSelector, string identifier, IDictionary<string, RValue> rValues);

        void AddMetadata(string identifier, RValue rValue);
        void AddMetadata(string identifier, IDictionary<string, RValue> rValues);
    }
}