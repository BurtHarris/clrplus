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

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("DB01A1E3-A42B-11cf-8F20-00805F2CD064")]
    internal interface IActiveScriptSite {
        void GetLCID(out uint id);
        void GetItemInfo(string pstrName, uint dwReturnMask, [Out, MarshalAs(UnmanagedType.IUnknown)] out object item, IntPtr ppti);
        void GetDocVersionString(out string v);
        void OnScriptTerminate(ref object result, ref EXCEPINFO info);
        void OnStateChange(uint state);
        void OnScriptError(IActiveScriptError err);
        void OnEnterScript();
        void OnLeaveScript();
    }
}