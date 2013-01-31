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
//-----------------------------------------------------------------------

namespace ClrPlus.Powershell.Rest.Commands {
    using System.Management.Automation;
    using ClrPlus.Core.Configuration;
    using ClrPlus.Core.Extensions;

    [Cmdlet(VerbsCommon.Set, "DefaultRemoteService")]
    public class SetDefaultRemoteService : Cmdlet {
        private static string _defaultServiceUrl;
        private static PSCredential _defaultCredential;

        [Parameter(HelpMessage = "Remote Service URL")]
        public string ServiceUrl {get; set;}

        [Parameter(HelpMessage = "Credentials to user for remote service")]
        public PSCredential Credential {get; set;}

        [Parameter(HelpMessage = "Encrypt and store defaults in the user registry")]
        public SwitchParameter Remember {get; set;}

        [Parameter(HelpMessage = "Remove any defaults stored in the user registry")]
        public SwitchParameter Forget {get; set;}

        public static string DefaultServiceUrl {
            get {
                if (string.IsNullOrEmpty(_defaultServiceUrl)) {
                    return RegistryView.User[@"Software\ClrPlus\RemoteCmdlet#DefaultServiceUrl"].EncryptedStringValue;
                }
                return _defaultServiceUrl;
            }
            set {
                _defaultServiceUrl = value;
            }
        }

        public static PSCredential DefaultCredential {
            get {
                if (_defaultCredential == null) {
                    var user = RegistryView.User[@"Software\ClrPlus\RemoteCmdlet#DefaultUser"].EncryptedStringValue;
                    var pass = RegistryView.User[@"Software\ClrPlus\RemoteCmdlet#DefaultPassword"].EncryptedStringValue;
                    return string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) ? null : new PSCredential(user, pass.ToSecureString());
                }
                return _defaultCredential;
            }
            set {
                _defaultCredential = value;
            }
        }

        protected override void ProcessRecord() {
            if (Forget) {
                DefaultServiceUrl = null;
                DefaultCredential = null;
                RegistryView.User[@"Software\ClrPlus\RemoteCmdlet"].DeleteValues();
                return;
            }

            DefaultServiceUrl = ServiceUrl;
            DefaultCredential = Credential;

            if (Remember) {
                RegistryView.User[@"Software\ClrPlus\RemoteCmdlet"].DeleteValues();

                RegistryView.User[@"Software\ClrPlus\RemoteCmdlet#DefaultServiceUrl"].EncryptedStringValue = ServiceUrl;
                if (Credential != null) {
                    RegistryView.User[@"Software\ClrPlus\RemoteCmdlet#DefaultUser"].EncryptedStringValue = Credential.UserName;
                    RegistryView.User[@"Software\ClrPlus\RemoteCmdlet#DefaultPassword"].EncryptedStringValue = Credential.Password.ToUnsecureString();
                }
            }
        }
    }
}