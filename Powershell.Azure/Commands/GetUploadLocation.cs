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

namespace ClrPlus.Powershell.Azure.Commands {
    using System.Management.Automation;
    using ClrPlus.Core.Extensions;
    using Core.Commands;

    [Cmdlet(VerbsCommon.Get, "UploadLocation")]
    public class GetUploadLocation : RestableCmdlet<GetUploadLocation> {
        [Parameter]
        public string Name {get; set;}

        protected override void ProcessRecord() {
            // must use this to support processing record remotely.
            if (!string.IsNullOrEmpty(Remote)) {
                ProcessRecordViaRest();
                return;
            }
            /*
            var v = SessionState.Drive;
            var x = v.Current;
            var c = x.CurrentLocation;
            var pipe = Runspace.DefaultRunspace.CreateNestedPipeline();
            Runspace.DefaultRunspace.Create

            // Runspace.DefaultRunspace.CreateNestedPipeline("get-psdriveinfo", false);
            */

            // continue as normal.
            // WriteObject("Hello there {0}".format(Name));
            WriteObject(new {
                Message = "Hello there {0}".format(Name),
                Age = 41,
            });
        }
    }
}