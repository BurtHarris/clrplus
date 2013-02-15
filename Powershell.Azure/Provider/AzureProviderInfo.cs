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
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Management.Automation;
    using Powershell.Provider.Base;
    using Powershell.Provider.Utility;

    /*
    public interface ILocationProvider {
        ILocation GetLocation(string path);
    }

    public class FilesystemLocationProvider : ILocationProvider {
        ILocation GetLocation(string path) {
            return new FileLocation(path);
        }
    }
    */

    public abstract class FileLocation : Location {
    }

    /*
    public class FilesystemFileItem : IFileItem {

        private ProviderInfo _providerInfo;
        private dynamic _provider;
        private dynamic Provider { get {
            return _provider ?? new AccessPrivateWrapper(_provider = _providerInfo.CreateCmdletProvider() as NavigationCmdletProvider);
        }}
        
        public FilesystemFileItem(ProviderInfo providerInfo) {
            _providerInfo = providerInfo;
        }

        public bool ItemExists(string path) {
            return Provider.ItemExists(path);
        }

        public bool ItemIsContainer(string path) {
            return Provider.IsItemContainer(path);
        }

        public Stream GetSourceStream(string path) {
            return File.OpenRead(path);
        }

        public Stream GetDestinationStream(string path) {
            if( File.Exists( path )) {
                path.TryHardToDelete();
            }
            if( File.Exists( path )) {
                throw new CoAppException("Unable to overwrite file '{0}'".format(path));
            }

            return File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        }
    }
    public static class ProviderInfoExtensions {
        public static CmdletProvider CreateCmdletProvider(this ProviderInfo providerInfo) {
            var createInstanceMethod = providerInfo.GetType().GetMethod("CreateInstance", BindingFlags.NonPublic);
            var result = createInstanceMethod.Invoke(providerInfo, null);
            return result as CmdletProvider;
        }
    }
     
     */

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

            var byAccount = AddingDrives.Union(Drives).Select(each => each as AzureDriveInfo).Where(each => each.HostAndPort == parsedPath.HostAndPort);

            if (!byAccount.Any()) {
                return AzureLocation.UnknownLocation;
            }

            var byContainer = byAccount.Where(each => each.ContainerName == parsedPath.Container);
            var byFolder = byContainer.Where(each => each.Path.IsSubpath(parsedPath)).OrderByDescending(each => each.RootPath.Length);

            return new AzureLocation(byFolder.FirstOrDefault() ?? byContainer.FirstOrDefault() ?? byAccount.FirstOrDefault(), parsedPath, null);
        }
    }
}