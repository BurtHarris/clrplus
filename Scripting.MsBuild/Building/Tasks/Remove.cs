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
    using System.Linq;
    using Core.Extensions;
    using Microsoft.Build.Framework;
    using Platform;

    public class Remove : MsBuildTaskBase {
        public ITaskItem[] Directories {
            get;
            set;
        }
        public ITaskItem[] Files {
            get;
            set;
        }
        public ITaskItem[] Locations {
            get;
            set;
        }
        public ITaskItem[] Directory {
            get;
            set;
        }
        public ITaskItem[] File {
            get;
            set;
        }
        public ITaskItem[] Location {
            get;
            set;
        }

        public override bool Execute() {
            var items = Directories.IsNullOrEmpty() ? new string[0] : Directories.Select(each => each.ItemSpec);
            items.Union(Files.IsNullOrEmpty() ? new string[0] : Files.Select(each => each.ItemSpec));
            items.Union(Locations.IsNullOrEmpty() ? new string[0] : Locations.Select(each => each.ItemSpec));
            items.Union(Directory.IsNullOrEmpty() ? new string[0] : Directory.Select(each => each.ItemSpec));
            items.Union(File.IsNullOrEmpty() ? new string[0] : File.Select(each => each.ItemSpec));
            items.Union(Location.IsNullOrEmpty() ? new string[0] : Location.Select(each => each.ItemSpec));
            foreach (var i in items) {
                Log.LogMessage("Removing '{0}'.".format(i));
                i.TryHardToDelete();
            }
            return true;
        }
    
    }
}