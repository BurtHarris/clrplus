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

    public class Accessor {
        private readonly Func<object> _getter;
        private readonly Action<object> _setter;
        
        public Accessor(Func<object> getter, Action<object> setter) {
            _getter = getter;
            _setter = setter;
        }

        public object Value {
            get {
                return _getter();
            }
            set {
                _setter(value);
            }
        }
    }

    public class Accessor<T> : Accessor {
        public Accessor(Func<T> getter, Action<T> setter)
            : base(() => getter(), value => setter((T)value)) {
        }
    }

    
    public delegate object Value();
    public delegate T Value<T>();

    internal delegate object RouteDelegate<TParent>(Value<TParent> parent);
    public delegate object Route<TParent>(TParent parent);

    internal delegate Accessor ValueDelegate<TParent>(Value<TParent> parent);
    public delegate Accessor ValueRoute<TParent>(TParent parent);

    internal delegate IEnumerable EnumerableDelegate<TParent>(Value<TParent> parent);
    public delegate IEnumerable EnumerableRoute<TParent>(TParent parent);

    internal delegate IList ListDelegate<TParent>(Value<TParent> parent);
    public delegate IList ListRoute<TParent>(TParent parent);

    internal delegate IDictionary<TKey, TVal> DictionaryDelegate<TParent, TKey, TVal>(Value<TParent> parent);
    public delegate IDictionary<TKey, TVal> DictionaryRoute<TParent, TKey, TVal>(TParent parent);

    public delegate IDictionary<TKey, TVal> DictionaryRoute<TKey, TVal>();

    public delegate object IndexedChildRoute<TParent>(TParent parent, View childView);
    internal delegate object IndexedChildRouteDelegate<TParent>(Value<TParent> parent, View childView);


}