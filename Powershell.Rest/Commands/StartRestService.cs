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

namespace ClrPlus.Powershell.Rest.Commands {
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Powershell.Core.Service;
    using Core;

    [Cmdlet("Start", "RestService")]
    public class StartRestService : Cmdlet {
        [Parameter]
        public SwitchParameter Auto {
            get;
            set;
        }

        [Parameter]
        public SwitchParameter Reset {
            get;
            set;
        }


        [Parameter]
        public string Config {
            get;
            set;
        }

        [Parameter]
        public string[] ListenOn {
            get;
            set;
        }

        protected override void ProcessRecord() {
            if (Reset) {
                // wipes all the current listeners out first, before loading the config file
                RestService.ResetService();
            }

            if (Auto) {
                Config = "restservice.properties";
            }

            if (!string.IsNullOrEmpty(Config)) {
                RestService.ConfigFile = Config;
            }

            // add on any at the prompt.
            if (!ListenOn.IsNullOrEmpty()) {
                RestService.AddListeners(ListenOn);
            }


            IEnumerable<string> activeModules;
            using(var ps = Runspace.DefaultRunspace.Dynamic()) {
                IEnumerable<object> modules = ps.GetModule();
                activeModules = modules.Select(each => each as PSModuleInfo).Where(each => each != null).Select(each => each.Path).ToArray();
            }

            RestService.StartService(activeModules);
        }
    }
}   