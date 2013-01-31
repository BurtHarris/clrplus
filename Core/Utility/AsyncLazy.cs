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
    using System.Threading.Tasks;

    /// <summary>
    ///     Provides support for lazy initialization in asyncronous manner.
    /// </summary>
    /// <typeparam name="T"> Specifies the type of object that is being lazily initialized. </typeparam>
    public sealed class AsyncLazy<T> {
        private readonly object _syncObject = new object();
        private readonly Task<T> _initializeTask;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AsyncLazy" /> class.
        ///     When lazy initialization occurs, the specified initialization function is used.
        /// </summary>
        /// <param name="valueFactory"> The delegate that is invoked to produce the lazily initialized value when it is needed. </param>
        public AsyncLazy(Func<T> valueFactory) {
            if (valueFactory == null) {
                throw new ArgumentNullException("valueFactory");
            }
            _initializeTask = new Task<T>(valueFactory);
            _initializeTask.Start();
        }

        /// <summary>
        ///     Gets the lazily initialized value of the current instance.
        ///     If value have not been initialized, block calling thread until value get initialized.
        ///     If during initialization exception have been thrown, it will wrapped into <see cref="AggregateException" />
        ///     and rethrowned on accessing this property.
        /// </summary>
        public T Value {
            get {
                if (_initializeTask.Status == TaskStatus.Created) {
                    lock (_syncObject) {
                        if (_initializeTask.Status == TaskStatus.Created) {
                            _initializeTask.RunSynchronously();
                        }
                    }
                }

                return _initializeTask.Result;
            }
        }

        /// <summary>
        ///     Gets a value that indicates whether a value has been created for this instance.
        /// </summary>
        /// <value>
        ///     <c>true</c> if a value has been created; otherwise, <c>false</c> .
        /// </value>
        public bool IsValueCreated {
            get {
                
                return _initializeTask.IsCompleted;
            }
        }

        /// <summary>
        ///     Initializes value on background thread.
        ///     Calling thread will never be blocked.
        /// </summary>
        public void InitializeAsync() {
            if (_initializeTask.Status == TaskStatus.Created) {
                lock (_syncObject) {
                    if (_initializeTask.Status == TaskStatus.Created) {
                        _initializeTask.Start();
                    }
                }
            }
        }
    }
}