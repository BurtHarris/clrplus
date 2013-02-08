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

namespace ClrPlus.Powershell.Core.Commands {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using ClrPlus.Core.Extensions;
    using Service;
    using ServiceStack.ServiceClient.Web;
    using ServiceStack.ServiceHost;
    using ServiceStack.ServiceInterface.Auth;
    using ServiceStack.Text;

    internal interface IHasSession {
        IAuthSession Session {get;set;}
    }

    public class RestableCmdlet<T> : PSCmdlet, IHasSession , IService<T> where T : RestableCmdlet<T> {
        [Parameter(HelpMessage = "Remote Service URL")]
        public string Remote {get; set;}

        [Parameter(HelpMessage = "Credentials to conenct to service")]
        public PSCredential Credential {get; set;}

        [Parameter(HelpMessage = "Restricted: Remote Session Instance (do not specify)")]
        public IAuthSession Session {get;set;}

        static RestableCmdlet() {
            JsConfig<T>.ExcludePropertyNames = new[] {
                "CommandRuntime", "CurrentPSTransaction", "Stopping", "Remote", "Credential", "CommandOrigin", "Events", "Host", "InvokeCommand", "InvokeProvider" , "JobManager", "MyInvocation", "PagingParameters", "ParameterSetName", "SessionState", "Session"
            };
        }

        protected virtual void ProcessRecordViaRest() {
            var client = new JsonServiceClient(Remote);
            
            if (Credential != null) {
                client.SetCredentials(Credential.UserName, Credential.Password.ToUnsecureString());            
            }
            object[] response = null;

            try {
                // try connecting where the URL is the base URL
                response = client.Send<object[]>((this as T));
                
                if (!response.IsNullOrEmpty()) {
                    foreach (var ob in response) {
                        WriteObject(ob);
                    }
                }

            } catch (WebServiceException wse) {
                throw new Exception("Invalid Remote Service");
            }
        }

        public virtual object Execute(T cmdlet) {
            // credential gets set by the filter. 

            var restCommand = RestService.ReverseLookup[cmdlet.GetType()];
            
            using(var dps = new DynamicPowershell(RestService.RunspacePool)) {
                return dps.Invoke(restCommand.Name, _persistableElements, cmdlet, restCommand.DefaultParameters, restCommand.ForcedParameters);
            }
        }

        private PersistablePropertyInformation[] _persistableElements = typeof(T).GetPersistableElements().Where(p => p.Name == "Session" || !JsConfig<T>.ExcludePropertyNames.Contains(p.Name)).ToArray();

        private IEnumerable<KeyValuePair<string, object>> PropertiesAsDictionary(object obj) {
            return _persistableElements.Select(p => new KeyValuePair<string, object>(p.Name, p.GetValue(obj, null)));
        }
    }
}