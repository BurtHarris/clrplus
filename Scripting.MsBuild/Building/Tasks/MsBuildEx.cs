using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Scripting.MsBuild.Building.Tasks {
    using Platform;

    public class MsBuildEx : Microsoft.Build.Tasks.MSBuild {

        public override bool Execute() {
            if (this.Projects == null || this.Projects.Length == 0)
                return true;

            // make sure that all the projects end in .vcxproj
            foreach (var i in Projects) {
                if (!i.ItemSpec.ToLower().EndsWith(".vcxproj")) {
                    return base.Execute();
                }
            }

            // ok, they're all c++ projects.
            var tempSolution = "tempSolution.sln".GenerateTemporaryFilename();
            
            

            return base.Execute();
        }

    }
}
