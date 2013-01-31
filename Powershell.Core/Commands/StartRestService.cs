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
//------------------------------------------------------------  -----------

namespace ClrPlus.Powershell.Core.Commands {
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using Service;

    [Cmdlet("Start", "RestService")]
    public class StartRestService : Cmdlet {
        protected override void ProcessRecord() {
            IEnumerable<string> activeModules;
            using(var ps = Runspace.DefaultRunspace.Dynamic()) {
                IEnumerable<object> modules = ps.GetModule();
                activeModules = modules.Select(each => each as PSModuleInfo).Where(each => each != null).Select(each => each.Path).ToArray();
            }

            RestService.StartService(activeModules);
        }
    }
}