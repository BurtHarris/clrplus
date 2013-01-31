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

namespace ClrPlus.Core.Utility {
    using System;
    using System.Text;

    public static class TextEncoding {
        /// <summary>
        ///     Uses various discovery techniques to guess the encoding used for a byte buffer presumably containing text characters.
        /// </summary>
        /// <remarks>
        ///     Note that this is only a guess and could be incorrect.  Be prepared to catch exceptions while using the
        ///     <see
        ///         cref="Decoder" />
        ///     returned by
        ///     the encoding returned by this method.
        /// </remarks>
        /// <param name="bytes"> The buffer containing the bytes to examine. </param>
        /// <param name="offset"> The offset into the buffer to begin examination, or 0 if not specified. </param>
        /// <param name="length"> The number of bytes to examine. </param>
        /// <returns>
        ///     An encoding, or <see langword="null" />if one cannot be determined.
        /// </returns>
        public static Encoding GetTextEncoding(this byte[] bytes, int offset = 0, int? length = null) {
            if (bytes == null) {
                throw new ArgumentNullException("bytes");
            }
            length = length ?? bytes.Length;
            if (offset < 0 || offset > bytes.Length) {
                throw new ArgumentOutOfRangeException("offset", "Offset is out of range.");
            }
            if (length < 0 || length > bytes.Length) {
                throw new ArgumentOutOfRangeException("length", "Length is out of range.");
            } else if ((offset + length) > bytes.Length) {
                throw new ArgumentOutOfRangeException("offset", "The specified range is outside of the specified buffer.");
            }
            // Look for a byte order mark:
            if (length >= 4) {
                var one = bytes[offset];
                var two = bytes[offset + 1];
                var three = bytes[offset + 2];
                var four = bytes[offset + 3];
                if (one == 0x2B &&
                    two == 0x2F &&
                    three == 0x76 &&
                    (four == 0x38 || four == 0x39 || four == 0x2B || four == 0x2F)) {
                    return Encoding.UTF7;
                } else if (one == 0xFE && two == 0xFF && three == 0x00 && four == 0x00) {
                    return Encoding.UTF32;
                } else if (four == 0xFE && three == 0xFF && two == 0x00 && one == 0x00) {
                    throw new NotSupportedException("The byte order mark specifies UTF-32 in big endian order, which is not supported by .NET.");
                }
            } else if (length >= 3) {
                var one = bytes[offset];
                var two = bytes[offset + 1];
                var three = bytes[offset + 2];
                if (one == 0xFF && two == 0xFE) {
                    return Encoding.Unicode;
                } else if (one == 0xFE && two == 0xFF) {
                    return Encoding.BigEndianUnicode;
                } else if (one == 0xEF && two == 0xBB && three == 0xBF) {
                    return Encoding.UTF8;
                }
            }
            if (length > 1) {
                // Look for a leading < sign:
                if (bytes[offset] == 0x3C) {
                    if (bytes[offset + 1] == 0x00) {
                        return Encoding.Unicode;
                    } else {
                        return Encoding.UTF8;
                    }
                } else if (bytes[offset] == 0x00 && bytes[offset + 1] == 0x3C) {
                    return Encoding.BigEndianUnicode;
                }
            }
            if (bytes.IsUtf8()) {
                return Encoding.UTF8;
            } else {
                // Impossible to tell.
                return null;
            }
        }

        public static bool IsUtf8(this byte[] bytes, int offset = 0, int? length = null) {
            if (bytes == null) {
                throw new ArgumentNullException("bytes");
            }
            length = length ?? (bytes.Length - offset);
            if (offset < 0 || offset > bytes.Length) {
                throw new ArgumentOutOfRangeException("offset", "Offset is out of range.");
            } else if (length < 0) {
                throw new ArgumentOutOfRangeException("length");
            } else if ((offset + length) > bytes.Length) {
                throw new ArgumentOutOfRangeException("offset", "The specified range is outside of the specified buffer.");
            }
            var bytesRemaining = length.Value;
            while (bytesRemaining > 0) {
                var rank = bytes.GetUtf8MultibyteRank(offset, Math.Min(4, bytesRemaining));
                if (rank == MultibyteRank.None) {
                    return false;
                } else {
                    var charsRead = (int)rank;
                    offset += charsRead;
                    bytesRemaining -= charsRead;
                }
            }
            return true;
        }

        /// <summary>
        ///     Determines whether the bytes in this buffer at the specified offset represent a UTF-8 multi-byte character.
        /// </summary>
        /// <remarks>
        ///     It is not guaranteed that these bytes represent a sensical character - only that the binary pattern matches UTF-8 encoding.
        /// </remarks>
        /// <param name="bytes"> This buffer. </param>
        /// <param name="offset"> The position in the buffer to check. </param>
        /// <param name="length"> The number of bytes to check, of 4 if not specified. </param>
        /// <returns> The rank of the UTF </returns>
        public static MultibyteRank GetUtf8MultibyteRank(this byte[] bytes, int offset = 0, int length = 4) {
            if (bytes == null) {
                throw new ArgumentNullException("bytes");
            }
            if (offset < 0 || offset > bytes.Length) {
                throw new ArgumentOutOfRangeException("offset", "Offset is out of range.");
            } else if (length < 0 || length > 4) {
                throw new ArgumentOutOfRangeException("length", "Only values 1-4 are valid.");
            } else if ((offset + length) > bytes.Length) {
                throw new ArgumentOutOfRangeException("offset", "The specified range is outside of the specified buffer.");
            }
            // Possible 4 byte sequence
            if (length > 3 && IsLead4(bytes[offset])) {
                if (IsExtendedByte(bytes[offset + 1]) && IsExtendedByte(bytes[offset + 2]) && IsExtendedByte(bytes[offset + 3])) {
                    return MultibyteRank.Four;
                }
            }
                // Possible 3 byte sequence
            else if (length > 2 && IsLead3(bytes[offset])) {
                if (IsExtendedByte(bytes[offset + 1]) && IsExtendedByte(bytes[offset + 2])) {
                    return MultibyteRank.Three;
                }
            }
                // Possible 2 byte sequence
            else if (length > 1 && IsLead2(bytes[offset]) && IsExtendedByte(bytes[offset + 1])) {
                return MultibyteRank.Two;
            }
            if (bytes[offset] < 0x80) {
                return MultibyteRank.One;
            } else {
                return MultibyteRank.None;
            }
        }

        private static bool IsLead4(byte b) {
            return b >= 0xF0 && b < 0xF8;
        }

        private static bool IsLead3(byte b) {
            return b >= 0xE0 && b < 0xF0;
        }

        private static bool IsLead2(byte b) {
            return b >= 0xC0 && b < 0xE0;
        }

        private static bool IsExtendedByte(byte b) {
            return b > 0x80 && b < 0xC0;
        }

        private static DecoderExceptionFallback _decoderExceptionFallback = new DecoderExceptionFallback();

        public static bool Validate(this Encoding encoding, byte[] bytes, int offset = 0, int? length = null) {
            if (encoding == null) {
                throw new ArgumentNullException("encoding");
            }
            if (bytes == null) {
                throw new ArgumentNullException("bytes");
            }
            length = length ?? bytes.Length;
            if (offset < 0 || offset > bytes.Length) {
                throw new ArgumentOutOfRangeException("offset", "Offset is out of range.");
            }
            if (length < 0 || length > bytes.Length) {
                throw new ArgumentOutOfRangeException("length", "Length is out of range.");
            } else if ((offset + length) > bytes.Length) {
                throw new ArgumentOutOfRangeException("offset", "The specified range is outside of the specified buffer.");
            }
            var decoder = encoding.GetDecoder();
            decoder.Fallback = _decoderExceptionFallback;
            try {
                var charCount = decoder.GetCharCount(bytes, 0, bytes.Length);
            } catch (DecoderFallbackException) {
                return false;
            }
            return true;
        }
    }
}