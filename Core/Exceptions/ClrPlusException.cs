//-----------------------------------------------------------------------
// <copyright company="CoApp Project" >
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Core.Exceptions {
    using System;
    using System.Diagnostics;
    using System.Runtime.Serialization;

    public class ClrPlusException : Exception {
        public bool Logged;

        public string StackTraceString;

        public bool IsCanceled {get; set;}

        public void Cancel() {
            IsCanceled = true;
        }

        private void Log() {
            StackTraceString = new StackTrace(2, true).ToString();
#if TODO
    // CoApp should support logging in *it's* base class that inherits from this one.
            Logger.Error(this);
#endif
        }

        public ClrPlusException(bool skipLogging = false) {
            if (!skipLogging) {
                Log();
            }
            Logged = true;
        }

        public ClrPlusException(string message, bool skipLogging = false)
            : base(message) {
            if (!skipLogging) {
                Log();
            }
            Logged = true;
        }

        public ClrPlusException(String message, Exception innerException, bool skipLogging = false)
            : base(message, innerException) {
            if (!skipLogging) {
                Log();
            }
            Logged = true;
        }

        protected ClrPlusException(SerializationInfo info, StreamingContext context, bool skipLogging = false)
            : base(info, context) {
            if (!skipLogging) {
                Log();
            }
            Logged = true;
        }
    }
}