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

namespace ClrPlus.Powershell.Provider.Utility {
    using System;
    using System.Linq;
    using Core.Extensions;

    public static class PathExtensions {
        private static readonly char[] Slashes = new[] {
            '\\', '/'
        };

        private const char Slash = '\\';

        public static string GetRelativePath(this string rootPath, string childPath) {
            var rpSegments = (rootPath ?? string.Empty).UrlDecode().Split(Slashes, StringSplitOptions.RemoveEmptyEntries);
            var chSegments = (childPath ?? string.Empty).UrlDecode().Split(Slashes, StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < rpSegments.Length; i++) {
                if (!rpSegments[i].Equals(chSegments[i], StringComparison.CurrentCultureIgnoreCase)) {
                    return chSegments.Skip(i).Aggregate((current, each) => current = current + Slash + each);
                }
            }
            return chSegments.Skip(rpSegments.Length).Aggregate((current, each) => current = current + Slash + each);
        }
    }
}