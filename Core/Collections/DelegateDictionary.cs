//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011-2013 Garrett Serack and CoApp Contributors. 
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
    using System.Collections.Specialized;

    public class DelegateList : IList {
        public DelegateList() {
            
        }

        public IEnumerator GetEnumerator() {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index) {
            throw new NotImplementedException();
        }

        public int Count {
            get {
                throw new NotImplementedException();
            }
        }

        public object SyncRoot {
            get {
                throw new NotImplementedException();
            }
        }

        public bool IsSynchronized {
            get {
                throw new NotImplementedException();
            }
        }

        public int Add(object value) {
            throw new NotImplementedException();
        }

        public bool Contains(object value) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public int IndexOf(object value) {
            throw new NotImplementedException();
        }

        public void Insert(int index, object value) {
            throw new NotImplementedException();
        }

        public void Remove(object value) {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index) {
            throw new NotImplementedException();
        }

        public object this[int index] {
            get {
                throw new NotImplementedException();
            }
            set {
                throw new NotImplementedException();
            }
        }

        public bool IsReadOnly {
            get {
                throw new NotImplementedException();
            }
        }

        public bool IsFixedSize {
            get {
                return false;
            }
        }
    }

    public class DelegateDictionary<TKey, TVal> : AbstractDictionary<TKey, TVal> {
        private readonly Func<TKey, TVal> _get;
        private readonly Action<TKey, TVal> _set;
        private readonly Func<ICollection<TKey>> _keys;
        private readonly Func<TKey, bool> _remove;
        private readonly Action _clear;

        public DelegateDictionary(Func<ICollection<TKey>> keys, Func<TKey, TVal> get, Action<TKey, TVal> set, Func<TKey, bool> remove) {
            _keys = keys;
            _get = get;
            _set = set;
            _remove = remove;
            _clear = null;
        }

        public DelegateDictionary(Func<ICollection<TKey>> keys, Func<TKey, TVal> get, Action<TKey, TVal> set, Func<TKey, bool> remove, Action clear) {
            _keys = keys;
            _get = get;
            _set = set;
            _remove = remove;
            _clear = clear;
        }

        public override bool Remove(TKey key) {
            return _remove(key);
        }

        public override TVal this[TKey key] {
            get {
                return _get(key);
            }
            set {
                _set(key, value);
            }
        }

        public override ICollection<TKey> Keys {
            get {
                return _keys();
            }
        }

        public override void Clear() {
            if (_clear == null) {
                base.Clear();
                return;
            }
            _clear();
        }
    }

    public class DictionaryProxy<TKey, TVal> : IDictionary<TKey, TVal> {
        private readonly IDictionary<TKey, TVal> _dictionary;
        private readonly Func<TKey, TVal> _get;
        private readonly Action<TKey, TVal> _set;
        private readonly Func<ICollection<TKey>> _keys;
        private readonly Func<ICollection<TVal>> _values;
        private readonly Action<TKey, TVal> _add;
        private readonly Func<int> _count;
        private readonly Func<bool> _isReadOnly;
        private readonly Func<TKey, bool> _containsKey;
        private readonly TryGetDelegate _tryGetValue;
        private readonly Func<TKey, bool> _remove;
        private readonly Action _clear;
        private readonly Func<KeyValuePair<TKey, TVal>, bool> _removeKeyValuePair;
        private readonly Func<IEnumerator<KeyValuePair<TKey, TVal>>> _getEnumerator;
        private readonly Action<KeyValuePair<TKey, TVal>> _addKeyValuePair;
        private readonly Func<KeyValuePair<TKey, TVal>,bool> _contains;
        private readonly Action<KeyValuePair<TKey, TVal>[], int> _copyTo;
        
        public delegate bool TryGetDelegate(TKey key, out TVal value);

        public DictionaryProxy(IDictionary<TKey, TVal> dictionary, Func<TKey, TVal> get = null, Action<TKey, TVal> set = null, Func<ICollection<TKey>> keys = null, Func<ICollection<TVal>> values = null, Action<TKey, TVal> add = null, Func<int> count = null, Func<bool> isReadOnly = null, Func<TKey, bool> containsKey = null, TryGetDelegate tryGetValue = null, Func<TKey, bool> remove = null, Action clear = null, Func<KeyValuePair<TKey, TVal>, bool> removeKeyValuePair = null, Func<IEnumerator<KeyValuePair<TKey, TVal>>> getEnumerator = null, Action<KeyValuePair<TKey, TVal>> addKeyValuePair = null, Func<KeyValuePair<TKey, TVal>, bool> contains = null, Action<KeyValuePair<TKey, TVal>[], int> copyTo = null) {
            _dictionary = dictionary;
            _get = get ?? (k =>_dictionary[k]);
            _set = set ?? ((k,v) => _dictionary[k]=v);
            _keys = keys ?? (()=>_dictionary.Keys);
            _values = values ?? (() => _dictionary.Values);
            _add = add ?? _dictionary.Add;
            _count = count ?? (() => _dictionary.Count); ;
            _isReadOnly = isReadOnly ?? (() => _dictionary.IsReadOnly); ;
            _containsKey = containsKey ?? _dictionary.ContainsKey;
            _tryGetValue = tryGetValue ?? _dictionary.TryGetValue;
            _remove = remove ?? _dictionary.Remove;
            _clear = clear ?? _dictionary.Clear;
            _removeKeyValuePair = removeKeyValuePair ?? _dictionary.Remove;
            _getEnumerator = getEnumerator ?? _dictionary.GetEnumerator;
            _addKeyValuePair = addKeyValuePair ?? _dictionary.Add;
            _contains = contains ?? _dictionary.Contains;
            _copyTo = copyTo ?? _dictionary.CopyTo;
        }

        public IEnumerator<KeyValuePair<TKey, TVal>> GetEnumerator() {
            return _getEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TVal> item) {
            _addKeyValuePair(item);
        }

        public void Clear() {
            _clear();
        }

        public bool Contains(KeyValuePair<TKey, TVal> item) {
            return _contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TVal>[] array, int arrayIndex) {
            _copyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TVal> item) {
            return _removeKeyValuePair(item);
        }

        public int Count { get {
            return _count();
        } }
        public bool IsReadOnly { get {
            return _isReadOnly();
        } }
        public bool ContainsKey(TKey key) {
            return _containsKey(key);
        }

        public void Add(TKey key, TVal value) {
            _add(key, value);
        }

        public bool Remove(TKey key) {
            return _remove(key);
        }

        public bool TryGetValue(TKey key, out TVal value) {
            return _tryGetValue(key, out value);
        }

        public TVal this[TKey key] {
            get {
                return _get(key);
            }
            set {
                _set(key, value);
            }
        }

        public ICollection<TKey> Keys { get {
            return _keys();
        } }
        public ICollection<TVal> Values { get {
            return _values();
        } }
    }

    public class ReadOnlyDelegateDictionary<TKey, TVal> : DelegateDictionary<TKey, TVal> {
        public ReadOnlyDelegateDictionary(Func<ICollection<TKey>> keys, Func<TKey, TVal> get)
            : base(keys, get, (k, v) => { throw new Exception("READ ONLY COLLECTION"); }, key => { throw new Exception("READ ONLY COLLECTIOn"); }) {
        }

        public override bool IsReadOnly {
            get {
                return true;
            }
        }
        
    }

    public class OrderedDictionaryProxy<TKey, TVal> : IOrderedDictionary<TKey, TVal> {
        private readonly IOrderedDictionary<TKey, TVal> _dictionary;
        private readonly Func<TKey, TVal> _get;
        private readonly Action<TKey, TVal> _set;
        private readonly Func<ICollection<TKey>> _keys;
        private readonly Func<ICollection<TVal>> _values;
        private readonly Action<TKey, TVal> _add;
        private readonly Func<int> _count;
        private readonly Func<bool> _isReadOnly;
        private readonly Func<TKey, bool> _containsKey;
        private readonly TryGetDelegate _tryGetValue;
        private readonly Func<TKey, bool> _remove;
        private readonly Action _clear;
        private readonly Func<KeyValuePair<TKey, TVal>, bool> _removeKeyValuePair;
        private readonly Func<IEnumerator<KeyValuePair<TKey, TVal>>> _getEnumerator;
        private readonly Action<KeyValuePair<TKey, TVal>> _addKeyValuePair;
        private readonly Func<KeyValuePair<TKey, TVal>, bool> _contains;
        private readonly Action<KeyValuePair<TKey, TVal>[], int> _copyTo;

        public delegate bool TryGetDelegate(TKey key, out TVal value);

        public OrderedDictionaryProxy(IOrderedDictionary<TKey, TVal> dictionary, Func<TKey, TVal> get = null, Action<TKey, TVal> set = null, Func<ICollection<TKey>> keys = null, Func<ICollection<TVal>> values = null, Action<TKey, TVal> add = null,
            Func<int> count = null, Func<bool> isReadOnly = null, Func<TKey, bool> containsKey = null, TryGetDelegate tryGetValue = null, Func<TKey, bool> remove = null, Action clear = null, Func<KeyValuePair<TKey, TVal>, bool> removeKeyValuePair = null,
            Func<IEnumerator<KeyValuePair<TKey, TVal>>> getEnumerator = null, Action<KeyValuePair<TKey, TVal>> addKeyValuePair = null, Func<KeyValuePair<TKey, TVal>, bool> contains = null, Action<KeyValuePair<TKey, TVal>[], int> copyTo = null,
            Func<TKey, TVal, int> addReturningIndex = null,
            Action<int, object, object> insertObject = null,
            Action<int, TKey, TVal> insert = null,
            Action<int> removeAt = null,
            Action<object> removeObject = null,

            Func<int, TVal> getViaIndex = null,
            Action<int, TVal> setViaIndex = null,
            Func<object, object> getViaObject = null,
            Action<object, object> setViaObject = null,
            Action<object, object> addViaObject = null,
            Action<int, object> setObjectViaIndex = null) {

            _dictionary = dictionary;
            _get = get ?? (k => _dictionary[k]);
            _set = set ?? ((k, v) => _dictionary[k] = v);
            _keys = keys ?? (() => ((IDictionary<TKey, TVal>)_dictionary).Keys);
            _values = values ?? (() => ((IDictionary<TKey, TVal>)_dictionary).Values);
            _add = add ?? ((IDictionary<TKey, TVal>)_dictionary).Add;
            _count = count ?? (() => ((IDictionary<TKey, TVal>)_dictionary).Count);

            _isReadOnly = isReadOnly ?? (() => ((IDictionary<TKey, TVal>)_dictionary).IsReadOnly);

            _containsKey = containsKey ?? _dictionary.ContainsKey;
            _tryGetValue = tryGetValue ?? _dictionary.TryGetValue;
            _remove = remove ?? _dictionary.Remove;
            _clear = clear ?? ((IDictionary<TKey, TVal>)_dictionary).Clear;
            _removeKeyValuePair = removeKeyValuePair ?? _dictionary.Remove;
            _getEnumerator = getEnumerator ?? ((IDictionary<TKey, TVal>)_dictionary).GetEnumerator;
            _addKeyValuePair = addKeyValuePair ?? _dictionary.Add;
            _contains = contains ?? _dictionary.Contains;
            _copyTo = copyTo ?? _dictionary.CopyTo;

            _addReturningIndex = addReturningIndex ?? _dictionary.Add;
            _insertObject = insertObject ?? _dictionary.Insert;
            _insert = insert ?? _dictionary.Insert;
            _removeAt = removeAt ?? _dictionary.RemoveAt;
            _removeObject = removeObject ?? _dictionary.Remove;

            _getViaIndex = getViaIndex ?? (k => _dictionary[k]);
            _setViaIndex = setViaIndex ?? ((k, v) => _dictionary[k] = v);
            _getViaObject = getViaObject ?? (k => _dictionary[k]);
            _setViaObject = setViaObject ?? ((k, v) => _dictionary[k] = v);
            _addViaObject = addViaObject ?? _dictionary.Add;
            _setObjectViaIndex = setObjectViaIndex ?? ((k, v) => ((IDictionary)_dictionary)[k] = v);
            
        }
        private readonly Func<TKey, TVal, int> _addReturningIndex;
        private readonly Action<int, object, object> _insertObject;
        private readonly Action<int, TKey, TVal> _insert;
        private readonly Action<int> _removeAt;
        private readonly Action<object> _removeObject;

        private readonly Func<int, TVal> _getViaIndex;
        private readonly Action<int, TVal> _setViaIndex;
        private readonly Func<object, object> _getViaObject;
        private readonly Action<object, object> _setViaObject;
        private readonly Action<object, object> _addViaObject;
        private readonly Action<int, object> _setObjectViaIndex;

        public IEnumerator<KeyValuePair<TKey, TVal>> GetEnumerator() {
            return _getEnumerator();
        }

        public void Insert(int index, object key, object value) {
            _insertObject(index, key, value);
        }

        public void RemoveAt(int index) {
            _removeAt(index);
        }

        TVal IOrderedDictionary<TKey, TVal>.this[int index] {
            get {
                return _getViaIndex(index);
            }
            set {
                _setViaIndex(index, value);
            }
        }

        public void Insert(int index, TKey key, TVal value) {
            _insert(index, key, value);
        }

        object IOrderedDictionary.this[int index] {
            get {
                return _getViaIndex(index);
            }
            set {
                _setObjectViaIndex(index, value);
            }
        }

        public void Remove(object key) {
            _removeObject(key);
        }

        object IDictionary.this[object key] {
            get {
                return _getViaObject(key);
            }
            set {
                _setViaObject(key, value);
            }
        }

        public void Add(object key, object value) {
            _addViaObject(key, value);
        }

        int IOrderedDictionary<TKey, TVal>.Add(TKey key, TVal value) {
            return _addReturningIndex(key, value);
        }

        public bool Contains(object key) {
            // todo: override
            return _dictionary.Contains(key);
        }


        IDictionaryEnumerator IDictionary.GetEnumerator() {
            // todo: override
            return ((IDictionary)_dictionary).GetEnumerator();
        }

        IDictionaryEnumerator IOrderedDictionary.GetEnumerator() {
            // todo: override
            return ((IDictionary)_dictionary).GetEnumerator();
        }

        public object SyncRoot {
            get {
                // todo: override
                return _dictionary.SyncRoot;
            }
        }

        public bool IsSynchronized {
            get {
                // todo: override
                return _dictionary.IsSynchronized;
            }
        }

        

        public bool IsFixedSize {
            get {
                // todo: override
                return _dictionary.IsFixedSize;
            }
        }


        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TVal> item) {
            _addKeyValuePair(item);
        }

        public void Clear() {
            _clear();
        }

        public bool Contains(KeyValuePair<TKey, TVal> item) {
            return _contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TVal>[] array, int arrayIndex) {
            _copyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<TKey, TVal> item) {
            return _removeKeyValuePair(item);
        }

        public void CopyTo(Array array, int index) {
            CopyTo((KeyValuePair<TKey, TVal>[])array, index);
        }

        public int Count {
            get {
                return _count();
            }
        }

        ICollection IDictionary.Values {
            get {
                return (ICollection)Values;
            }
        }

        public bool IsReadOnly {
            get {
                return _isReadOnly();
            }
        }


        public bool ContainsKey(TKey key) {
            return _containsKey(key);
        }


        public void Add(TKey key, TVal value) {
            _add(key, value);
        }

        public bool Remove(TKey key) {
            return _remove(key);
        }

        public bool TryGetValue(TKey key, out TVal value) {
            return _tryGetValue(key, out value);
        }

        public TVal this[TKey key] {
            get {
                return _get(key);
            }
            set {
                _set(key, value);
            }
        }

        public ICollection<TKey> Keys {
            get {
                return _keys();
            }
        }

        ICollection IDictionary.Keys {
            get {
                return (ICollection)Keys;
            }
        }

        public ICollection<TVal> Values {
            get {
                return _values();
            }
        }
    }
}