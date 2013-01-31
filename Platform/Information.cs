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

namespace ClrPlus.Platform {
    using System;

    public static class Information {
        public static bool IsWindows {
            get {
                return ((int)Environment.OSVersion.Platform) < 4;
            }
        }

        /// <summary>
        ///     Use this to pivot on if it's OK to call Win32 APIs.
        /// </summary>
        public static bool CanUseWin32API {
            get {
                return IsWindows;
            }
        }
    }
}