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
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using Exceptions;
    using Windows.Api;

    public static class CommandLine {
        public static IEnumerable<string> SplitArgs(string unsplitArgumentLine) {
            if (Information.CanUseWin32API) {
                int numberOfArgs;

                var ptrToSplitArgs = Kernel32.CommandLineToArgvW(unsplitArgumentLine, out numberOfArgs);

                // CommandLineToArgvW returns NULL upon failure.
                if (ptrToSplitArgs == IntPtr.Zero) {
                    throw new ArgumentException("Unable to split argument.", new Win32Exception());
                }

                // Make sure the memory ptrToSplitArgs to is freed, even upon failure.
                try {
                    var splitArgs = new string[numberOfArgs];

                    // ptrToSplitArgs is an array of pointers to null terminated Unicode strings.
                    // Copy each of these strings into our split argument array.
                    for (var i = 0; i < numberOfArgs; i++) {
                        splitArgs[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(ptrToSplitArgs, i*IntPtr.Size));
                    }

                    return splitArgs;
                } finally {
                    // Free memory obtained by CommandLineToArgW.
                    Kernel32.LocalFree(ptrToSplitArgs);
                }
            }

            throw new UnsupportedPlatformException();
        }
    }
}