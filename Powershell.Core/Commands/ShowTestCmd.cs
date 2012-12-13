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
    using System;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using ClrPlus.Core.Extensions;

    [Cmdlet(VerbsCommon.Show, "Test")]
    public class ShowTestCmd : RestableCmdlet<ShowTestCmd> {
        [Parameter(HelpMessage = "foo")]
        public string Foo {get; set;}

        protected override void ProcessRecord() {
            // must use this to support processing record remotely.
            if (!string.IsNullOrEmpty(Remote)) {
                ProcessRecordViaRest();
                return;
            }

            using (var powershell = Runspace.DefaultRunspace.GetDynamicPowershell()) {
                foreach (var item in powershell.dir(@"c:\")) {
                    Console.WriteLine("item: {0}", item);
                }
            }

            WriteObject(new {
                Message = "Hello there {0}".format(Foo),
                Age = 41,
            });

        }
    }
}