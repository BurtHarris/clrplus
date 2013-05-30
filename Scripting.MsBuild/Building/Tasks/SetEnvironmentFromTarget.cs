namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Extensions;
    using Core.Utility;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Tasks;
    using Microsoft.Build.Utilities;

    [RunInMTA]
    public class SetEnvironmentFromTarget : Task {
        private static readonly Dictionary<string, IDictionary> _environments = new Dictionary<string, IDictionary>();

        // Fields
        private ArrayList targetOutputs = new ArrayList();

        private static dynamic _msbuild = (new MSBuild()).AccessPrivate();

        // Methods
        public override bool Execute() {
            Target = Target.ToLower();

            if (_environments.ContainsKey(Target)) {
                
                var env = _environments[Target];
                if (env == null) {
                    IsEnvironmentValid = false;
                    return true;
                }

                IsEnvironmentValid = true;
                EnvironmentManager.Instance.Apply(env);
                return true;
            } 
            try {
                ArrayList targetLists = _msbuild.CreateTargetLists(new[] {Target}, false);
                var result = _msbuild.ExecuteTargets(new ITaskItem[] {null}, null, null, targetLists, false, false, BuildEngine3, Log, targetOutputs, false, false, null);

                if (result) {
                    _environments.Add(Target, Environment.GetEnvironmentVariables());
                    IsEnvironmentValid = true;
                    return true;
                }

            } catch  {
                // any failure here really means that it should just assume it didn't work.
            }

            _environments.Add(Target, null);
            IsEnvironmentValid = false;
            
            return true;
        }

        [Output]
        public ITaskItem[] TargetOutputs {
            get {
                return (ITaskItem[])targetOutputs.ToArray(typeof(ITaskItem));
            }
        }

        [Required]
        public string Target { get; set; }

  

        [Output]
        public bool IsEnvironmentValid { get; set;}
    }
}