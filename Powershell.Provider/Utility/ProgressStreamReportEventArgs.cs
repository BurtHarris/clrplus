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

    /// <summary>
    ///     Contains the pertinent data for a ProgressStream Report event.
    /// </summary>
    public class ProgressStreamReportEventArgs : EventArgs {
        /// <summary>
        ///     Default constructor for ProgressStreamReportEventArgs.
        /// </summary>
        public ProgressStreamReportEventArgs() {
        }

        /// <summary>
        ///     Creates a new ProgressStreamReportEventArgs initializing its members.
        /// </summary>
        /// <param name="bytesMoved"> The number of bytes that were read/written to/from the stream. </param>
        /// <param name="streamLength"> The total length of the stream in bytes. </param>
        /// <param name="streamPosition"> The current position in the stream. </param>
        /// <param name="wasRead"> True if the bytes were read from the stream, false if they were written. </param>
        public ProgressStreamReportEventArgs(int bytesMoved, long streamLength, long streamPosition, bool wasRead)
            : this() {
            BytesMoved = bytesMoved;
            StreamLength = streamLength;
            StreamPosition = streamPosition;
            WasRead = wasRead;
        }

        /// <summary>
        ///     The number of bytes that were read/written to/from the stream.
        /// </summary>
        public int BytesMoved {get; private set;}

        /// <summary>
        ///     The total length of the stream in bytes.
        /// </summary>
        public long StreamLength {get; private set;}

        /// <summary>
        ///     The current position in the stream.
        /// </summary>
        public long StreamPosition {get; private set;}

        /// <summary>
        ///     True if the bytes were read from the stream, false if they were written.
        /// </summary>
        public bool WasRead {get; private set;}
    }
}