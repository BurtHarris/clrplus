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
    using System.Management.Automation.Runspaces;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Core.Tasks;
    using ClrPlus.Core.Utility;
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
        }

        [Parameter(HelpMessage = "Suppress output of all warnings")]
        public SwitchParameter NoWarnings { get; set; }

        [Parameter(HelpMessage = "Suppress output of all non-essential messages")]
        public SwitchParameter Quiet { get; set; }

        protected LocalEventSource LocalEventSource {
            get {
                var local = CurrentTask.Local;

                local.Events += new Error((code, message, objects) => {
                    Host.UI.WriteErrorLine("{0}:{1}".format(code, message.format(objects)));
                    return true;
                });

                if (!NoWarnings && !Quiet) {
                    local.Events += new Warning((code, message, objects) => {
                        WriteWarning("{0}:{1}".format(code, message.format(objects)));
                        return false;
                    });
                }

                local.Events += new Debug((code, message, objects) => {
                    WriteDebug("{0}:{1}".format(code, message.format(objects)));
                    return false;
                });

                local.Events += new Verbose((code, message, objects) => {
                    WriteVerbose("{0}:{1}".format(code, message.format(objects)));
                    return false;
                });

                local.Events += new Progress((code, progress, message, objects) => {
                    WriteProgress(new ProgressRecord(0, code, message.format(objects)) {
                        PercentComplete = progress
                    });
                    return false;
                });

                if (!Quiet) {
                    local.Events += new Message((code, message, objects) => {
                        Host.UI.WriteLine("{0}{1}".format(code, message.format(objects)));
                        return false;
                    });
                }
                return local;
            }
        }

        protected virtual void ProcessRecordViaRest() {
            var client = new JsonServiceClient(ServiceUrl);
            
            if (Credential != null) {
                client.SetCredentials(Credential.UserName, Credential.Password.ToUnsecureString());            
            }
            PowershellReponse response = null;

            try {
                // try connecting where the URL is the base URL
                response = client.Send<PowershellReponse>((this as T));
                
                if (response != null ) {

                    if(!response.Warnings.IsNullOrEmpty()) {
                        foreach(var warning in response.Warnings) {
                            WriteWarning(warning);
                        }
                    }

                    if(!response.Error.IsNullOrEmpty()) {
                        foreach(var error in response.Error.TakeAllBut(1)) {
                            WriteError(new ErrorRecord(new Exception("{0} - {1}".format( error.ExceptionType, error.ExceptionMessage)), error.Message, error.Category, null));
                        }
                    }

                    if(!response.Output.IsNullOrEmpty()) {
                        foreach(var ob in response.Output) {
                            WriteObject(ob);
                        }
                    }

                    if (response.LastIsTerminatingError) {
                        var error = response.Error.Last();
                        ThrowTerminatingError(new ErrorRecord(new Exception("{0} - {1}".format(error.ExceptionType, error.ExceptionMessage)), error.Message, error.Category, null));
                    }
                }

            } catch (WebServiceException wse) {
                switch(wse.StatusCode) {
                    case 401:
                        if (Credential == null) {
                            ThrowTerminatingError(new ErrorRecord(new Exception("Not Authenticated: you must supply credentials to access the remote service",wse), "", ErrorCategory.PermissionDenied, null));
                            return;
                        } else {
                            ThrowTerminatingError(new ErrorRecord(new Exception("Invalid Authentication: the given credentials are not valid with the remote service",wse), "", ErrorCategory.PermissionDenied, null));
                            return;
                        }

                    case 403:
                        ThrowTerminatingError(new ErrorRecord(new Exception("Not Authorized: You are not authorized to access that remote service", wse), "", ErrorCategory.PermissionDenied, null));
                        return;
                    case 404:
                        ThrowTerminatingError(new ErrorRecord(new Exception("Unknown Service: no remote service for {0} found at {1}".format( GetType().Name, ServiceStack.Text.StringExtensions.WithTrailingSlash(client.SyncReplyBaseUri) + GetType().Name), wse), "", ErrorCategory.ResourceUnavailable, null)) ;
                        return;
                }

                ThrowTerminatingError(new ErrorRecord(new Exception("Unable to call remote cmdlet -- error: {0}".format(wse.Message), wse), "", ErrorCategory.NotSpecified, null));
            }
        }

        public virtual object Execute(T cmdlet) {
            var restCommand = RestService.ReverseLookup[cmdlet.GetType()];
            var result = new PowershellReponse();
            AsynchronouslyEnumerableList<ErrorRecord> errors = null; 

            using (var dps = new DynamicPowershell(RestService.RunspacePool)) {
                if (cmdlet.Session != null && cmdlet.Session.HasRole("password_must_be_changed") && typeof (T) != typeof (SetServicePassword)) {
                    result.Warnings = new string[] {
                        "WARNING: USER PASSWORD SHOULD BE CHANGED.\r\n"
                    };
                    
                }
                var dynamicResult = dps.Invoke(restCommand.Name, _persistableElements, cmdlet, restCommand.DefaultParameters, restCommand.ForcedParameters, out errors);
                result.Output = dynamicResult.ToArray();
                result.LastIsTerminatingError = dynamicResult.LastIsTerminatingError;

            }
           
            if (errors != null && errors.Any()) {
                result.Error = errors.Select(e => new RemoteError {
                    Category = e.CategoryInfo.Category,
                    Message = e.FullyQualifiedErrorId,
                    ExceptionType = e.Exception.GetType().Name,
                    ExceptionMessage = e.Exception.Message,
                }).ToArray();
            }

            return result;
        }

    }

    public abstract class RestableCmdlet : PSCmdlet {

        public static readonly Regex CREDENTIAL_USERNAME = new Regex (@"(?<property>[a-zA-Z]\w*)_USERNAME");
        public static readonly Regex CREDENTIAL_PASSWORD = new Regex(@"(?<property>[a-zA-Z]\w*)_PASSWORD");
     
        static RestableCmdlet() {
            JsConfig<PSCredential>.RawSerializeFn = credential => {
                return string.Format(@"{{""__type"":""System.Management.Automation.PSCredential, System.Management.Automation"",""Username"":""{0}"",""Password"":""{1}""}}", ClrPlus.Core.Extensions.StringExtensions.UrlEncode(credential.UserName), ClrPlus.Core.Extensions.StringExtensions.UrlEncode(credential.Password.ToUnsecureString()));
            };

            JsConfig<PSCredential>.RawDeserializeFn = s => {
                var j = JsonObject.Parse(s);
                return new PSCredential( HttpUtility.UrlDecode(j["Username"]), HttpUtility.UrlDecode(j["Password"]).ToSecureString());
            };
        }
        
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

        private string _originalDir; 
        protected override void BeginProcessing() {
            _originalDir = Environment.CurrentDirectory;
            Environment.CurrentDirectory = (SessionState.PSVariable.GetValue("pwd") ?? "").ToString();
        }

        protected override void EndProcessing() {
            if (_originalDir != null) {
                Environment.CurrentDirectory = _originalDir;
            }
        }

    }
}

