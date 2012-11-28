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
    public static class CloudBlobExtensions {
#if TODO
        private static readonly char[] PathChars = new[] {
            '\\', '/'
        };

        internal string _leaseid;
        internal int _leaseReferenceCount = 0;
        internal ManualResetEvent _leaseReleaseEvent = new ManualResetEvent(false);

        public void Lock(Action<CloudBlob> action) {
            Lock(action, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(5));
        }

        public void Lock(Action<CloudBlob> action, TimeSpan maxWait) {
            Lock(action, maxWait, TimeSpan.FromSeconds(5));
        }

        public void Lock(Action<CloudBlob> action, TimeSpan maxWait, TimeSpan pollingFrequency) {
            var startTime = DateTime.Now;
            Task renewTask = null;

            do {
                lock (this) {
                    // does this blob already have a lease?
                    if (_leaseid != null) {
                        // inc the ref count on the lease.
                        _leaseReferenceCount++;
                    } else {
                        // can we get the lease?
                        if (!Container.Exists()) {
                            Container.CreateIfNotExist();
                        }

                        try {
                            UploadByteArray(new byte[0], new BlobRequestOptions { AccessCondition = AccessCondition.IfNoneMatch("*") });
                        }
                        catch (StorageClientException e) {
                            if (e.ErrorCode != StorageErrorCode.BlobAlreadyExists && e.StatusCode != HttpStatusCode.PreconditionFailed) {
                                // 412 from trying to modify a blob that's leased
                                Thread.Sleep(pollingFrequency);
                                continue;
                            }
                        }

                        _leaseid = TryAcquireLease();
                        _leaseReferenceCount++;

                        if (_leaseid != null) {
                            renewTask = Task.Factory.StartNew(() => {
                                while (!_leaseReleaseEvent.WaitOne(40000) && _leaseReferenceCount > 0) {
                                    _leaseReleaseEvent.Reset();
                                    RenewLease(_leaseid);
                                }
                                lock (this) {
                                    ReleaseLease(_leaseid);
                                    _leaseid = null;
                                    _leaseReferenceCount = 0;
                                    _leaseReleaseEvent.Reset();
                                }
                            }, TaskCreationOptions.AttachedToParent);
                        } else {
                            // no, block on this thread
                            // and continue
                            Thread.Sleep(pollingFrequency);
                            continue;
                        }
                    }
                }

                // we have a lease, do our work.
                action(this);

                lock( this ) {
                    _leaseReferenceCount--;
                    if( _leaseReferenceCount <= 0) {
                        // we're the last consumer of the lease. remove it and gtfo.
                        _leaseReleaseEvent.Set();
                    }
                    return;
                }
               
            } while (DateTime.Now.Subtract(startTime) < maxWait);
        }

       
        public string Filename {
            get {
                return Name.Substring(Name.LastIndexOfAny(PathChars) + 1);
            }
        }

        public string ContainerName {
            get {
                return Container.Name;
            }
        }

        public string MD5 {
            get {
                return Properties.ContentMD5;
            }
        }

        public DateTime Timestamp {
            get {
                return Properties.LastModifiedUtc.ToLocalTime();
            }
        }

        public long Length {
            get {
                return Properties.Length;
            }
        }

        public bool Exists() {
            try {
                FetchAttributes();
                return true;
            }
            catch (StorageClientException e) {
                if (e.ErrorCode == StorageErrorCode.ResourceNotFound) {
                    return false;
                }
                else {
                    throw;
                }
            }
        }

        private static string LookupMimeType(string extension) {
            extension = extension.ToLower();
            using (var key = Registry.ClassesRoot.OpenSubKey("MIME\\Database\\Content Type")) {
                if (key != null) {
                    foreach (var subkeyName in key.GetSubKeyNames()) {
                        using (var subkey = key.OpenSubKey(subkeyName)) {
                            if (extension.Equals(subkey.GetValue("Extension"))) {
                                return subkeyName;
                            }
                        }
                    }
                }
            }
            return "";
        }

        public void CopyToFile(string localFilename, Action<long> progress = null) {
            if (this.Exists()) {
                var md5 = string.Empty;
                try {
                    FetchAttributes();

                    md5 = Properties.ContentMD5;
                    if (string.IsNullOrEmpty(md5)) {
                        if (Metadata.AllKeys.Contains("MD5")) {
                            md5 = Metadata["MD5"];
                        }
                    }
                } catch {
                }

                if (File.Exists(localFilename)) {
                    if (string.Equals(localFilename.GetFileMD5(), md5, StringComparison.CurrentCultureIgnoreCase)) {
                        if (progress != null) {
                            progress(100);
                        }
                        return;
                    }
                    localFilename.TryHardToDelete();
                }

                try {
                    using (var stream = new ProgressStream(new FileStream(localFilename, FileMode.CreateNew), Properties.Length, progress ?? (p => {
                    }))) {
                        DownloadToStream(stream);
                    }
                } catch (StorageException e) {
                    if (e.ErrorCode == StorageErrorCode.BlobAlreadyExists || e.ErrorCode == StorageErrorCode.ConditionFailed) {
                        throw new ApplicationException("Concurrency Violation", e);
                    }
                    throw;
                }
            }
        }

        public void CopyFromFile(string localFilename, bool gzip = false, Action<long> progress = null) {
            if (!File.Exists(localFilename)) {
                throw new FileNotFoundException("local filename does not exist", localFilename);
            }

            var md5 = string.Empty;
            try {
                FetchAttributes();

                md5 = Properties.ContentMD5;
                if (string.IsNullOrEmpty(md5)) {
                    if (Metadata.AllKeys.Contains("MD5")) {
                        md5 = Metadata["MD5"];
                    }
                }
            } catch {
            }

            var localMD5 = localFilename.GetFileMD5();

            if (!string.Equals(md5, localMD5, StringComparison.CurrentCultureIgnoreCase)) {
                // different file
                Properties.ContentType = LookupMimeType(Path.GetExtension(localFilename));
                if (gzip) {
                    Properties.ContentEncoding = "gzip";
                }

                try {
                    // copy to tmp file to compress to gz.
                    if (gzip) {
                        var localGZFilename = localFilename.GenerateTemporaryFilename();

                        using (var gzStream = new GZipStream(
                            new FileStream(localGZFilename, FileMode.CreateNew), CompressionMode.Compress, CompressionLevel.BestCompression)) {
                            using (var fs = new FileStream(localFilename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                                fs.CopyTo(gzStream);
                                localFilename = localGZFilename;
                            }
                        }
                    }

                    using (var stream = new ProgressStream(new FileStream(localFilename, FileMode.Open, FileAccess.Read, FileShare.Read), progress ?? (p => {
                    }))) {
                        UploadFromStream(stream);
                        if (Metadata.AllKeys.Contains("MD5")) {
                            Metadata["MD5"] = localMD5;
                        } else {
                            Metadata.Add("MD5", localMD5);
                        }
                        SetMetadata();
                    }
                } catch (StorageException e) {
                    if (e.ErrorCode == StorageErrorCode.BlobAlreadyExists || e.ErrorCode == StorageErrorCode.ConditionFailed) {
                        throw new ApplicationException("Concurrency Violation", e);
                    }
                    throw;
                }
            } else {
                if (progress != null) {
                    progress(100);
                }
            }
        }

        public void WriteText(string content, bool gzip = false, Action<long> progress = null) {
            content = content ?? "";

            var md5 = string.Empty;
            try {
                FetchAttributes();

                md5 = Properties.ContentMD5;
                if (string.IsNullOrEmpty(md5)) {
                    if (Metadata.AllKeys.Contains("MD5")) {
                        md5 = Metadata["MD5"];
                    }
                }
            } catch {
            }

            var localMD5 = content.MD5Hash();

            if (!string.Equals(md5, localMD5, StringComparison.CurrentCultureIgnoreCase)) {
                // different file
                Properties.ContentType = LookupMimeType(Path.GetExtension(Name));
                if (gzip) {
                    Properties.ContentEncoding = "gzip";
                }

                try {
                    using (var stream = new ProgressStream(
                        gzip ? 
                            (Stream)new GZipStream(new MemoryStream(content.ToByteArray()), CompressionMode.Compress, CompressionLevel.BestCompression) 
                            : new MemoryStream(content.ToByteArray()), progress ?? (p => {
                    }))) {
                        UploadFromStream(stream);
                        if (Metadata.AllKeys.Contains("MD5")) {
                            Metadata["MD5"] = localMD5;
                        } else {
                            Metadata.Add("MD5", localMD5);
                        }
                        SetMetadata();
                    }
                } catch (StorageException e) {
                    if (e.ErrorCode == StorageErrorCode.BlobAlreadyExists || e.ErrorCode == StorageErrorCode.ConditionFailed) {
                        throw new ApplicationException("Concurrency Violation", e);
                    }
                    throw;
                }
            } else {
                if (progress != null) {
                    progress(100);
                }
            }
        }

        public string TryAcquireLease() {
            try {
                return AcquireLease();
            }
            catch (WebException e) {
                if (((HttpWebResponse)e.Response).StatusCode != HttpStatusCode.Conflict) // 409, already leased
                {
                    throw;
                }
                e.Response.Close();
                return null;
            }
        }

        public string AcquireLease() {
            var creds = ServiceClient.Credentials;
            var transformedUri = new Uri(creds.TransformUri(Uri.AbsoluteUri));
            var req = BlobRequest.Lease(transformedUri,
                90, // timeout (in seconds)
                LeaseAction.Acquire, // as opposed to "break" "release" or "renew"
                null); // name of the existing lease, if any
            ServiceClient.Credentials.SignRequest(req);

            using (var response = req.GetResponse()) {
                return response.Headers["x-ms-lease-id"];
            }
        }

        private void DoLeaseOperation(string leaseId, LeaseAction action) {
            var creds = ServiceClient.Credentials;
            var transformedUri = new Uri(creds.TransformUri(Uri.AbsoluteUri));
            var req = BlobRequest.Lease(transformedUri, 90, action, leaseId);
            creds.SignRequest(req);
            req.GetResponse().Close();
        }

        public void ReleaseLease(string leaseId) {
            DoLeaseOperation(leaseId, LeaseAction.Release);
        }

        public bool TryRenewLease(string leaseId) {
            try {
                RenewLease(leaseId);
                return true;
            }
            catch {
                return false;
            }
        }

        public void RenewLease(string leaseId) {
            DoLeaseOperation(leaseId, LeaseAction.Renew);
        }

        public void BreakLease() {
            DoLeaseOperation(null, LeaseAction.Break);
        }

        // NOTE: This method doesn't do everything that the regular UploadText does.
        // Notably, it doesn't update the BlobProperties of the blob (with the new
        // ETag and LastModifiedTimeUtc). It also, like all the methods in this file,
        // doesn't apply any retry logic. Use this at your own risk!
        public void _UploadText(string text, string leaseId) {
            string url = Uri.AbsoluteUri;
            if (ServiceClient.Credentials.NeedsTransformUri) {
                url = ServiceClient.Credentials.TransformUri(url);
            }
            var req = BlobRequest.Put(new Uri(ServiceClient.Credentials.TransformUri(Uri.AbsoluteUri)),
                90, new BlobProperties(), BlobType.BlockBlob, leaseId, 0);
            using (var writer = new StreamWriter(req.GetRequestStream())) {
                writer.Write(text);
            }
            ServiceClient.Credentials.SignRequest(req);
            req.GetResponse().Close();
        }

        public void SetMetadata(string leaseId) {
            var req = BlobRequest.SetMetadata(new Uri(ServiceClient.Credentials.TransformUri(Uri.AbsoluteUri)), 90, leaseId);
            foreach (string key in Metadata.Keys) {
                req.Headers.Add("x-ms-meta-" + key, Metadata[key]);
            }
            ServiceClient.Credentials.SignRequest(req);
            req.GetResponse().Close();
        }
#endif
    }
}