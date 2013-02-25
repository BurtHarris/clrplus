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

namespace ClrPlus.Powershell.Provider.Utility {
    using System;
    using System.IO;

    /// <summary>
    ///     Wraps another stream and provides reporting for when bytes are read or written to the stream.
    /// </summary>
    public class ProgressStream : Stream {
        private readonly Stream _innerStream;

        /// <summary>
        ///     Creates a new ProgressStream supplying the stream for it to report on.
        /// </summary>
        /// <param name="streamToReportOn"> The underlying stream that will be reported on when bytes are read or written. </param>
        public ProgressStream(Stream streamToReportOn) {
            if (streamToReportOn != null) {
                _innerStream = streamToReportOn;
            } else {
                throw new ArgumentNullException("streamToReportOn");
            }
        }

        /// <summary>
        ///     Raised when bytes are read from the stream.
        /// </summary>
        public event ProgressStreamReportDelegate BytesRead;

        /// <summary>
        ///     Raised when bytes are written to the stream.
        /// </summary>
        public event ProgressStreamReportDelegate BytesWritten;

        /// <summary>
        ///     Raised when bytes are either read or written to the stream.
        /// </summary>
        public event ProgressStreamReportDelegate BytesMoved;

        protected virtual void OnBytesRead(int bytesMoved) {
            if (BytesRead != null) {
                var args = new ProgressStreamReportEventArgs(bytesMoved, _innerStream.Length, _innerStream.Position, true);
                BytesRead(this, args);
            }
        }

        protected virtual void OnBytesWritten(int bytesMoved) {
            if (BytesWritten != null) {
                long length = 0;
                try {
                    length = _innerStream.Length;
                } catch {
                    
                }
                var args = new ProgressStreamReportEventArgs(bytesMoved, length, _innerStream.Position, false);
                BytesWritten(this, args);
            }
        }

        protected virtual void OnBytesMoved(int bytesMoved, bool isRead) {
            if (BytesMoved != null) {
                long length = 0;
                try
                {
                    length = _innerStream.Length;
                }
                catch
                {

                }
                var args = new ProgressStreamReportEventArgs(bytesMoved, length, _innerStream.Position, isRead);
                BytesMoved(this, args);
            }
        }

        public override bool CanRead {
            get {
                return _innerStream.CanRead;
            }
        }

        public override bool CanSeek {
            get {
                return _innerStream.CanSeek;
            }
        }

        public override bool CanWrite {
            get {
                return _innerStream.CanWrite;
            }
        }

        public override long Length {
            get {
                return _innerStream.Length;
            }
        }

        public override long Position {
            get {
                return _innerStream.Position;
            }
            set {
                _innerStream.Position = value;
            }
        }

        public override void Flush() {
            _innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) {
            int bytesRead = _innerStream.Read(buffer, offset, count);

            OnBytesRead(bytesRead);
            OnBytesMoved(bytesRead, true);

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin) {
            return _innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value) {
            _innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            _innerStream.Write(buffer, offset, count);
            
            OnBytesWritten(count);
            OnBytesMoved(count, false);
        }

        public override void Close() {
            _innerStream.Close();
            base.Close();
        }
    }
}