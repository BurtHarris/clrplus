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

namespace ClrPlus.Powershell.Provider.Base {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using Core.Extensions;
    using Scripting.Languages.PropertySheet;

    public abstract class UniversalProviderInfo : ProviderInfo, ILocationResolver {
        protected readonly PropertySheet PropertySheet;
        protected abstract string Prefix {get;}

        protected UniversalProviderInfo(ProviderInfo providerInfo) : base(providerInfo) {
            try {
                PropertySheet = PropertySheet.Parse(@"@import ""pstab.properties"";", "default");
            } catch (Exception) {

                PropertySheet = new PropertySheet();
            }
            
        }

        public IEnumerable<Rule> Aliases {
            get {
                return PropertySheet.Rules.Where(each => each.Name == Prefix).Where(alias => !alias.HasProperty("disabled") || !alias["disabled"].Value.IsTrue()).Reverse();
            }
        }

        public abstract ILocation GetLocation(string path);
    }
}