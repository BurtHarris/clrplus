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

namespace ClrPlus.Powershell.Provider.Base {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Management.Automation.Provider;
    using System.Text;
    using Core.Utility;
    using Utility;

    public class ContentReader : IContentReader {
        private Stream _stream;
        private long _length;
        private List<string> _lineBuffer = new List<string>();
        private Decoder _decoder;

        public ContentReader(Stream stream, long length, Encoding encoding = null) {
            _stream = stream;
            _length = length;
            if (encoding != null) {
                _decoder = encoding.GetDecoder();
            }
        }

        public void Dispose() {
            Close();
        }

        private long RemainingBytes {
            get {
                return _length - _stream.Position;
            }
        }

        public IList Read(long linesToRead) {
            if (linesToRead == 0) {
                LoadBuffer(RemainingBytes);
                linesToRead = _lineBuffer.Count;
            }

            while (_lineBuffer.Count < linesToRead && RemainingBytes > 0) {
                LoadBuffer();
            }

            linesToRead = Math.Min(linesToRead, _lineBuffer.Count);

            if (linesToRead > 0) {
                lock (_lineBuffer) {
                    var result = _lineBuffer.GetRange(0, (int)linesToRead);
                    _lineBuffer.RemoveRange(0, (int)linesToRead);
                    return result;
                }
            }

            return new List<string>();
        }

        private void LoadBuffer(long bytesToRead = (256*1024)) {
            bytesToRead = Math.Min(bytesToRead, RemainingBytes);
            var buffer = new byte[bytesToRead];

            var read = _stream.Read(buffer, 0, (int)bytesToRead);
            var chars = new char[read];
            read = GetDecoder(buffer).GetChars(buffer, 0, read, chars, 0);
            lock (_lineBuffer) {
                _lineBuffer.AddRange(new String(chars, 0, read).Replace("\r\n", "\r").Split('\r'));
            }
        }

        private Decoder GetDecoder(byte[] buffer) {
            return _decoder ?? (_decoder = buffer.GetTextEncoding().GetDecoder());
        }

        public void Seek(long offset, SeekOrigin origin) {
            _stream.Seek(offset, origin);
        }

        public void Close() {
            if (_stream != null) {
                _stream.Close();
                _stream.Dispose();
                _stream = null;
            }
        }
    }
}