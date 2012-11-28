namespace ClrPlus.Azure.Storage {
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.WindowsAzure.Storage.Blob;

    public static class CloudBlobDirectoryExtensions {
        public static IEnumerable<CloudBlobDirectory> ListSubdirectories(this CloudBlobDirectory cloudBlobDirectory) {
            var l = cloudBlobDirectory.Uri.AbsolutePath.Length;
            return (from blob in cloudBlobDirectory.ListBlobs().Select(each => each.Uri.AbsolutePath.Substring(l + 1))
                let i = blob.IndexOf('/')
                where i > -1
                select blob.Substring(0, i)).Distinct().Select(cloudBlobDirectory.GetSubdirectoryReference);
        }
    }
}