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

namespace ClrPlus.CommandLine {
    using System.Collections.Generic;
    using Core.Collections;

    /// <summary>
    ///     Storage Class for complex options from the command line.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class ComplexOption {
        /// <summary>
        /// </summary>
        public string WholePrefix; // stuff in the []

        /// <summary>
        /// </summary>
        public string WholeValue; // stuff after the []

        /// <summary>
        /// </summary>
        public List<string> PrefixParameters = new List<string>(); // individual items in the []

        /// <summary>
        /// </summary>
        public IDictionary<string, string> Values = new XDictionary<string, string>(); // individual key/values after the []
    }
}