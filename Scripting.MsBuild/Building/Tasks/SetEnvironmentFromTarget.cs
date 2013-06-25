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

namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Core.Utility;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Tasks;
    using Microsoft.Build.Utilities;
    using Platform;

    [RunInMTA]
    public class SetEnvironmentFromTarget : Task {
        private static readonly Dictionary<string, IDictionary> _environments = new Dictionary<string, IDictionary>();

        // Fields

        private static dynamic _msbuild = (new MSBuild()).AccessPrivate();
        private ArrayList targetOutputs = new ArrayList();

        // Methods

        [Output]
        public ITaskItem[] TargetOutputs {
            get {
                return (ITaskItem[])targetOutputs.ToArray(typeof (ITaskItem));
            }
        }

        [Required]
        public string Target {get; set;}

        [Output]
        public bool IsEnvironmentValid {get; set;}

        public override bool Execute() {
            // set to false first. 
            IsEnvironmentValid = false;

            Target = Target.ToLower();

            if (_environments.ContainsKey(Target)) {
                var env = _environments[Target];
                if (env == null) {
                    IsEnvironmentValid = false;
                    return true;
                }

                IsEnvironmentValid = true;
                EnvironmentUtility.Apply(env);
                return true;
            }
            try {
                ArrayList targetLists = _msbuild.CreateTargetLists(new[] {
                    Target
                }, false);
                var result = _msbuild.ExecuteTargets(new ITaskItem[] {
                    null
                }, null, null, targetLists, false, false, BuildEngine3, Log, targetOutputs, false, false, null);

                if (result) {
                    _environments.Add(Target, Environment.GetEnvironmentVariables());
                    IsEnvironmentValid = true;
                    return true;
                }
            } catch {
                // any failure here really means that it should just assume it didn't work.
            }

            _environments.Add(Target, null);
            IsEnvironmentValid = false;

            return true;
        }
    }
}