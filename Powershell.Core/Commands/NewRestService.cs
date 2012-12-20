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
//------------------------------------------------------------  -----------

namespace ClrPlus.Powershell.Core.Commands {
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Extensions;
    using Scripting.Languages.PropertySheet;
    using Service;

    [Cmdlet(VerbsCommon.New, "RestService")]
    public class NewRestService : Cmdlet {
        [Parameter]
        public SwitchParameter Auto {get; set;}

        [Parameter]
        public string Config {get; set;}

        [Parameter]
        public string Name {get; set;}

        [Parameter]
        public string[] ListenOn {get; set;}

        protected override void ProcessRecord() {
            if (Auto) {
                Config = "restservice.properties";
            }
            
            if (!string.IsNullOrEmpty(Config)) {
                var propertySheet = PropertySheet.Parse(@"@import @""{0}"";".format(Config), "default");
                
                foreach (var serviceRule in propertySheet.Rules.Where(rule => rule.Name == "rest-service")) {
                    var  serviceName = serviceRule.Parameter;
                    Rest.Services[serviceName].AddListeners(serviceRule["listen-on"].Values.Union(ListenOn));

                    foreach(var commandRule in propertySheet.Rules.Where(rule => rule.Name == "rest-command" && serviceName == rule["service"].Value)) {
                        AddCommandsFromConfig(commandRule, Rest.Services[serviceName]);    
                    }
                }

                // find any commands for the default listener...
                foreach(var commandRule in propertySheet.Rules.Where(rule => rule.Name == "rest-command" && rule["service"] == null)) {
                    AddCommandsFromConfig(commandRule, Rest.Services.Default);
                }
            } else {
                Rest.Services[Name].AddListeners(ListenOn);
            }
        }

        private static void AddCommandsFromConfig(Rule commandRule, RestService service) {
            var cmdletName = commandRule["cmdlet"] ?? commandRule["command"];
            var publishAs = commandRule["publish-as"] ?? commandRule["publishas"] ?? cmdletName;
            var parameters = commandRule["parameters"] ?? commandRule["default-parameters"] ?? commandRule["default"];
            var forcedParameters = commandRule["forced-parameters"] ?? commandRule["forced"];

            if (cmdletName != null) {
                service.AddCommand( new RestCommand {
                    Name = cmdletName.Value,
                    PublishAs =  publishAs.Value ,
                    DefaultParameters =   (parameters == null) ? new Dictionary<string, IEnumerable<string>>() : parameters.Labels.ToDictionary(label => label, label => (IEnumerable<string>)parameters[label]),
                    ForcedParameters = (forcedParameters == null) ? new Dictionary<string, IEnumerable<string>>() : forcedParameters.Labels.ToDictionary(label => label, label => (IEnumerable<string>)forcedParameters[label])
                });
            }
        }
    }
}