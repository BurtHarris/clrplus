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

namespace ClrPlus.Core.Collections {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using Exceptions;

    public class AsynchronouslyEnumerableList<TElement> : IList<TElement> {
        private readonly IList<TElement> _actualCollection;
        private event Func<bool> CollectionModified;
        public bool IsCompleted { get; private set;}

        public AsynchronouslyEnumerableList() : this(new List<TElement>() ) {
        }

        public AsynchronouslyEnumerableList(IList<TElement> list) {
            _actualCollection = list;
        }

        public IEnumerator<TElement> GetEnumerator() {
            return new AsnycEnumerator<TElement>(this);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Completed() {
            if (!IsCompleted) {
                IsCompleted = true;
                Modified();
            }
        }

        private void Modified() {
            if(CollectionModified  != null) {
                CollectionModified();
            }
        }

        private void DangerouslyModified() {
            // what happens if the collection is modified in a dangerous way?
        }

        public void Wait(int msec = -1) {
            if(!IsCompleted) {
                var mre = new ManualResetEvent(false);
                CollectionModified += mre.Set;

                while(!IsCompleted) {
                    mre.WaitOne();
                }

                CollectionModified -= mre.Set;
            }
        }

        public void Add(TElement item) {
            _actualCollection.Add(item);
            Modified();
        }

        public void Clear() {
            _actualCollection.Clear();
            DangerouslyModified();
        }

        public bool Contains(TElement item) {
            return _actualCollection.Contains(item);
        }

        public void CopyTo(TElement[] array, int arrayIndex) {
            Wait();
            _actualCollection.CopyTo(array, arrayIndex);
        }

        public bool Remove(TElement item) {
            var result = _actualCollection.Remove(item);
            DangerouslyModified();
            return result;
        }

        public int Count {get {
            Wait();
            return _actualCollection.Count;
        }}
        public bool IsReadOnly { get {
            return _actualCollection.IsReadOnly;
        } }
        public int IndexOf(TElement item) {
            return _actualCollection.IndexOf(item);
        }

        public void Insert(int index, TElement item) {
            _actualCollection.Insert(index, item);
            DangerouslyModified();
        }

        public void RemoveAt(int index) {
            _actualCollection.RemoveAt(index);
            DangerouslyModified();
        }

        public TElement this[int index] {
            get {
                return _actualCollection[index];
            }
            set {
                _actualCollection[index] = value;
                DangerouslyModified();
            }
        }

        internal class AsnycEnumerator<TElem> : IEnumerator<TElem> {
            private AsynchronouslyEnumerableList<TElem> _collection;
            private readonly ManualResetEvent _event = new ManualResetEvent(false);
            private int _index = -1;

            internal AsnycEnumerator(AsynchronouslyEnumerableList<TElem> collection) {
                _collection = collection;
                _collection.CollectionModified += Set;
            }

            private bool Set() {
                _event.Set();
                return true;
            }

            public void Dispose() {
                _collection.CollectionModified -= Set;
                _collection = null;
            }

            public bool MoveNext() {
                _index++;

                while(_collection.Count <= _index) {
                    if(_collection.IsCompleted) {
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

            public TElem Current { get { return _collection[_index]; } }

            object IEnumerator.Current {
                get {
                    return Current;
                }
            }
        }

        public void AddRange(IEnumerable<TElement> sourceCollection) {
            foreach (var i in sourceCollection) {
                Add(i);
            }
        }
    }


#if false
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
#endif 
}