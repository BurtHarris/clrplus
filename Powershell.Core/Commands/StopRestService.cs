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
//-----------------------------------------------------------------------

namespace ClrPlus.Powershell.Core.Commands {
    using System.Management.Automation;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using Service;
    using ServiceStack.Text;

    [Cmdlet("Stop", "RestService")]
    public class StopRestService : Cmdlet {
        [Parameter]
        public SwitchParameter All {get; set;}

        [Parameter]
        public SwitchParameter Delete {get; set;}

        [Parameter]
        public string Name {get; set;}

        protected override void ProcessRecord() {
            var d = (bool)Delete;

            if (All) {
                foreach (var instance in RestAppHost.Instances.Keys.ToArray()) {
                    RestAppHost.Instances[instance].Stop();
                    if (d) {
                        RestAppHost.Instances[instance].Dispose();
                        RestAppHost.Instances.Remove(instance);
                    }
                    WriteObject("Stopping REST Service '{0}'".format(instance));
                }
            } else {
                var instance = RestAppHost.Instances[Name.ToLower()];
                if (instance == null) {
                    throw new ClrPlusException("No rest service by name of '{0}'".format(Name));
                }
                instance.Stop();
                if (d) {
                    instance.Dispose();
                    RestAppHost.Instances.Remove(Name.ToLower());
                }

                WriteObject("Stopping REST Service '{0}'".format(Name));
            }
        }
    }
}