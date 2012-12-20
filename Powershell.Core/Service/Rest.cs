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
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Extensions;

    public class Rest  {
        public static Rest Services = new Rest();
        internal RunspacePool RunspacePool;
        private readonly IDictionary<string, RestService> _services = new XDictionary<string, RestService>();

        private Rest() {
            // only accessible via the singleton.
        }

        public IEnumerable<string> ServiceNames {
            get {
                return _services.Keys;
            }
        }

        public RestService Default { get {
            return _services["default"];
        }}

        public RestService this[string index] {
            get {
                index = string.IsNullOrEmpty(index) ? "default" : index;
                return _services.AddOrSet(index, new RestService(index));
            }
        }

        public void Stop(Cmdlet cmdlet ) {
            foreach (var serviceName in _services.Keys) {
                cmdlet.WriteObject("Stopping REST Service: '{0}'".format(serviceName));
            }

            RunspacePool.Close();
            RunspacePool.Dispose();
            RunspacePool = null;
            ReverseLookup = null;
        }

        public void Start(Cmdlet cmdlet, IEnumerable<string> activeModules) {
            
            ReverseLookup = new Dictionary<Type, string>();
            var ss = InitialSessionState.CreateDefault();
            ss.ImportPSModule(Modules.Union(activeModules).ToArray());

            RunspacePool = RunspaceFactory.CreateRunspacePool(ss);
            RunspacePool.Open();

            foreach(var serviceName in _services.Keys) {
                _services[serviceName].Start();
                cmdlet.WriteObject("Started REST Service: '{0}'".format(serviceName));
            }
        }

        internal IDictionary<Type, string> ReverseLookup = new Dictionary<Type, string>();
        public List<string> Modules  = new List<string>();
    }
}