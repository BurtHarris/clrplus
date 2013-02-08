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

namespace ClrPlus.Powershell.Core.Service {
    using System.Collections.Generic;

    internal class RestCommand {
        public string Name;
        public string[] Roles;
        public string PublishAs;
        public Dictionary<string, object> DefaultParameters;
        public Dictionary<string, object> ForcedParameters;
    }
}