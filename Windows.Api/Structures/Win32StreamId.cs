﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack . All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Windows.Api.Structures {
    using System.Runtime.InteropServices;

    [StructLayout(LayoutKind.Sequential)]
    public struct Win32StreamId {
        public readonly int StreamId;
        public readonly int StreamAttributes;
        public long Size;
        public readonly int StreamNameSize;
    }
}