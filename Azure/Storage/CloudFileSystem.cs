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

namespace ClrPlus.Azure.Storage {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class CloudFileSystem {
        private static CloudStorageAccount _account;
        private static CloudBlobClient _blobStore;

        public CloudFileSystem(string accountName, string accountKey) {
            _account = new CloudStorageAccount(new StorageCredentials(accountName, accountKey), true);
            _blobStore = _account.CreateCloudBlobClient();
        }

        public CloudBlobContainer this[string index] {
            get {
                var container = _blobStore.GetContainerReference(index.ToLower());
                if (!container.Exists()) {
                    container.CreateIfNotExists();

                    var stream = new MemoryStream();
                    try {
                        container.GetBlobReferenceFromServer("__testblob99").UploadFromStream(stream);
                        container.GetBlobReferenceFromServer("__testblob99").Delete();

                        var permissions = container.GetPermissions();
                        permissions.PublicAccess = BlobContainerPublicAccessType.Container;
                        container.SetPermissions(permissions);
                    } catch /* (StorageClientException e) */ {
                        throw new Exception(string.Format("Failed creating container '{0}'. This may happen if the container was recently deleted (in which case, try again).", index));
                    }
                }
                return container;
            }
        }

        public bool ContainerExists(string name) {
            return _blobStore.GetContainerReference(name).Exists();
        }

        public void RemoveContainer(string name) {
            if (ContainerExists(name)) {
                var container = _blobStore.GetContainerReference(name);
                container.Delete();
            }
        }

        public IEnumerable<string> Names {
            get {
                return _blobStore.ListContainers().Select(each => each.Name);
            }
        }

        public CloudBlobClient ServiceClient {
            get {
                return _blobStore;
            }
        }
    }
}