// ----------------------------------------------------------------------
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

namespace ClrPlus.Scripting.MsBuild.Building {
    using Languages.PropertySheetV3.Mapping;
    using Microsoft.Build.Construction;
    using MsBuild.Utility;
    using Utility;

    internal static class ProjectTargetElementExtensions {
        internal static CustomPropertyList EnvironmentList(this ProjectTargetElement target) {
            return null;
        }

        internal static CustomPropertyList Uses(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor DefaultFlag(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor BuildCommand(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor CleanCommand(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor Platform(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor UsesTool(this ProjectTargetElement target) {
            return null;
        }

        internal static CustomPropertyList ProducesTargets(this ProjectTargetElement target) {
            return null;
        }

        internal static CustomPropertyList GenerateFiles(this ProjectTargetElement target) {
            return null;
        }

        internal static CustomPropertyList RequiresPackages(this ProjectTargetElement target) {
            return null;
        }

        internal static Accessor Condition(this ProjectTargetElement target) {
            return null;
        }
    }
}