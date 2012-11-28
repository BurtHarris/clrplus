//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors. 
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Core.Collections {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    public class EnumerableForMutatingCollection<TSrc, TDest> : IEnumerable<TDest> {
        private readonly IList<TSrc> _source;
        private bool _completed;
        private event Func<bool> _elementAdded;
        private readonly Func<TSrc, TDest> _func;

        public void ElementAdded() {
            Func<bool> handler = _elementAdded;
            if (handler != null) {
                handler();
            }
        }

        public EnumerableForMutatingCollection(IList<TSrc> source) {
            _source = source;
            _func = each => (TDest)(object)each;
        }

        public EnumerableForMutatingCollection(IList<TSrc> source, Func<TSrc, TDest> function) {
            _source = source;
            _func = function;
        }

        #region IEnumerable<T> Members

        public IEnumerator<TDest> GetEnumerator() {
            return new Enumerator<TDest>(this);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Wait(int msec = -1) {
            if (!_completed) {
                var mre = new ManualResetEvent(false);
                _elementAdded += mre.Set;

                while (!_completed) {
                    mre.WaitOne();
                }

                _elementAdded -= mre.Set;
            }
        }

        public void Completed() {
            _completed = true;
            ElementAdded();
        }

        #endregion

        #region Nested type: Enumerator

        private class Enumerator<TT> : IEnumerator<TT> {
            private EnumerableForMutatingCollection<TSrc, TT> _collection;
            private readonly ManualResetEvent _event = new ManualResetEvent(false);
            private int _index = -1;

            internal Enumerator(EnumerableForMutatingCollection<TSrc, TT> collection) {
                _collection = collection;
                _collection._elementAdded += Set;
            }

            private bool Set() {
                _event.Set();
                return true;
            }

            #region IEnumerator<Tt> Members

            public TT Current {
                get {
                    return _collection._func(_collection._source[_index]);
                }
            }

            public void Dispose() {
                _collection._elementAdded -= Set;
                _collection = null;
            }

            object IEnumerator.Current {
                get {
                    return Current;
                }
            }

            public bool MoveNext() {
                _index++;

                while (_collection._source.Count <= _index) {
                    if (_collection._completed) {
                        return false;
                    }
                    _event.Reset();
                    _event.WaitOne();
                }

                return true;
            }

            public void Reset() {
                _index = -1;
            }

            #endregion
        }

        #endregion
    }
}