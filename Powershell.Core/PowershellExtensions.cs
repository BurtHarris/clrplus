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

namespace ClrPlus.Powershell.Core {
    using System.Management.Automation.Runspaces;

    public static class PowershellExtensions {
        public static dynamic Dynamic(this RunspacePool pool) {
            if (pool == null) {
                return new DynamicPowershell();
            }
            return new DynamicPowershell(pool);
        }

        public static dynamic Dynamic(this Runspace runspace) {
            if(runspace== null) {
                return new DynamicPowershell();
            }

            return new DynamicPowershell(runspace);
        }
    }
}