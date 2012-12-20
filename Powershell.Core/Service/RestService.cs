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
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Net;
    using System.Reflection;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using Funq;
    using ServiceStack.Common.Web;
    using ServiceStack.Logging;
    using ServiceStack.Logging.Support.Logging;
    using ServiceStack.WebHost.Endpoints;

    public class RestService : AppHostHttpListenerBase {
        private readonly string _serviceName;
        private readonly List<string> _urls = new List<string>();
        private readonly List<RestCommand> _commands = new List<RestCommand>();
        private bool _configured;
        private bool _isStopping;

        protected override void Dispose(bool disposing) {
            Stop();
            base.Dispose(disposing);
        }

        ~RestService() {
            Dispose();
        }

        public RestService(string serviceName)
            : base(serviceName, GetActiveAssemblies().ToArray()) {
            _serviceName = serviceName;
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

            using(var ps = Rest.Services.RunspacePool.Dynamic()) {
                foreach (var restCommand in _commands) {

                    PSObject command = ps.LookupCommand(restCommand.Name);

                    

                    if (command != null) {
                        var cmdletInfo = (command.ImmediateBaseObject as CmdletInfo);
                        if (cmdletInfo != null) {
                            Rest.Services.ReverseLookup.AddOrSet(cmdletInfo.ImplementingType, restCommand.Name);
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
            if (!_urls.Any() && _serviceName == "default") {
                // if the default hasn't got anything set, listen everywhere.
                _urls.Add("http://*/");
            }

            foreach (var urlBase in _urls) {
                Listener.Prefixes.Add(urlBase);
            }

            Config.DebugOnlyReturnRequestInfo = false;
            Config.LogFactory = new ConsoleLogFactory();
            Config.LogFactory.GetLogger(GetType()).Debug("Hi");

            Start(_urls.FirstOrDefault());
        }

        public override void Stop() {
            if(!_isStopping) {
                _isStopping = true;
                base.Stop();
            }
        }

        internal void AddCommand(RestCommand restCommand) {
            for (int i = _commands.Count-1; i>= 0; i--) {
                if (_commands[i].PublishAs == restCommand.PublishAs) {
                    _commands.RemoveAt(i);
                }
            }

            _commands.Add(restCommand);
        }

        public void AddListener(string url) {
            if(!string.IsNullOrEmpty(url) && !_urls.Contains(url)) {
                _urls.Add(url);
            }
        }

        public void AddListeners(IEnumerable<string> listenOn) {
            foreach (var listener in listenOn) {
                AddListener(listener);
            }
        }
    }
}