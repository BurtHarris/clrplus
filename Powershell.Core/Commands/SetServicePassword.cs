using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Powershell.Core.Commands {
    using System.Management.Automation;
    using Service;

    [Cmdlet(VerbsCommon.Set, "ServicePassword")]
    public class SetServicePassword : RestableCmdlet<SetServicePassword> {

        [Parameter(HelpMessage = "New Password for account.",Mandatory=true)]
        public string NewPassword {get; set;}

        protected override void ProcessRecord() {
            // must use this to support processing record remotely.
            if (!string.IsNullOrEmpty(Remote)) {
                ProcessRecordViaRest();
                return;
            }

            if (Session == null) {
                throw new Exception("No remote session object.");
            }

            
            if (string.IsNullOrEmpty(NewPassword)) {
                throw new Exception("Invalid Password.");
            }

            if (RestService.ChangePassword(Session.UserAuthName, NewPassword)) {
                WriteObject("Password Successfully Changed");
            } else {
                WriteObject("Failed.");
            }
        }


    }
}
