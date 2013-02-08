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
    using System.Linq;
    using System.Management.Automation;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Powershell.Provider.Utility;
    using Scripting.Languages.PropertySheet;

    public class AzureDriveInfo : PSDriveInfo {

        public const string SharedAccessTokenUsername = "3BCA5A66-053D-41CF-8F26-3BE606B568C4";
        internal const string ProviderScheme = "azure";
        internal const string ProviderDescription = "azure blob storage";

        internal Path Path;
        internal string Secret;
        private CloudStorageAccount _account;
        private CloudBlobClient _blobStore;
        private readonly IDictionary<string, CloudBlobContainer> _containerCache = new XDictionary<string, CloudBlobContainer>();

        internal string Account {
            get {
                return Path.Account;
            }
        }

        internal string ContainerName {
            get {
                return Path.Container;
            }
        }

        internal string RootPath {
            get {
                return Path.SubPath;
            }
        }

        internal CloudBlobClient CloudFileSystem {
            get {
                if (_blobStore == null) {

                    // is the secret really a SAS token?
                    // Eric : this is the spot to use the token!

                    _account = new CloudStorageAccount(new StorageCredentials(Account, Secret), true);
                    _blobStore = _account.CreateCloudBlobClient();
                }
                return _blobStore;
            }
        }

        internal CloudBlobContainer GetContainer(string containerName) {
            if (_containerCache.ContainsKey(containerName)) {
                return _containerCache[containerName];
            }

            if (CloudFileSystem.GetContainerReference(containerName).Exists()) {
                var container = _blobStore.GetContainerReference(containerName);
                _containerCache.Add(containerName, container);
                return container;
            }
            return null;
        }

        public AzureDriveInfo(Rule aliasRule, ProviderInfo providerInfo, PSCredential psCredential = null)
            : base(GetDriveInfo(aliasRule, providerInfo, psCredential)) {
            Path = new Path {
                Account = aliasRule.HasProperty("key") ? aliasRule["key"].Value : aliasRule.Parameter,
                Container = aliasRule.HasProperty("container") ? aliasRule["container"].Value : "",
                SubPath = aliasRule.HasProperty("root") ? aliasRule["root"].Value.Replace('/', '\\').Replace("\\\\", "\\").Trim('\\') : "",
            };
            Path.Validate();
            Secret = aliasRule.HasProperty("secret") ? aliasRule["secret"].Value : psCredential != null ? psCredential.Password.ToUnsecureString() : null;
        }

        private static PSDriveInfo GetDriveInfo(Rule aliasRule, ProviderInfo providerInfo, PSCredential psCredential) {
            var name = aliasRule.Parameter;
            var account = aliasRule.HasProperty("key") ? aliasRule["key"].Value : name;
            var container = aliasRule.HasProperty("container") ? aliasRule["container"].Value : "";

            if (string.IsNullOrEmpty(container)) {
                return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\".format(ProviderScheme, account), ProviderDescription, psCredential);
            }

            var root = aliasRule.HasProperty("root") ? aliasRule["root"].Value.Replace('/', '\\').Replace("\\\\", "\\").Trim('\\') : "";

            if (string.IsNullOrEmpty(root)) {
                return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\{2}\".format(ProviderScheme, account, container), ProviderDescription, psCredential);
            }

            return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\{2}\{3}\".format(ProviderScheme, account, container, root), ProviderDescription, psCredential);
        }

        public AzureDriveInfo(PSDriveInfo driveInfo)
            : base(driveInfo) {
            Init(driveInfo.Provider, driveInfo.Root, driveInfo.Credential);
        }

        public AzureDriveInfo(string name, ProviderInfo provider, string root, string description, PSCredential credential)
            : base(name, provider, root, description, credential) {
            Init(provider, root, credential);
        }

        private void Init(ProviderInfo provider, string root, PSCredential credential) {
            var parsedPath = Path.ParseWithContainer(root);

            if (string.IsNullOrEmpty(parsedPath.Account) || string.IsNullOrEmpty(parsedPath.Scheme)) {
                Path = parsedPath;
                return; // this is the root azure namespace.
            }

            var pi = provider as AzureProviderInfo;
            if (pi == null) {
                throw new ClrPlusException("Invalid ProviderInfo");
            }

            if (parsedPath.Scheme == ProviderScheme) {
                // it's being passed a full url to a blob storage
                Path = parsedPath;

                if (credential == null || credential.Password == null) {
                    // look for another mount off the same account and container for the credential
                    foreach (var d in pi.Drives.Select(each => each as AzureDriveInfo).Where(d => d.Account == Account && d.ContainerName == ContainerName)) {
                        Secret = d.Secret;
                        return;
                    }
                    // now look for another mount off just the same account for the credential
                    foreach (var d in pi.Drives.Select(each => each as AzureDriveInfo).Where(d => d.Account == Account)) {
                        Secret = d.Secret;
                        return;
                    }
                    throw new ClrPlusException("Missing credential information for {0} mount '{1}'".format(ProviderScheme, root));
                }

                Secret = credential.Password.ToUnsecureString();
                return;
            }

            // otherwise, it's an sub-folder off of another mount.
            foreach (var d in pi.Drives.Select(each => each as AzureDriveInfo).Where(d => d.Name == parsedPath.Scheme)) {
                Path = new Path {
                    Account = d.Account,
                    Container = string.IsNullOrEmpty(d.ContainerName) ? parsedPath.Account : d.ContainerName,
                    SubPath = string.IsNullOrEmpty(d.RootPath) ? parsedPath.SubPath : d.RootPath + '\\' + parsedPath.SubPath
                };
                Path.Validate();
                Secret = d.Secret;
                return;
            }
        }
    }
}