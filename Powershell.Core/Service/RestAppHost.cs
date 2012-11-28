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

namespace ClrPlus.Powershell.Core.Service {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Net;
    using System.Reflection;
    using ClrPlus.Core.Collections;
    using Funq;
    using ServiceStack.Common.Web;
    using ServiceStack.Logging;
    using ServiceStack.Logging.Support.Logging;
    using ServiceStack.WebHost.Endpoints;

    public class RestAppHost : AppHostHttpListenerBase {
        private OnDisposable<RunspacePool> _sharedRunspacePool;

        public OnDisposable<RunspacePool> SharedRunspacePool {
            get {
                return _sharedRunspacePool ?? (_sharedRunspacePool = new OnDisposable<RunspacePool>(RunspaceFactory.CreateRunspacePool(InitialSessionState.CreateDefault())));
            }
            private set {
                _sharedRunspacePool = value;
            }
        }

        protected override void Dispose(bool disposing) {
            Stop();
            SharedRunspacePool = null; // deletes when it's the last user.
            base.Dispose();
        }

        ~RestAppHost() {
            Dispose();
        }

        public RestAppHost(IEnumerable<string> modules)
            : base("RestService", GetActiveAssemblies().ToArray()) {
            SharedRunspacePool.Value.InitialSessionState.ImportPSModule(modules.ToArray());
            SharedRunspacePool.Value.Open();
        }

        public RestAppHost(RestAppHost oldInstance, IEnumerable<string> modules)
            : base("RestService", GetActiveAssemblies().ToArray()) {
            SharedRunspacePool.Value.InitialSessionState.ImportPSModule(modules.ToArray());
            SharedRunspacePool.Value.Open();
            _urls.AddRange(oldInstance._urls);
            // _commands.AddRange(oldInstance._commands);
        }

        private static string[] hideKnownAssemblies = new[] {
            "ServiceStack", // exclude the service stack assembly
            "b03f5f7f11d50a3a", // Microsoft
            "b77a5c561934e089", // Microsoft
            "31bf3856ad364e35" // Microsoft
        };

        private static IEnumerable<Assembly> GetActiveAssemblies() {
            return AppDomain.CurrentDomain.GetAssemblies().Where(each => !hideKnownAssemblies.Any(x => each.FullName.IndexOf(x) > -1));
        }

        private bool _configured;

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

            using (dynamic ps = new DynamicPowershell(SharedRunspacePool)) {
                foreach (var commandName in _commands) {
                    PSObject command = ps.ResolveCommand(commandName);

                    if (command != null) {
                        var cmdletInfo = (command.ImmediateBaseObject as CmdletInfo);
                        if (cmdletInfo != null) {
                            Routes.Add(cmdletInfo.ImplementingType, "/" + commandName + "/", "GET");
                        }
                    }
                }
            }
        }

        private List<string> _urls = new List<string>();

        public void AddListener(string url) {
            if (!string.IsNullOrEmpty(url) && !_urls.Contains(url)) {
                _urls.Add(url);
            }
        }

        private XDictionary<string, IDictionary<string, object>> _commands = new XDictionary<string, IDictionary<string, object>>();

        public void AddCommand(string command, IDictionary<string, object> defaultParameters = null) {
            if (string.IsNullOrEmpty(command)) {
                return;
            }

            if (!_commands.ContainsKey(command)) {
                _commands.Add(command, defaultParameters);
                return;
            }

            if (defaultParameters != null) {
                foreach (var k in defaultParameters.Keys) {
                    _commands[command][k] = defaultParameters[k];
                }
            }
        }

        public bool Started {
            get {
                return IsStarted;
            }
        }

        public static XDictionary<string, RestAppHost> Instances = new XDictionary<string, RestAppHost>();

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
            foreach (var urlBase in _urls) {
                Listener.Prefixes.Add(urlBase);
            }
            Config.DebugOnlyReturnRequestInfo = false;
            Config.LogFactory = new ConsoleLogFactory();
            Config.LogFactory.GetLogger(GetType()).Debug("Hi");

            Start(_urls.FirstOrDefault());
        }

        public new void Stop() {
            base.Stop();
        }
    }
}