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

namespace ClrPlus.Powershell.Rest.Commands {
    using System.Management.Automation;

    public class RemoteError {
        public string Message {get; set;}
        public string ExceptionType { get; set; }
        public string ExceptionMessage { get; set; }
        public ErrorCategory Category { get; set; }
    }
}