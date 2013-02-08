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
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation.Provider;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Core.Utility;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Powershell.Provider.Base;
    using Path = Powershell.Provider.Utility.Path;

    public class AzureLocation : Location {
        public static AzureLocation InvalidLocation = new AzureLocation(null, new Path(), null) {
            _invalidLocation = true
        };

        public static AzureLocation UnknownLocation = new AzureLocation(null, new Path(), null) {
            _invalidLocation = true
        };

        private bool _invalidLocation;
        private readonly AsyncLazy<IListBlobItem> _cloudItem;
        private readonly AzureDriveInfo _driveInfo;
        private CloudBlobContainer _cloudContainer;
        private Stream _blobStream;

        protected bool IsRootNamespace {
            get {
                return !_invalidLocation && _driveInfo == null;
            }
        }

        protected bool IsAccount {
            get {
                return !_invalidLocation && !IsRootNamespace && Path.Container.IsNullOrEmpty() && Path.SubPath.IsNullOrEmpty();
            }
        }

        protected CloudBlockBlob FileBlob {
            get {
                return _cloudItem.Value as CloudBlockBlob;
            }
        }

        protected CloudBlobDirectory DirectoryBlob {
            get {
                return _cloudItem.Value as CloudBlobDirectory;
            }
        }

        protected CloudBlobContainer CloudContainer {
            get {
                if (!_invalidLocation && _cloudContainer == null) {
                    /*
                    if (_driveInfo.CloudFileSystem == null || Path.Container.IndexOfAny(Wildcards) > -1 || !_driveInfo.CloudFileSystem.ContainerExists(Path.Container)) {
                        return null;
                    }

                    _cloudContainer = _driveInfo.CloudFileSystem[Path.Container];
                     * */
                    if (_driveInfo.CloudFileSystem != null && Path.Container.IndexOfAny(Wildcards) == -1) {
                        _cloudContainer = _driveInfo.GetContainer(Path.Container);
                    }
                }
                return _cloudContainer;
            }
        }

        public bool IsContainer {
            get {
                if (_invalidLocation || string.IsNullOrEmpty(Path.Container) || !string.IsNullOrEmpty(Path.SubPath)) {
                    return false;
                }
                return CloudContainer != null;
            }
        }

        public bool IsDirectory {
            get {
                return !_invalidLocation && !string.IsNullOrEmpty(Path.SubPath) && _cloudItem.Value is CloudBlobDirectory;
            }
        }

        public string MD5 {
            get {
                if (FileBlob == null) {
                    return string.Empty;
                }
                var result = FileBlob.Properties.ContentMD5;
                return string.IsNullOrEmpty(result) ? FileBlob.Metadata["MD5"] ?? string.Empty : result;
            }
        }

        public string MimeType {
            get {
                return FileBlob != null ? FileBlob.Properties.ContentType : string.Empty;
            }
        }

        public AzureLocation(AzureDriveInfo driveInfo, Path path, IListBlobItem cloudItem) {
            _driveInfo = driveInfo;
            Path = path;
            Path.Validate();

            if (cloudItem != null) {
                _cloudItem = new AsyncLazy<IListBlobItem>(() => cloudItem);
            } else {
                if (IsRootNamespace || IsAccount || IsContainer) {
                    // azure namespace mount.
                    _cloudItem = new AsyncLazy<IListBlobItem>(() => null);
                    return;
                }

                _cloudItem = new AsyncLazy<IListBlobItem>(() => {
                    if (CloudContainer == null) {
                        return null;
                    }
                    // not sure if it's a file or a directory.
                    if (path.EndsWithSlash) {
                        // can't be a file!
                        return CloudContainer.GetDirectoryReference(Path.SubPath);
                    }
                    // check to see if it's a file.
                    var blobRef = CloudContainer.GetBlobReferenceFromServer(Path.SubPath);
                    if (blobRef.BlobType == BlobType.BlockBlob) {
                        return blobRef;
                    }

                    // well, we know it's not a file, container, or account. 
                    // it could be a directory (but the only way to really know that is to see if there is any files that have this as a parent path)
                    var dirRef = CloudContainer.GetDirectoryReference(Path.SubPath);
                    if (dirRef.ListBlobs().Any()) {
                        return dirRef;
                    }

                    // it really didn't match anything, we'll return the reference to the blob in case we want to write to it.
                    return blobRef;
                });
                _cloudItem.InitializeAsync();
            }
        }

        public override void Delete(bool recurse) {
            if (IsFile) {
                FileBlob.DeleteIfExists();
                return;
            }
            if (IsDirectory && recurse) {
                foreach (var d in GetDirectories(true)) {
                    d.Delete(true);
                }

                foreach (var d in GetFiles(false)) {
                    d.Delete(false);
                }
            }
        }

        public override string Name {
            get {
                return _invalidLocation ? "<invalid>"
                    : IsRootNamespace ? AzureDriveInfo.ProviderScheme + ":"
                        : IsAccount ? _driveInfo.Account
                            : IsContainer ? Path.Container
                                : Path.Name;
            }
        }

        private string _absolutePath;

        public override string AbsolutePath {
            get {
                return _absolutePath ?? (_absolutePath = _invalidLocation ? "???"
                    : IsRootNamespace ? @"{0}:\".format(AzureDriveInfo.ProviderScheme)
                        : IsAccount ? @"{0}:\{1}\".format(AzureDriveInfo.ProviderScheme, Path.Account)
                            : IsContainer ? @"{0}:\{1}\{2}".format(AzureDriveInfo.ProviderScheme, Path.Account, Path.Container)
                                : IsDirectory ? @"{0}:\{1}\{2}\{3}\".format(AzureDriveInfo.ProviderScheme, Path.Account, Path.Container, Path.SubPath)
                                    : @"{0}:\{1}\{2}\{3}".format(AzureDriveInfo.ProviderScheme, Path.Account, Path.Container, Path.SubPath));
            }
        }

        public override string Url {
            get {
                return (_invalidLocation || IsRootNamespace || IsAccount) ? string.Empty : IsContainer ? CloudContainer.Uri.AbsoluteUri : IsDirectory || IsFile ? _cloudItem.Value.Uri.AbsoluteUri : string.Empty;
            }
        }

        public override string Type {
            get {
                return _invalidLocation ? "<invalid>" : IsRootNamespace ? "<root>" : IsAccount ? "<account>" : IsContainer ? "<container>" : (IsDirectory ? "<dir>" : (IsFile ? MimeType : "<?>"));
            }
        }

        public override long Length {
            get {
                return FileBlob != null ? FileBlob.Properties.Length : -1;
            }
        }

        public override DateTime TimeStamp {
            get {
                if (FileBlob != null) {
                    return FileBlob.Properties.LastModified.Value.UtcDateTime.ToLocalTime();
                }
                return DateTime.MinValue;
            }
        }

        public override bool IsItemContainer {
            get {
                return IsRootNamespace || IsAccount || IsContainer || IsDirectory;
            }
        }

        public override bool IsFileContainer {
            get {
                return IsContainer || IsDirectory;
            }
        }

        public override bool IsFile {
            get {
                return !_invalidLocation && FileBlob != null && FileBlob.Properties.Length > 0;
            }
        }

        public override bool Exists {
            get {
                return !_invalidLocation && IsRootNamespace || IsAccount || IsContainer || IsDirectory || IsFile;
            }
        }

        public override IEnumerable<ILocation> GetDirectories(bool recurse) {
            if (_invalidLocation) {
                return Enumerable.Empty<AzureLocation>();
            }

            if (recurse) {
                var dirs = GetDirectories(false);
                return dirs.Union(dirs.SelectMany(each => each.GetDirectories(true)));
            }

            if (IsRootNamespace) {
                // list accounts we know

                return AzureProviderInfo.NamespaceProvider.Drives
                                        .Select(each => each as AzureDriveInfo)
                                        .Where(each => !string.IsNullOrEmpty(each.Account))
                                        .Distinct(new ClrPlus.Core.Extensions.EqualityComparer<AzureDriveInfo>((a, b) => a.Account == b.Account, a => a.Account.GetHashCode()))
                                        .Select(each => new AzureLocation(each, new Path {
                                            Account = each.Account,
                                            Container = string.Empty,
                                            SubPath = string.Empty,
                                        }, null));
            }

            if (IsAccount) {
                return _driveInfo.CloudFileSystem.ListContainers().Select(each => new AzureLocation(_driveInfo, new Path {
                    Account = Path.Account,
                    Container = each.Name,
                }, null));
            }

            if (IsContainer) {
                return ListSubdirectories(CloudContainer).Select(each => new AzureLocation(_driveInfo, new Path {
                    Account = Path.Account,
                    Container = Path.Container,
                    SubPath = Path.ParseUrl(each.Uri).Name,
                }, each));
            }

            if (IsDirectory) {
                var cbd = (_cloudItem.Value as CloudBlobDirectory);

                return cbd == null
                    ? Enumerable.Empty<ILocation>()
                    : ListSubdirectories(cbd).Select(each => new AzureLocation(_driveInfo, new Path {
                        Account = Path.Account,
                        Container = Path.Container,
                        SubPath = Path.SubPath + '\\' + Path.ParseUrl(each.Uri).Name,
                    }, each));
            }

            return Enumerable.Empty<AzureLocation>();
        }

        public static IEnumerable<CloudBlobDirectory> ListSubdirectories(CloudBlobContainer cloudBlobContainer) {
            var l = cloudBlobContainer.Uri.AbsolutePath.Length;
            return (from blob in cloudBlobContainer.ListBlobs().Select(each => each.Uri.AbsolutePath.Substring(l + 1))
                let i = blob.IndexOf('/')
                where i > -1
                select blob.Substring(0, i)).Distinct().Select(cloudBlobContainer.GetDirectoryReference);
        }

        public static IEnumerable<CloudBlobDirectory> ListSubdirectories(CloudBlobDirectory cloudBlobDirectory) {
            var l = cloudBlobDirectory.Uri.AbsolutePath.Length;
            return (from blob in cloudBlobDirectory.ListBlobs().Select(each => each.Uri.AbsolutePath.Substring(l + 1))
                let i = blob.IndexOf('/')
                where i > -1
                select blob.Substring(0, i)).Distinct().Select(cloudBlobDirectory.GetSubdirectoryReference);
        }

        public override IEnumerable<ILocation> GetFiles(bool recurse) {
            if (recurse) {
                return GetFiles(false).Union(GetDirectories(false).SelectMany(each => each.GetFiles(true)));
            }

            if (IsContainer) {
                return CloudContainer.ListBlobs().Where(each => each is ICloudBlob && !(each as ICloudBlob).Name.EndsWith("/")).Select(each => new AzureLocation(_driveInfo, new Path {
                    Account = Path.Account,
                    Container = Path.Container,
                    SubPath = Path.ParseUrl(each.Uri).Name,
                }, each));
            }

            if (IsDirectory) {
                var cbd = (_cloudItem.Value as CloudBlobDirectory);
                return cbd == null ? Enumerable.Empty<ILocation>() : cbd.ListBlobs().Where(each => each is ICloudBlob && !(each as ICloudBlob).Name.EndsWith("/")).Select(each => new AzureLocation(_driveInfo, new Path {
                    Account = Path.Account,
                    Container = Path.Container,
                    SubPath = Path.SubPath + '\\' + Path.ParseUrl(each.Uri).Name,
                }, each));
            }
            return Enumerable.Empty<AzureLocation>();
        }

        public void Dispose() {
            Close();
        }

        public void Close() {
            if (_blobStream != null) {
                _blobStream.Close();
                _blobStream.Dispose();
                _blobStream = null;
            }
        }

        public override Stream Open(FileMode mode) {
            if (_blobStream != null) {
                return _blobStream;
            }

            switch (mode) {
                case FileMode.Create:
                case FileMode.CreateNew:
                case FileMode.Truncate:
                    var b = FileBlob;
                    if (b == null) {
                    }
                    return _blobStream = FileBlob.OpenWrite();

                case FileMode.Open:
                    if (!Exists || !IsFile) {
                        throw new ClrPlusException("Path not found '{0}'".format(AbsolutePath));
                    }
                    return _blobStream = FileBlob.OpenRead();
            }
            throw new ClrPlusException("Unsupported File Mode.");
        }

        public override ILocation GetChildLocation(string relativePath) {
            return new AzureLocation(_driveInfo, Path.ParseWithContainer(AbsolutePath + "\\" + relativePath), null);
        }

        public override IContentReader GetContentReader() {
            return new ContentReader(Open(FileMode.Open), Length);
        }

        public override IContentWriter GetContentWriter() {
            return new UniversalContentWriter(Open(FileMode.Create));
        }

        public override void ClearContent() {
        }
    }
}