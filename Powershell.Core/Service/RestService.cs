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

namespace ClrPlus.Powershell.Core.Service {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Net;
    using System.Reflection;
    using System.Web;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using Commands;
    using Funq;
    using Platform;
    using Scripting.Languages.PropertySheet;
    using ServiceStack.Common.Web;
    using ServiceStack.Logging;
    using ServiceStack.Logging.Support.Logging;
    using ServiceStack.ServiceInterface;
    using ServiceStack.ServiceInterface.Auth;
    using ServiceStack.WebHost.Endpoints;
    using ServiceStack.WebHost.Endpoints.Extensions;

    public class RestService : AppHostHttpListenerBase{

        internal static RunspacePool RunspacePool;
        internal static readonly IDictionary<Type, RestCommand> ReverseLookup = new XDictionary<Type, RestCommand>();
        private static RestService _service;
        private static readonly List<string> _urls = new List<string>();
        private static readonly List<RestCommand> _commands = new List<RestCommand>();
        private static string _configFile;

        public static void StopService() {
            lock (typeof(RestService)) {
                _service.Dispose();
                _service = null;
            }
        }

        public static void StartService(IEnumerable<string> activeModules) {
            if (_service != null) {
                StopService();
            }

            _service = new RestService("restservice", _urls, _commands, activeModules);
            _service.Start();
        }

        internal static void AddCommand(RestCommand restCommand) {
            for (int i = _commands.Count - 1; i >= 0; i--) {
                if (_commands[i].PublishAs == restCommand.PublishAs) {
                    _commands.RemoveAt(i);
                }
            }

            _commands.Add(restCommand);
        }

        public static void AddListener(string url) {
            if (!string.IsNullOrEmpty(url) && !_urls.Contains(url)) {
                _urls.Add(url);
            }
        }

        public static void AddListeners(IEnumerable<string> listenOn) {
            foreach (var listener in listenOn) {
                AddListener(listener);
            }
        }



        public static string ConfigFile {
            get {

                return _configFile;
            }
            set {
                if (value.IndexOf("/") > -1 || value.IndexOf("\\") > -1) {
                    _configFile = value.GetFullPath();
                }
                else {
                    _configFile = value;
                }


                var propertySheet = PropertySheet;

                var serviceRule = propertySheet.Rules.FirstOrDefault(rule => rule.Name == "rest-service");
                var l1 = serviceRule["listen-on"].Values.ToArray();

                if (!l1.IsNullOrEmpty()) {
                    AddListeners(l1);
                }

                foreach (var commandRule in propertySheet.Rules.Where(rule => rule.Name == "rest-command")) {
                    AddCommandsFromConfig(commandRule);
                }
            }
        }

        private static void AddCommandsFromConfig(Rule commandRule) {
            var cmdletName = commandRule["cmdlet"] ?? commandRule["command"];
            var publishAs = commandRule["publish-as"] ?? commandRule["publishas"] ?? cmdletName;
            var parameters = commandRule["parameters"] ?? commandRule["default-parameters"] ?? commandRule["default"];
            var forcedParameters = commandRule["forced-parameters"] ?? commandRule["forced"];
            var roles = commandRule["role"] ?? commandRule["roles"];

            if (cmdletName != null) {
                AddCommand(new RestCommand {
                    Name = cmdletName.Value,
                    PublishAs = publishAs.Value,
                    DefaultParameters = (parameters == null) ? null : parameters.Labels.ToDictionary(label => label, label => parameters[label].IsSingleValue ? (object)parameters[label].Value : ((IEnumerable<string>)parameters[label]).ToArray()),
                    ForcedParameters = (forcedParameters == null) ? null : forcedParameters.Labels.ToDictionary(label => label, label => forcedParameters[label].IsSingleValue ? (object)forcedParameters[label].Value : ((IEnumerable<string>)forcedParameters[label]).ToArray()),
                    Roles = roles == null ? null : roles.Values.ToArray()
                });
            }
        }

        internal static PropertySheet PropertySheet {
            get {
                if (_configFile.IndexOf("/") > -1 || _configFile.IndexOf("\\") > -1) {
                    return PropertySheet.Load(_configFile);
                }

                return PropertySheet.Parse(@"@import @""{0}"";".format(ConfigFile), "default");
            }
        }

        private static Rule GetUserRule(string name) {
            return PropertySheet.Rules.FirstOrDefault(rule => rule.Name == "user" && rule.Parameter == name);
        }

        public static bool TryAuthenticate(IServiceBase authService, string userName, string password) {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password)) {
                return false;
            }
            var user = GetUserRule(userName);

            if (user == null) {
                return false;
            }

            if (user["password"].Value == password) {
                return true;
            }

            return false;
        }

        internal static void OnAuthenticated(IServiceBase authService, IAuthSession session, IOAuthTokens tokens, Dictionary<string, string> authInfo, TimeSpan? SessionExpiry) {
            var user = GetUserRule(session.UserAuthName);

            if (user == null) {
                return;
            }

            session.IsAuthenticated = true;

            //Fill the IAuthSession with data which you want to retrieve in the app eg:
            session.FirstName = user.HasProperty("firstname") ? user["firstname"].Value : "";
            session.LastName = user.HasProperty("lastname") ? user["lastname"].Value : "";

            session.Roles = user.HasProperty("roles") ? user["roles"].Values.ToList() : new List<string>();

            //Important: You need to save the session!
            authService.SaveSession(session, SessionExpiry);
        }



        internal static bool ChangePassword(string userName, string newPassword) {
            lock (typeof(RestService)) {
                if (_configFile.IndexOf("/") > -1 || _configFile.IndexOf("\\") > -1) {

                    var propertySheet = PropertySheet;
                    var user = propertySheet.Rules.FirstOrDefault(rule => rule.Name == "user" && rule.Parameter == userName);
                    if (user != null && user.HasProperty("password")) {
                        user["password"].Value = newPassword;
                    }

                    propertySheet.Save(_configFile);
                    return true;
                }
            }
            return false;
        }

        
        private readonly string _serviceName;
        private bool _configured;
        private bool _isStopping;
        private readonly List<RestCommand> _activeCommands;
        private readonly List<string> _listenOnUrls = new List<string>();
        
        protected override void Dispose(bool disposing) {
            Stop();

            RunspacePool.Close();
            RunspacePool.Dispose();
            RunspacePool = null;
            base.Dispose(disposing);
        }

        ~RestService() {
            Dispose();
        }


        internal RestService(string serviceName, List<string> urls, List<RestCommand> commands, IEnumerable<string> modules) 
            : base(serviceName, GetActiveAssemblies().ToArray()) {
            _serviceName = serviceName;
            _activeCommands = commands;
            _listenOnUrls = urls;
            ReverseLookup.Clear();


            var ss = InitialSessionState.CreateDefault();
            ss.ImportPSModule(modules.ToArray());

            RunspacePool = RunspaceFactory.CreateRunspacePool(ss);
            RunspacePool.Open();
        }
      
        private static readonly string[] _hideKnownAssemblies = new[] {
            "ServiceStack", // exclude the service stack assembly
            "b03f5f7f11d50a3a", // Microsoft
            "b77a5c561934e089", // Microsoft
            "31bf3856ad364e35" // Microsoft
        };

        private static IEnumerable<Assembly> GetActiveAssemblies() {
            return AppDomain.CurrentDomain.GetAssemblies().Where(each => !_hideKnownAssemblies.Any(x => each.FullName.IndexOf(x) > -1));
        }

        public override void Configure(Container container) {
            _configured = true;
            // Feature disableFeatures = Feature.Jsv | Feature.Soap;
            SetConfig(new EndpointHostConfig {
                // EnableFeatures = Feature.All.Remove(disableFeatures), //all formats except of JSV and SOAP
                DebugMode = true, //Show StackTraces in service responses during development
                WriteErrorsToResponse = false, //Disable exception handling
                DefaultContentType = ContentType.Json, //Change default content type
                AllowJsonpRequests = true, //Enable JSONP requests
                ServiceName = "RestService",
            });
            LogManager.LogFactory = new DebugLogFactory();


            Plugins.Add(new AuthFeature(() => new AuthUserSession(),
             new IAuthProvider[] {
                    new CustomBasicAuthProvider(), 
                   // new CustomCredentialsAuthProvider(), 
                }
             ));
            

            // stick a request filter in to validate that the user has the right to actually 
            // call this method.
            RequestFilters.Add((request, response, requestDto) => {
                
                var restCommand = ReverseLookup[requestDto.GetType()];

                // is this one of the restCommands?
                // and does it has roles defined?
                if (restCommand != null && !restCommand.Roles.IsNullOrEmpty()) {
                    
                    // ensure we're authenticated if the user passed the right stuff in the request
                    try {
                        AuthenticateAttribute.AuthenticateIfBasicAuth(request, response);
                    } catch (Exception e) {
                        Console.WriteLine(e.Message);
                        response.StatusCode = 401;
                        response.AddHeader("WWW-Authenticate", "Basic realm=\"rest-service\"");
                        response.StatusDescription = "Unauthorized";
                        response.EndServiceStackRequest(false);
                        return;
                    }

                    // get the session object.
                    IAuthSession session = request.GetSession(false);
                    
                    // check if we got our authentication.
                    if (!session.IsAuthenticated) {
                        response.StatusCode = 401;
                        response.AddHeader("WWW-Authenticate", "Basic realm=\"rest-service\"");
                        response.StatusDescription = "Unauthorized";
                        response.EndServiceStackRequest(false);
                        return;
                    }

                    // validate the user has the role.
                    if (!restCommand.Roles.Any(session.HasRole)) {
                        response.StatusCode = 403;
                        
                        response.StatusDescription = "Forbidden";
                        response.EndServiceStackRequest(false);
                    }

                    var req = (requestDto as IHasSession);
                    if (req != null) {
                        req.Session = session;
                    }
                }
            });

            using(var ps = RunspacePool.Dynamic()) {
                foreach (var restCommand in _activeCommands) {
                    PSObject command = ps.LookupCommand(restCommand.Name);

                    if (command != null) {
                        var cmdletInfo = (command.ImmediateBaseObject as CmdletInfo);
                        if (cmdletInfo != null) {
                            ReverseLookup.AddOrSet(cmdletInfo.ImplementingType, restCommand);
                            Routes.Add(cmdletInfo.ImplementingType, "/" + restCommand.PublishAs + "/", "GET");
                        } else {
                            throw new ClrPlusException("command isn't cmdletinfo: {0}".format(command.GetType()));
                        }
                    }
                }
            }
        }

        public void Start() {
            if (IsStarted) {
                return;
            }

            if (!_configured) {
                Init();
            }

            if (Listener == null) {
                Listener = new HttpListener();
            }
            if (!_listenOnUrls.Any()) {
                // if the default hasn't got anything set, listen everywhere.
                _listenOnUrls.Add("http://*/");
            }

            foreach (var urlBase in _listenOnUrls) {
                Listener.Prefixes.Add(urlBase);
            }

            Config.DebugOnlyReturnRequestInfo = false;
            Config.LogFactory = new ConsoleLogFactory();

            Start(_listenOnUrls.FirstOrDefault());
        }



        public override void Stop() {
            if (IsStarted) {
                if (!_isStopping) {
                    _isStopping = true;
                    base.Stop();
                }
            }
        }

       
    }
}