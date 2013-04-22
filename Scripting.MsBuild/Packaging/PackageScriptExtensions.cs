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

namespace ClrPlus.Scripting.MsBuild.Packaging {
    using System;
    using Core.Extensions;

    internal static class PackageScriptExtensions {
        internal static string SafeToString(this object value, string defaultValue = null) {
            if (value == null) {
                return defaultValue;
            }

            var v = value.ToString();
            return v;
        }

        internal static Uri SafeToUri(this object value) {
            var v = SafeToString(value);
            return v == null ? null : v.ToUri();
        }
    }
}