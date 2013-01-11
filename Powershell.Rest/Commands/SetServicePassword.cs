namespace ClrPlus.Powershell.Rest.Commands {
    using System;
    using System.Management.Automation;
    using ClrPlus.Powershell.Core.Service;

    [Cmdlet(VerbsCommon.Set, "ServicePassword")]
    public class SetServicePassword : RestableCmdlet<SetServicePassword> {

        [Parameter(HelpMessage = "New Password for account.",Mandatory=true)]
        public string NewPassword {get; set;}

        protected override void ProcessRecord() {
            // must use this to support processing record remotely.
            if (Remote) {
                ProcessRecordViaRest();
                return;
            }

            if (Session == null) {
                WriteObject(new Exception("No remote session object."));
            }

            if (string.IsNullOrEmpty(NewPassword)) {
                WriteObject(new Exception("Invalid Password."));
            }

            if (RestService.ChangePassword(Session.UserAuthName, NewPassword)) {
                WriteObject("Password Successfully Changed");
                if (Session.HasRole("password_must_be_changed")) {
                    Session.Roles.Remove("password_must_be_changed");
                }
            } else {
                WriteObject("Failed.");
            }
        }


    }
}
