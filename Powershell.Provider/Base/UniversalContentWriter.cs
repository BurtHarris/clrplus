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
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Management.Automation.Provider;
    using System.Text;

    public class UniversalContentWriter : IContentWriter {
        private Stream _stream;
        private Encoding _encoding;

        public UniversalContentWriter(Stream stream, Encoding encoding = null) {
            _stream = stream;
            _encoding = encoding ?? Encoding.Default;
        }

        public void Dispose() {
            Close();
        }

        public IList Write(IList content) {
            foreach (var c in content) {
                var bytes = _encoding.GetBytes(c + "\r\n");
                _stream.Write(bytes, 0, bytes.Length);
            }
            return new List<string>();
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