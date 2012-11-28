//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Eric Schultz. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Core.Utility {
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class PathEqualityComparer : IEqualityComparer<string> {
        //public static PathEqualityComparer StatPathComp = new PathEqualityComparer();
        public bool Equals(string x, string y) {
            return string.Compare(Path.GetFullPath(x), Path.GetFullPath(y), StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        public int GetHashCode(string obj) {
            return Path.GetFullPath(obj).ToUpperInvariant().GetHashCode();
        }
    }
}