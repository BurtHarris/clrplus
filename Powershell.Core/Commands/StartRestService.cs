//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//------------------------------------------------------------  -----------

namespace ClrPlus.Powershell.Core.Commands {
    using System.Management.Automation;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using Service;

    [Cmdlet("Start", "RestService")]
    public class StartRestService : Cmdlet {
        [Parameter]
        public SwitchParameter All {get; set;}

        [Parameter]
        public string Name {get; set;}

        protected override void ProcessRecord() {
            if (All) {
                foreach (var instance in RestAppHost.Instances.Keys) {
                    RestAppHost.Instances[instance].Start();
                    WriteObject("Started REST Service '{0}'".format(instance));
                }
            } else {
                var instance = RestAppHost.Instances[Name.ToLower()];
                if (instance == null) {
                    throw new ClrPlusException("No rest service by name of '{0}'".format(Name));
                }
                instance.Start();
                WriteObject("Started REST Service '{0}'".format(Name));
            }
        }
    }
}