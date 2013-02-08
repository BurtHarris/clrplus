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
    using EXCEPINFO = System.Runtime.InteropServices.ComTypes.EXCEPINFO;

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("BB1A2AE2-A4F9-11cf-8F20-00805F2CD064")]
    internal interface IActiveScriptParse {
        void InitNew();
        void AddScriptlet(string defaultName, string code, string itemName, string subItemName, string eventName, string delimiter, uint sourceContextCookie, uint startingLineNumber, uint flags, out string name, out EXCEPINFO info);
        void ParseScriptText(string code, string itemName, IntPtr context, string delimiter, uint sourceContextCookie, uint startingLineNumber, uint flags, IntPtr result, out EXCEPINFO info);
    }
}