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

namespace ClrPlus.Platform {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using ClrPlus.Core.Extensions;

    public class EnvironmentStack {
        private static EnvironmentStack _instance;
        private readonly Stack<IDictionary> _stack = new Stack<IDictionary>();

        public static EnvironmentStack Instance {
            get {
                return _instance ?? (_instance = new EnvironmentStack());
            }
        }

        public void Push() {
            _stack.Push(Environment.GetEnvironmentVariables());
        }

        public void Pop() {
            if (_stack.Count > 0) {
                EnvironmentUtility.Apply(_stack.Pop());
            }
        }

       
    }
}