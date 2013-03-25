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

namespace ClrPlus.Powershell.Provider.Base {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Management.Automation.Provider;

    public interface ILocation {
        string Name {get;}
        string AbsolutePath {get;}
        string Url {get;}
        string Type {get;}

        long Length {get;}
        DateTime TimeStamp {get;}

        bool Exists {get;}
        bool IsFile {get;}
        bool IsFileContainer {get;}
        bool IsItemContainer {get;}

        IEnumerable<ILocation> GetDirectories(bool recurse);
        IEnumerable<ILocation> GetFiles(bool recurse);

        void Delete(bool recurse);

        Stream Open(FileMode mode);

        ILocation GetChildLocation(string relativePath);

        IContentReader GetContentReader();
        IContentWriter GetContentWriter();
        void ClearContent();

        ILocation NewItem(string type, object newItemValue);
        ILocation Rename(string newName);
        ILocation Move(ILocation newLocation);
        IEnumerable<ILocation> Copy(ILocation newLocation, bool recurse);
    }
}