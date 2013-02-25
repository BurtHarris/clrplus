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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Powershell.Core.Service;
    using Core;
    using ServiceStack.ServiceClient.Web;
    using ServiceStack.ServiceHost;
    using ServiceStack.ServiceInterface.Auth;
    using ServiceStack.Text;

    internal interface IHasSession {
        IAuthSession Session {get;set;}
    }

    public class RestableCmdlet<T> : RestableCmdlet, IHasSession , IService<T> where T : RestableCmdlet<T> {
        private PersistablePropertyInformation[] _persistableElements = typeof(T).GetPersistableElements().Where(p => p.Name == "Session" || !JsConfig<T>.ExcludePropertyNames.Contains(p.Name)).ToArray();
        private string _serviceUrl;
        private PSCredential _credential;

        [Parameter(HelpMessage = "Remote Service URL")]
        public string ServiceUrl {
            get {
                if (Remote && string.IsNullOrEmpty(_serviceUrl)) {
                    return SetDefaultRemoteService.DefaultServiceUrl;
                }
                return _serviceUrl;
            }
            set {
                // setting the service url automatically assumes that you 
                // are using a remote service.
                Remote = true;
                _serviceUrl = value;
            }
        }

        [Parameter(HelpMessage = "Credentials to conenct to service")]
        public PSCredential Credential {
            get {
                if(Remote && _credential == null) {
                    return SetDefaultRemoteService.DefaultCredential;
                }
                return _credential;
            }
            set {
                _credential = value;
            }
        }

        [Parameter(HelpMessage = "Use Remote Service")]
        public SwitchParameter Remote { get; set; }

        [Parameter(HelpMessage = "Restricted: Remote Session Instance (do not specify)")]
        public IAuthSession Session {get;set;}

        static RestableCmdlet() {
            var excludes = (from each in typeof (T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                let setMethodInfo = each.GetSetMethod(true)
                let getMethodInfo = each.GetGetMethod(true)
                where
                    (setMethodInfo == null || getMethodInfo == null || each.GetCustomAttributes(typeof (NotPersistableAttribute), true).Any() || !each.GetSetMethod(true).IsPublic || !each.GetGetMethod(true).IsPublic)
                select each.Name);

            JsConfig<T>.ExcludePropertyNames = new[] {
                "CommandRuntime", "CurrentPSTransaction", "Stopping","Remote", "ServiceUrl", "Credential", "CommandOrigin", "Events", "Host", "InvokeCommand", "InvokeProvider" , "JobManager", "MyInvocation", "PagingParameters", "ParameterSetName", "SessionState", "Session"
            }.Union(excludes).ToArray();


            JsConfig<PSCredential>.SerializeFn = credential => string.Format("{0}&{1}", ClrPlus.Core.Extensions.StringExtensions.UrlEncode(credential.UserName), ClrPlus.Core.Extensions.StringExtensions.UrlEncode(credential.Password.ToUnsecureString()));

            JsConfig<PSCredential>.DeSerializeFn = s => {
                var items = s.Split('&');
                return new PSCredential(items[0], items[1].ToSecureString());
            };
        }

        protected virtual void ProcessRecordViaRest() {
            var client = new JsonServiceClient(ServiceUrl);
            
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
                switch(wse.StatusCode) {
                    case 401:
                        if (Credential == null) {
                            throw new Exception("Not Authenticated: you must supply credentials to access the remote service");
                        } else {
                            throw new Exception("Invalid Authentication: the given credentials are not valid with the remote service");
                        }

                    case 403:
                        throw new Exception("Not Authorized: You are not authorized to access that remote service");
                        
                    case 404:
                        throw new Exception("Unknown Service: no remote service for {0} found at {1}".format( GetType().Name, ServiceStack.Text.StringExtensions.WithTrailingSlash(client.SyncReplyBaseUri) + GetType().Name));
                }

                throw new Exception("Unable to call remote cmdlet -- error: {0}".format(wse.Message));
            }
        }

        public virtual object Execute(T cmdlet) {
            var restCommand = RestService.ReverseLookup[cmdlet.GetType()];
            
            using(var dps = new DynamicPowershell(RestService.RunspacePool)) {
                if(cmdlet.Session != null && cmdlet.Session.HasRole("password_must_be_changed") && typeof(T) != typeof(SetServicePassword)) {
                    return new object[] {"WARNING: USER PASSWORD SHOULD BE CHANGED.\r\n"}.Concat( dps.Invoke(restCommand.Name, _persistableElements, cmdlet, restCommand.DefaultParameters, restCommand.ForcedParameters));
                }

                return dps.Invoke(restCommand.Name, _persistableElements, cmdlet, restCommand.DefaultParameters, restCommand.ForcedParameters);
            }
        }

       
        
    }

    public abstract class RestableCmdlet : PSCmdlet {

        public static readonly Regex CREDENTIAL_USERNAME = new Regex (@"(?<property>[a-zA-Z]\w*)_USERNAME");
        public static readonly Regex CREDENTIAL_PASSWORD = new Regex(@"(?<property>[a-zA-Z]\w*)_PASSWORD");
     
        
        public static Dictionary<string, object> ParseParameters(Dictionary<string, object> inputParameters) {
            var keysWithUsername = inputParameters.Keys.Select(s => new {
                                                                            key = s,
                                                                            match = CREDENTIAL_USERNAME.Match(s)
                                                                        }).Where(u => u.match != Match.Empty).ToArray();
            var keysWithPassword = inputParameters.Keys.Select(s => new
                                                                        {
                                                                            key = s,
                                                                            match = CREDENTIAL_PASSWORD.Match(s)
                                                                        }).Where(u => u.match != Match.Empty).ToArray();

            
            //create credential if needed
            if (keysWithPassword.Length == 1 && keysWithUsername.Length == 1 && keysWithUsername[0].match.Groups["property"].Value == keysWithPassword[0].match.Groups["property"].Value)
            {
                var ret = inputParameters.Where(kv => kv.Key != keysWithUsername[0].key && kv.Key != keysWithPassword[0].key).ToDictionary();
                ret[keysWithUsername[0].match.Groups["property"].Value] = new PSCredential(inputParameters[keysWithUsername[0].key].ToString(), inputParameters[keysWithPassword[0].key].ToString().ToSecureString());
                return ret;


            } else {
                return inputParameters;
            }

            
        }
    }
}