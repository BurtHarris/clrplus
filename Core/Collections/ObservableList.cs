﻿
namespace ClrPlus.Core.Collections {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Collections;

    /// <summary>
    /// List that fires events when items are changed
    /// </summary>
    /// <typeparam name="T">Type of list items</typeparam>
    public class ObservableList<T> : IList<T>, IList {
        private IList<T> internalList;

        public class ListChangedEventArgs : EventArgs {
            public int index;
            public T item;
            public ListChangedEventArgs(int index, T item) {
                this.index = index;
                this.item = item;
            }
        }

        public delegate void ItemAddedEventHandler(object source, ListChangedEventArgs e);
        public delegate void ItemRemovedEventHandler(object source, ListChangedEventArgs e);
        public delegate void ListChangedEventHandler(object source, ListChangedEventArgs e);
        public delegate void ListClearedEventHandler(object source, EventArgs e);
        /// <summary>
        /// Fired whenever list item has been changed, added or removed or when list has been cleared
        /// </summary>
        public event ListChangedEventHandler ListChanged;
        /// <summary>
        /// Fired when list item has been removed from the list
        /// </summary>
        public event ItemRemovedEventHandler ItemRemoved;
        /// <summary>
        /// Fired when item has been added to the list
        /// </summary>
        public event ItemAddedEventHandler ItemAdded;
        /// <summary>
        /// Fired when list is cleared
        /// </summary>
        public event ListClearedEventHandler ListCleared;

        public ObservableList() {
            internalList = new List<T>();
        }

        public ObservableList(IList<T> list) {
            internalList = list;
        }

        public ObservableList(IEnumerable<T> collection) {
            internalList = new List<T>(collection);
        }

        protected virtual void OnItemAdded(ListChangedEventArgs e) {
            if(ItemAdded != null)
                ItemAdded(this, e);
        }

        protected virtual void OnItemRemoved(ListChangedEventArgs e) {
            if(ItemRemoved != null)
                ItemRemoved(this, e);
        }

        protected virtual void OnListChanged(ListChangedEventArgs e) {
            if(ListChanged != null)
                ListChanged(this, e);
        }

        protected virtual void OnListCleared(EventArgs e) {
            if(ListCleared != null)
                ListCleared(this, e);
        }

        public int IndexOf(T item) {
            return internalList.IndexOf(item);
        }

        public void Insert(int index, T item) {
            internalList.Insert(index, item);
            OnListChanged(new ListChangedEventArgs(index, item));
        }

        public void Remove(object value) {
            Remove((T)value);
        }

        public void RemoveAt(int index) {
            T item = internalList[index];
            internalList.Remove(item);
            OnListChanged(new ListChangedEventArgs(index, item));
            OnItemRemoved(new ListChangedEventArgs(index, item));
        }

        object IList.this[int index] {
            get {
                return ((IList)internalList)[index];
            }
            set {
                this[index] = (T)value;
            }
        }

        public T this[int index] {
            get { return internalList[index]; }
            set {
                internalList[index] = value;
                OnListChanged(new ListChangedEventArgs(index, value));
            }
        }

        public void Add(T item) {
            internalList.Add(item);
            OnListChanged(new ListChangedEventArgs(internalList.IndexOf(item), item));
            OnItemAdded(new ListChangedEventArgs(internalList.IndexOf(item), item));
        }

        public int Add(object value) {
            var result = ((IList)internalList).Add(value);
            OnListChanged(new ListChangedEventArgs(result,(T) value));
            OnItemAdded(new ListChangedEventArgs(result, (T)value));
            return result;
        }

        public bool Contains(object value) {
            return ((IList)internalList).Contains(value);
        }

        public void Clear() {
            internalList.Clear();
            OnListCleared(new EventArgs());
        }

        public int IndexOf(object value) {
            return ((IList)internalList).IndexOf(value);
        }

        public void Insert(int index, object value) {
            Insert(index, (T)value);
        }

        public bool Contains(T item) {
            return internalList.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex) {
            internalList.CopyTo(array, arrayIndex);
        }

        public void CopyTo(Array array, int index) {
            ((IList)internalList).CopyTo(array, index);
        }

        public int Count {
            get { return internalList.Count; }
        }

        public object SyncRoot {
            get {
                return ((IList)internalList).SyncRoot;
            }
        }

        public bool IsSynchronized {
            get {
                return ((IList)internalList).IsSynchronized;
            }
        }

        public bool IsReadOnly {
            get { return IsReadOnly; }
        }

        public bool IsFixedSize {
            get {
                return ((IList)internalList).IsFixedSize;
            }
        }

        public bool Remove(T item) {
            lock(this) {
                int index = internalList.IndexOf(item);
                if(internalList.Remove(item)) {
                    OnListChanged(new ListChangedEventArgs(index, item));
                    OnItemRemoved(new ListChangedEventArgs(index, item));
                    return true;
                }
                else
                    return false;
            }
        }

        public IEnumerator<T> GetEnumerator() {
            return internalList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)internalList).GetEnumerator();
        }
    }
}
