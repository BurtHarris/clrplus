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
//------------------------------------------------------------  -----------

namespace ClrPlus.Powershell.Core.Commands {
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Text.RegularExpressions;
    using Service;

    [Cmdlet(VerbsCommon.Add, "RestCmdlet")]
    public class AddRestCmdlet : Cmdlet {
        private static readonly Regex _keyValueRx = new Regex(@"-?(?<switch>.*?):(?<value>.*)|(?<switch>.*)(?<value>)");
        [Parameter(Mandatory = true)]
        public string Command { get; set; }

        [Parameter]
        public string PublishAs{ get; set; }

        [Parameter]
        public string[] RoleRequired { get; set; }

        [Parameter]
        public string[] DefaultParameter { get; set; }

        [Parameter]
        public string[] ForcedParameter { get; set; }

        private static Dictionary<string, object> ProcessParameters(IEnumerable<string> parameters) {
            if (parameters == null) {
                return new Dictionary<string, object>();
            }
            var set = parameters.Select(each => _keyValueRx.Match(each)).ToArray();
            var keys = set.Select(match => match.Groups["switch"].Value).Distinct();

            return keys.ToDictionary(k => k, key => {
                var items = set.Where(match => match.Groups["switch"].Value == key).Select(each => each.Groups["value"].Value).ToArray();
                return items.Count() == 1 ? (object)items[0] : items;
            });
        }

        protected override void ProcessRecord() {
            RestService.AddCommand(new RestCommand {
                Name = Command,
                PublishAs = PublishAs ?? Command,
                DefaultParameters = ProcessParameters(DefaultParameter),
                ForcedParameters = ProcessParameters(ForcedParameter),
                Roles = RoleRequired
            });
        }
    }
}