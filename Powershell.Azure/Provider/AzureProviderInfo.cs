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

namespace ClrPlus.Powershell.Azure.Provider {
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Management.Automation;
    using System.Text.RegularExpressions;
    using Powershell.Provider.Base;
    using Powershell.Provider.Utility;

   

    public class AzureProviderInfo : UniversalProviderInfo {
        internal static AzureLocation AzureNamespace = new AzureLocation(null, new Path(), null);
        internal static AzureProviderInfo NamespaceProvider;

        /* public CmdletProvider GetProvider() {
            return this.foo();
        } */

        internal Collection<PSDriveInfo> AddingDrives = new Collection<PSDriveInfo>();

        protected override string Prefix {
            get {
                return AzureDriveInfo.ProviderScheme;
            }
        }

        public AzureProviderInfo(ProviderInfo providerInfo)
            : base(providerInfo) {
        }

        public override ILocation GetLocation(string path) {
            var parsedPath = Path.ParseWithContainer(path);

            // strip off the azure:
            /*
            if (parsedPath.Scheme != string.Empty && parsedPath.Scheme != AzureDriveInfo.ProviderScheme) {
                return AzureLocation.InvalidLocation;
            }*/

            // is this just a empty location?
            if (string.IsNullOrEmpty(parsedPath.HostAndPort)) {
                NamespaceProvider = NamespaceProvider ?? this;
                return AzureNamespace;
            }



            var byAccount = AddingDrives.Union(Drives).Select(each => each as AzureDriveInfo).Where(each =>  each.HostAndPort == parsedPath.HostAndPort || each.ActualHostAndPort == parsedPath.HostAndPort);

            if (!byAccount.Any())
            {
                return AzureLocation.UnknownLocation;
            }

            var byContainer = byAccount.Where(each => each.ContainerName == parsedPath.Container);
            var byFolder = byContainer.Where(each => each.Path.IsSubpath(parsedPath)).OrderByDescending(each => each.RootPath.Length);

            var result = byFolder.FirstOrDefault() ?? byContainer.FirstOrDefault() ?? byAccount.FirstOrDefault();

            

            return new AzureLocation(result, parsedPath, null);
        }

    }
}