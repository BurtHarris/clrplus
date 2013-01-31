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

namespace ClrPlus.Scripting.Utility {
    using System;
    using System.Runtime.InteropServices;

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("71ee5b20-fb04-11d1-b3a8-00a0c911e8b2")]
    internal interface IActiveScriptParseProcedure {
        void ParseProcedureText(string code, string formalParams, string procedureName, string itemName, IntPtr context, string delimiter, int sourceContextCookie, uint startingLineNumber, uint flags, [Out, MarshalAs(UnmanagedType.IDispatch)] out object ppdisp);
    }
}