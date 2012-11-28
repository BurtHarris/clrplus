//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
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
    using System.Linq;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    public static class CloudBlobContainerExtensions {
        public static IEnumerable<string> GetNames(this CloudBlobContainer cloudBlobContainer) {
            var containerUri = new Uri(cloudBlobContainer.Uri.AbsoluteUri + "/" + cloudBlobContainer.Name);
            return cloudBlobContainer.ListBlobs(useFlatBlobListing: true).Select(each => containerUri.MakeRelativeUri(each.Uri).ToString());
        }

        public static IEnumerable<CloudBlobDirectory> ListSubdirectories(this CloudBlobContainer cloudBlobContainer) {
            var l = cloudBlobContainer.Uri.AbsolutePath.Length;
            return (from blob in cloudBlobContainer.ListBlobs().Select(each => each.Uri.AbsolutePath.Substring(l + 1))
                let i = blob.IndexOf('/')
                where i > -1
                select blob.Substring(0, i)).Distinct().Select(cloudBlobContainer.GetDirectoryReference);
        }

#if TODO
        public CloudBlob this[string index] {
            get {
                return GetBlobReference(index);
            }
        }
#endif

        public static bool Exists(this CloudBlobContainer cloudBlobContainer) {
            try {
                cloudBlobContainer.FetchAttributes();
                return true;
            } catch (StorageException e) {
                if (e.RequestInformation.HttpStatusCode == 404) {
                    return false;
                } else {
                    throw;
                }
            }
        }
    }
}