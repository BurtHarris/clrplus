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
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using ClrPlus.Core.Extensions;
    using Service;
    using ServiceStack.ServiceClient.Web;
    using ServiceStack.ServiceHost;
    using ServiceStack.Text;

    public class Response {
        public Result[] Results {get; set;}
    }

    public class RestableCmdlet<T> : PSCmdlet, IService<T> where T : RestableCmdlet<T> {
        [Parameter(HelpMessage = "Remote Service URL")]
        public string Remote {get; set;}

        [Parameter(HelpMessage = "Credentials to conenct to service")]
        public PSCredential Credential {get; set;}

        static RestableCmdlet() {
            JsConfig<T>.ExcludePropertyNames = new[] {
                "CommandRuntime", "CurrentPSTransaction", "Stopping", "Remote", "Credential", "CommandOrigin", "Events", "Host", "InvokeCommand", "InvokeProvider" , "JobManager", "MyInvocation", "PagingParameters", "ParameterSetName", "SessionState"
            };
        }

        protected virtual void ProcessRecordViaRest() {
            var client = new JsonServiceClient(Remote);
            var response = client.Send<object[]>((this as T));
            foreach(var ob in response) {
                WriteObject(ob);
            }
        }

        public virtual object Execute(T cmdlet) {
            var name = Rest.Services.ReverseLookup[cmdlet.GetType()];
            

            using(var dps = new DynamicPowershell(Rest.Services.RunspacePool)) {
                return dps.Invoke(name, _persistableElements, cmdlet);
#if false
                var result = dps.Invoke(name, _persistableElements, cmdlet);
                
                var r = result.ToArray();

                // var rs = r.Select(each => each as Result).ToList();
                // var rsp = new Response {
                    // Results = (Result[])r.ToArrayOfType(typeof (Result))
                // };

                return r;
                // JsConfig.IncludeTypeInfo = true;
                // return rs;
#endif
            }
        }

        private PersistablePropertyInformation[] _persistableElements = typeof (T).GetPersistableElements().Where(p => !JsConfig<T>.ExcludePropertyNames.Contains(p.Name)).ToArray();

        private IEnumerable<KeyValuePair<string, object>> PropertiesAsDictionary(object obj) {
            return _persistableElements.Select(p => new KeyValuePair<string, object>(p.Name, p.GetValue(obj, null)));
        }
    }
}