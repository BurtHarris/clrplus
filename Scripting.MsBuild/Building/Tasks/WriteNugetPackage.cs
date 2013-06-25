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
    using System.IO;
    using System.Linq;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Packaging;
    using Platform;

    public class WriteNugetPackage : MsBuildTaskBase {
        [Required]
        public ITaskItem Package {get; set;}

        [Output]
        public ITaskItem[] AllPackages {get; set;}

        [Output]
        public ITaskItem[] MainPackages {get; set;}

        [Output]
        public ITaskItem[] RedistPackages {get; set;}

        [Output]
        public ITaskItem[] SymbolsPackages {get; set;}

        [Output]
        public bool NuGetSuccess {get; set;}

        public TaskItem[] Defines {get; set;}

        public string PackageDirectory {get; set;}

        public override bool Execute() {
            var pkgPath = Package.ItemSpec;

            var defines = Defines.IsNullOrEmpty() ? new string[0] : Defines.Select(each => each.ItemSpec).ToArray();

            try {
                using (var script = new PackageScript(pkgPath)) {
                    if (PackageDirectory.Is()) {
                        script.AddNuGetPackageDirectory(PackageDirectory.GetFullPath());
                    }
                    if (defines != null) {
                        foreach (var i in defines) {
                            var p = i.IndexOf("=");
                            var k = p > -1 ? i.Substring(0, p) : i;
                            var v = p > -1 ? i.Substring(p + 1) : "";
                            script.AddMacro(k, v);
                        }
                    }

                    script.Save(PackageTypes.NuGet, true);

                    AllPackages = script.Packages.Select(each => (ITaskItem)new TaskItem(each)).ToArray();

                    RedistPackages = AllPackages.Where(each => each.ItemSpec.ToLower().IndexOf("redist") > -1).ToArray();
                    SymbolsPackages = AllPackages.Where(each => each.ItemSpec.ToLower().IndexOf("symbols") > -1).ToArray();
                    MainPackages = AllPackages.Where(each => each.ItemSpec.ToLower().IndexOf("redist") == -1 && each.ItemSpec.ToLower().IndexOf("symbols") == -1).ToArray();

                    foreach (var p in RedistPackages) {
                        var n = Path.GetFileNameWithoutExtension(p.ItemSpec);
                        var o = n.IndexOf(".redist.");

                        p.SetMetadata("pkgIdentity", "{0} {1}".format(n.Substring(0, o + 7), n.Substring(o + 8)));
                    }

                    NuGetSuccess = true;
                    return true;
                }
            } catch {
            }
            NuGetSuccess = false;
            return false;
        }
    }
}