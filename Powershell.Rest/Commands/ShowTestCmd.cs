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

namespace ClrPlus.Powershell.Rest.Commands {
    using System;
    using System.Management.Automation;
    using ClrPlus.Core.Extensions;

    public class Result {
        public String Message {get; set;}
        public int Age { get; set; }
    }

    [Cmdlet(VerbsCommon.Show, "Test")]
    public class ShowTestCmd : RestableCmdlet<ShowTestCmd> {
        [Parameter(HelpMessage = "foo")]
        public string Foo {get; set;}

        [Parameter(HelpMessage = "p1")]
        public string P1 { get; set; }

        [Parameter(HelpMessage = "p2")]
        public string P2 { get; set; }

        [Parameter(HelpMessage = "p3")]
        public string[] P3 { get; set; }

        protected override void ProcessRecord() {
            // must use this to support processing record remotely.
            if (Remote) {
                ProcessRecordViaRest();
                return;
            }

            if (Session != null) {
                WriteObject("You are authenticated as {0}".format(Session.UserAuthName));
            }

            if (P3 == null) {
                WriteObject(new Result {
                    Message = "Nothing!",
                    Age = -1,
                });
            } else {
                foreach (var kid in P3) {
                    WriteObject(new Result {
                        Message = "Person: {0} {1} has a child {2}".format(P1, P2, kid),
                        Age = 41,
                    });
                }
            }
        }
    }
}