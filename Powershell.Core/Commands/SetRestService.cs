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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Extensions;
    using Scripting.Languages.PropertySheet;
    using Service;

    [Cmdlet(VerbsCommon.Set, "RestService")]
    public class SetRestService : Cmdlet {
        [Parameter]
        public SwitchParameter Auto {get; set;}

        [Parameter]
        public string Config {get; set;}

        [Parameter]
        public string[] ListenOn {get; set;}

        protected override void ProcessRecord() {
            if (Auto) {
                Config = "restservice.properties";
            }
            
            if (!string.IsNullOrEmpty(Config)) {
                RestService.ConfigFile = Config;

                if (!ListenOn.IsNullOrEmpty()) {
                    RestService.AddListeners(ListenOn);
                }

            } else {
                RestService.AddListeners(ListenOn);
            }
        }
    }
}