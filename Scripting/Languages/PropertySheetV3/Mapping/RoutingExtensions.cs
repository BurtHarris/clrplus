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

namespace ClrPlus.Scripting.Languages.PropertySheetV3.Mapping {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public delegate View ToRoute();

    public static class RoutingExtensions {

        internal static ToRoute MapToObject<TParent>(this string memberName, RouteDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes = null, IEnumerable<ToRoute> fallbackRoutes = null) where TParent : class {
            return () => new View<TParent>(memberName, route, childRoutes);
        }

        public static ToRoute MapTo<TParent>(this string memberName, RouteWithView<TParent> route, IEnumerable<ToRoute> childRoutes = null) where TParent : class {
            return MapToObject<TParent>(memberName, (p,v) => route(p(),v), childRoutes);
        }

        public static ToRoute MapTo<TParent>(this string memberName, Route<TParent> route, IEnumerable<ToRoute> childRoutes = null) where TParent : class {
            return MapToObject<TParent>(memberName, (p,v) => route(p()), childRoutes);
        }

        public static ToRoute MapTo(this string memberName, Func<object> obj, IEnumerable<ToRoute> childRoutes = null) {
            return MapToObject<object>(memberName, (p, v) => obj(), childRoutes);
        }

        public static ToRoute MapTo(this string memberName, Func<View,object> obj, IEnumerable<ToRoute> childRoutes = null) {
            return MapToObject<object>(memberName, (p, v) => obj(v), childRoutes);
        }

        public static ToRoute MapTo(this string memberName, object obj, IEnumerable<ToRoute> childRoutes = null) {
            return MapToObject<object>(memberName, (p, v) => obj, childRoutes);
        }


        internal static ToRoute MapToValue<TParent>(this string memberName, ValueDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return () => new View<TParent>(memberName, route, childRoutes);
        }
        public static ToRoute MapTo<TParent>(this string memberName, ValueRouteWithView<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToValue<TParent>(memberName, (p, v) => route(p(),v), childRoutes);
        }
        public static ToRoute MapTo<TParent>(this string memberName, ValueRoute<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToValue<TParent>(memberName, (p, v) => route(p()), childRoutes);
        }
        public static ToRoute MapTo(this string memberName, Func<View,Accessor> accessorfn, IEnumerable<ToRoute> childRoutes = null) {
            return MapToValue<object>(memberName, (p, v) => accessorfn(v), childRoutes);
        }

        public static ToRoute MapTo(this string memberName, Accessor accessor, IEnumerable<ToRoute> childRoutes = null) {
            return MapToValue<object>(memberName, (p, v) => accessor, childRoutes);
        }
        public static ToRoute MapTo(this string memberName, Func<object> getter, Action<object> setter, IEnumerable<ToRoute> childRoutes = null) {
            return MapTo(memberName, new Accessor(getter, setter), childRoutes);
        }

        internal static ToRoute MapToEnumerable<TParent>(this string memberName, EnumerableDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return () => new View<TParent>(memberName, route, childRoutes);
        }
        public static ToRoute MapTo<TParent>(this string selector, EnumerableRouteWithView<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToEnumerable<TParent>(selector, (p,v) => route(p(),v), childRoutes);
        }
        public static ToRoute MapTo<TParent>(this string selector, EnumerableRoute<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToEnumerable<TParent>(selector, (p,v) => route(p()), childRoutes);
        }
        public static ToRoute MapTo(this string selector, Func<View,IEnumerable> enumerable, IEnumerable<ToRoute> childRoutes = null) {
            return MapToEnumerable<object>(selector, (p, v) => enumerable(v), childRoutes);
        }
        public static ToRoute MapTo(this string selector, Func<IEnumerable> enumerable, IEnumerable<ToRoute> childRoutes = null) {
            return MapToEnumerable<object>(selector, (p, v) => enumerable(), childRoutes);
        }
        public static ToRoute MapTo(this Selector selector, IEnumerable enumerable, IEnumerable<ToRoute> childRoutes = null) {
            return MapToEnumerable<object>(selector, (p,v) => enumerable, childRoutes);
        }

        internal static ToRoute MapToList<TParent>(this string memberName, ListDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return () => new View<TParent>(memberName, route, childRoutes);
        }
        public static ToRoute MapTo<TParent>(this string memberName, ListRouteWithView<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToList<TParent>(memberName, (p,v) => route(p(),v), childRoutes);
        }
        public static ToRoute MapTo<TParent>(this string memberName, ListRoute<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToList<TParent>(memberName, (p, v) => route(p()), childRoutes);
        }
        public static ToRoute MapTo(this string memberName, Func<View,IList> list, IEnumerable<ToRoute> childRoutes = null) {
            return MapToList<object>(memberName, (p, v) => list(v), childRoutes);
        }
        public static ToRoute MapTo(this string memberName, Func<IList> list, IEnumerable<ToRoute> childRoutes = null) {
            return MapToList<object>(memberName, (p, v) => list(), childRoutes);
        }
        public static ToRoute MapTo(this string memberName, IList list, IEnumerable<ToRoute> childRoutes = null) {
            return MapToList<object>(memberName, (p, v) => list, childRoutes);
        }

        internal static ToRoute MapToDictionary<TParent, TKey, TVal>(this string memberName, DictionaryDelegateWithView<TParent, TKey, TVal> route, IEnumerable<ToRoute> childRoutes = null) {
            return () => new View<TParent,TKey, TVal>(memberName, route, childRoutes);
        }

        public static ToRoute MapTo<TParent, TKey, TVal>(this string memberName, DictionaryRouteWithView<TParent, TKey, TVal> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<TParent, TKey, TVal>(memberName, (p,v) => route(p(),v), childRoutes);
        }
        public static ToRoute MapTo<TParent, TKey, TVal>(this string memberName, DictionaryRoute<TParent, TKey, TVal> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<TParent, TKey, TVal>(memberName, (p,v) => route(p()), childRoutes);
        }
        public static ToRoute MapTo<TKey, TVal>(this string memberName, IDictionary<TKey, TVal> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<object, TKey, TVal>(memberName, (p, v) => route, childRoutes);
        }
        public static ToRoute MapTo<TParent, TKey, TVal>(this string memberName, IDictionary<TKey, TVal> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<TParent, TKey, TVal>(memberName, (p, v) => route, childRoutes);
        }

        internal static ToRoute MapToDictionary<TParent, TKey, TVal>(this string memberName, DictionaryDelegateWithView<TParent, TKey, TVal> route, Func<string, string> keyExchanger, IEnumerable<ToRoute> childRoutes = null) {
            return () => new View<TParent, TKey, TVal>(memberName, route, keyExchanger, childRoutes);
        }

        public static ToRoute MapTo<TParent, TKey, TVal>(this string memberName, DictionaryRouteWithView<TParent, TKey, TVal> route, Func<string, string> keyExchanger, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<TParent, TKey, TVal>(memberName, (p, v) => route(p(),v), keyExchanger, childRoutes);
        }

        public static ToRoute MapTo<TParent, TKey, TVal>(this string memberName, DictionaryRoute<TParent, TKey, TVal> route, Func<string, string> keyExchanger, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<TParent, TKey, TVal>(memberName, (p, v) => route(p()), keyExchanger, childRoutes);
        }

        public static ToRoute MapTo<TKey, TVal>(this string memberName, DictionaryRouteWithView<TKey, TVal> route, Func<string, string> keyExchanger, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<object, TKey, TVal>(memberName, (p, v) => route(v), keyExchanger, childRoutes);
        }
        public static ToRoute MapTo<TKey, TVal>(this string memberName, DictionaryRoute<TKey, TVal> route, Func<string, string> keyExchanger, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<object, TKey, TVal>(memberName, (p, v) => route(), keyExchanger, childRoutes);
        }

        public static ToRoute MapTo<TKey, TVal>(this string memberName, IDictionary<TKey, TVal> route, Func<string, string> keyExchanger, IEnumerable<ToRoute> childRoutes = null) {
            return MapToDictionary<object, TKey, TVal>(memberName, (p, v) => route, keyExchanger, childRoutes);
        }

        internal static ToRoute MapIndexedChildrenTo<TParent>(this string memberName, IndexedChildRouteDelegate<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            // set up a virtual view as a child of the view.
            // that virutal view can then expose the #'d grandchildren as children.
            return () =>  new View<TParent>(memberName, route, childRoutes);
        }

        public static ToRoute MapIndexedChildrenTo<TParent>(this string memberName, IndexedChildRoute<TParent> route, IEnumerable<ToRoute> childRoutes = null) {
            return MapIndexedChildrenTo<TParent>(memberName, (p, v) => route(p(), v), childRoutes);
        }
    }
}

#if OLD
 
         public static ToRoute __MapTo<TParent>(this string memberName, Route<TParent> route, params ToRoute[] childRoutes) where TParent : class {
            return MapToObject<TParent>(memberName, p => route(p()), childRoutes);
        }

        // map that doesn't actually require the parent value to compute.
        public static ToRoute __MapTo(this string memberName, Func<object> obj, params ToRoute[] childRoutes) {
            return MapToObject<object>(memberName, p => obj(), childRoutes);
        }
        public static ToRoute __MapTo(this string memberName, object obj, params ToRoute[] childRoutes) {
            return MapToObject<object>(memberName, p => obj, childRoutes);
        }

        public static ToRoute __MapTo<TParent>(this string memberName, ValueRoute<TParent> route, params ToRoute[] childRoutes) {
            return MapToValue<TParent>(memberName, p => route(p()), childRoutes);
        }
        public static ToRoute __MapTo(this string memberName, Accessor accessor, params ToRoute[] childRoutes) {
            return MapToValue<object>(memberName, (p) => accessor, childRoutes);
        }
        public static ToRoute __MapTo(this string memberName, Func<object> getter, Action<object> setter, params ToRoute[] childRoutes) {
            return MapTo(memberName, new Accessor(getter, setter), childRoutes);
        }

         public static ToRoute __MapTo<TParent>(this string selector, EnumerableRoute<TParent> route, params ToRoute[] childRoutes) {
            return MapToEnumerable<TParent>(selector, (p) => route(p()), childRoutes);
        }
        public static ToRoute __MapTo(this string selector, Func<IEnumerable> enumerable, params ToRoute[] childRoutes) {
            return MapToEnumerable<object>(selector, (p) => enumerable(), childRoutes);
        }

        public static ToRoute __MapTo(this Selector selector, IEnumerable enumerable, params ToRoute[] childRoutes) {
            return MapToEnumerable<object>(selector, (p) => enumerable, childRoutes);
        }
        public static ToRoute __MapTo<TParent>(this string memberName, ListRoute<TParent> route, params ToRoute[] childRoutes) {
            return MapToList<TParent>(memberName, p => route(p()), childRoutes);
        }
        public static ToRoute __MapTo(this string memberName, Func<IList> list, params ToRoute[] childRoutes) {
            return MapToList<object>(memberName, p => list(), childRoutes);
        }
        public static ToRoute __MapTo(this string memberName, IList list, params ToRoute[] childRoutes) {
            return MapToList<object>(memberName, p => list, childRoutes);
        }
        public static ToRoute __MapTo<TParent, TKey, TVal>(this string memberName, DictionaryRoute<TParent, TKey, TVal> route, params ToRoute[] childRoutes) {
            return MapToDictionary<TParent, TKey, TVal>(memberName, (p) => route(p()), childRoutes);
        }

        public static ToRoute __MapTo<TKey, TVal>(this string memberName, IDictionary<TKey, TVal> route, params ToRoute[] childRoutes) {
            return MapToDictionary<object,TKey,TVal>( memberName, (p) => route, childRoutes);
        }

        public static ToRoute __MapTo<TParent, TKey, TVal>(this string memberName, IDictionary<TKey, TVal> route, params ToRoute[] childRoutes) {
            return MapToDictionary<TParent, TKey, TVal>(memberName, (p) => route, childRoutes);
        }

         public static ToRoute __MapTo<TParent, TKey, TVal>(this string memberName, DictionaryRoute<TParent, TKey, TVal> route, Func<string, string> keyExchanger, params ToRoute[] childRoutes) {
            return MapToDictionary<TParent, TKey, TVal>(memberName, (p) => route(p()), keyExchanger, childRoutes);
        }

        public static ToRoute __MapTo<TKey, TVal>(this string memberName, DictionaryRoute<TKey, TVal> route, Func<string, string> keyExchanger, params ToRoute[] childRoutes) {
            return MapToDictionary<object, TKey, TVal>(memberName, (p) => route(), keyExchanger, childRoutes);
        }
        public static ToRoute __MapTo<TKey, TVal>(this string memberName, IDictionary<TKey, TVal> route, Func<string, string> keyExchanger, params ToRoute[] childRoutes) {
            return MapToDictionary<object, TKey, TVal>(memberName, (p) => route, keyExchanger, childRoutes);
        }

        public static ToRoute __MapIndexedChildrenTo<TParent>(this string memberName, IndexedChildRoute<TParent> route, params ToRoute[] childRoutes) {
            return MapIndexedChildrenTo<TParent>(memberName, (p, v) => route(p(), v), childRoutes);
        }

#endif