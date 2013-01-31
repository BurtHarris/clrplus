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
    using System.Runtime.InteropServices;
    using EXCEPINFO = System.Runtime.InteropServices.ComTypes.EXCEPINFO;

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("EAE1BA61-A4ED-11CF-8F20-00805F2CD064")]
    public interface IActiveScriptError {
        void GetExceptionInfo(out EXCEPINFO excepinfo);
        void GetSourcePosition(out int sourceContext, out int pulLineNumber, out int plCharacterPosition);
        void GetSourceLineText(out string sourceLine);
    }
}