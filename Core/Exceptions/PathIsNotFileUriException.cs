//-----------------------------------------------------------------------
// <copyright company="CoApp Project" >
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Core.Exceptions {
    using System;

    public class PathIsNotFileUriException : ClrPlusException {
        public string Path;
        public Uri Uri;

        public PathIsNotFileUriException(string path, Uri uri) {
            Path = path;
            Uri = uri;
        }
    }
}