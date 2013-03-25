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
    using Path = Utility.Path;

    public abstract class Location : ILocation {
        protected static readonly char[] Wildcards = new[] {
            '*', '?'
        };

        protected Path Path {get; set;}

        public abstract string Name {get;}
        public abstract string AbsolutePath {get;}
        public abstract string Url {get;}
        public abstract string Type {get;}
        public abstract long Length {get;}
        public abstract DateTime TimeStamp {get;}
        public abstract bool Exists {get;}
        public abstract bool IsFile {get;}
        public abstract bool IsFileContainer {get;}
        public abstract bool IsItemContainer {get;}
        public abstract IEnumerable<ILocation> GetDirectories(bool recurse);
        public abstract IEnumerable<ILocation> GetFiles(bool recurse);

        public abstract void Delete(bool recurse);

        public abstract IContentReader GetContentReader();
        public abstract IContentWriter GetContentWriter();
        public abstract void ClearContent();

        public abstract ILocation NewItem(string type, object newItemValue);

        public abstract ILocation Rename(string newName);
        public abstract ILocation Move(ILocation newLocation);
        public abstract IEnumerable<ILocation> Copy(ILocation newLocation, bool recurse);

        public abstract Stream Open(FileMode mode);

        public abstract ILocation GetChildLocation(string relativePath);

        public override string ToString() {
            return Name;
        }
    }
}