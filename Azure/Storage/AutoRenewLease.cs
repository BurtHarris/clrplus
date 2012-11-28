//-----------------------------------------------------------------------
// <copyright company="steve marx">
//     Copyright (c) 2010-2012 steve marx
//     Additional Contributors can be discovered using the 'git log' command.
// </copyright>
// <license>
//      Microsoft Public License (Ms-PL)
//      This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.
//      1. Definitions
//      The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under U.S. copyright law.
//      A "contribution" is the original software, or any additions or changes to the software.
//      A "contributor" is any person that distributes its contribution under this license.
//      "Licensed patents" are a contributor's patent claims that read directly on its contribution.
//      2. Grant of Rights
//      (A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
//      (B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.
//      3. Conditions and Limitations
//      (A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
//      (B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.
//      (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.
//      (D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.
//      (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.
// </license>
//-----------------------------------------------------------------------

namespace Windows.Azure.Storage {
#if TODO
    public class AutoRenewLease : IDisposable {
        public bool HasLease { get { return blob._leaseid != null; } }
        private CloudBlob blob;
        private Thread renewalThread;
        private bool disposed = false;

        public static void DoOnce(CloudBlob blob, Action action) { DoOnce(blob, action, TimeSpan.FromSeconds(5)); }
        public static void DoOnce(CloudBlob blob, Action action, TimeSpan pollingFrequency) {
            // blob.Exists has the side effect of calling blob.FetchAttributes, which populates the metadata collection
            while (!blob.Exists() || blob.Metadata["progress"] != "done") {
                using (var arl = new AutoRenewLease(blob)) {
                    if (arl.HasLease) {
                        action();
                        blob.Metadata["progress"] = "done";
                        blob.SetMetadata(arl.blob._leaseid);
                    }
                    else {
                        Thread.Sleep(pollingFrequency);
                    }
                }
            }
        }

        public AutoRenewLease(CloudBlob blob) {
            this.blob = blob;
            blob.Container.CreateIfNotExist();
            try {
                blob.UploadByteArray(new byte[0], new BlobRequestOptions { AccessCondition = AccessCondition.IfNoneMatch("*") });
            }
            catch (StorageClientException e) {
                if (e.ErrorCode != StorageErrorCode.BlobAlreadyExists
                    && e.StatusCode != HttpStatusCode.PreconditionFailed) // 412 from trying to modify a blob that's leased
                {
                    throw;
                }
            }

            // make sure we're not trying to get a lease on multiple threads concurrently.
            lock (blob) {

                // can't apply a lock if there is already a lock on this instance.
                if (blob._leaseid == null) {
                    this.blob._leaseid = blob.TryAcquireLease();
                    if (HasLease) {
                        renewalThread = new Thread(() => {
                            while (true) {
                                Thread.Sleep(TimeSpan.FromSeconds(40));
                                blob.RenewLease(this.blob._leaseid);
                            }
                        });
                        renewalThread.Start();
                    }
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposed) {
                if (disposing) {
                    if (renewalThread != null) {
                        renewalThread.Abort();
                        blob.ReleaseLease(blob._leaseid);
                        renewalThread = null;
                    }
                }
                disposed = true;
            }
        }

        ~AutoRenewLease() {
            Dispose(false);
        }
    }
#endif
}