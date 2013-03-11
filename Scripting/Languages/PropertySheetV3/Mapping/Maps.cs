namespace ClrPlus.Scripting.Languages.PropertySheetV3.Mapping {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;

    public interface IHasSingleValue {
        string GetSingleValue();
    }

    internal interface ICanSetBackingValue {
        void Reset();
        void AddValue(string value);
        void SetValue(string value);
    }

    internal interface ICanSetBackingValues {
        void Reset();
        void AddValue(string value);
    }

    public partial class View {

        protected interface IElements {
            IDictionary<string, View> ElementDictionary {
                get;
            }
        }

        protected interface IHasValueFromBackingStorage {
            string Value {
                get;
            }
            IEnumerable<string> Values {
                get;
            }
        }

        protected interface IReplaceable {
        }

        protected interface IPrefersSingleValue {
        };

        protected interface IPrefersMultipleValues {
        };

        protected interface IPrefersComputedValue {
        }

        protected class Map : AbstractDictionary<string, View> {
            internal View ParentView;
            protected IDictionary<string, View> _childItems;
            protected internal Queue<ToRoute> Initializers;
            internal StringExtensions.GetMacroValueDelegate GetMacroValue;
            private string _memberName;

            internal string MemberName {
                get {
                    return _memberName ;
                }
                set {
                    _memberName = value;
                }
            }

            internal string Identity {
                get {
                    if(string.IsNullOrEmpty(_memberName)) {
                        if(this is ElementMap) {
                            return "parameter:"+ (this as ElementMap).Parameter;
                        }
                        return this.GetType().Name + "-???";
                    }
                    return _memberName;
                }
            }

            protected Value _parentReferenceValue;

            protected Map(string memberName, IEnumerable<ToRoute> childRoutes) {
                _parentReferenceValue = () => {
                  Console.WriteLine("Accessing unset value--is this a parent object?  {0}", this.MemberName);
                  return null;
                };

                MemberName = memberName;

                if (childRoutes != null) {
                    Initializers = new Queue<ToRoute>(childRoutes);
                }
            }

            #region DictionaryImplementation

            protected virtual IDictionary<string, View> ChildItems {
                get {
                    return _childItems ?? (_childItems = new XDictionary<string, View>());
                }
            }

            public override View this[string key] {
                get {
                    if (_childItems == null) {
                        throw new ClrPlusException("Element '{0}' does not exist in map".format(key));
                    }
                    return _childItems[key];
                }
                set {
                    value.map._parentReferenceValue = () => ComputedValue;

                    ChildItems[key] = value;
                }
            }

            public override ICollection<View> Values {
                get {
                    if (_childItems == null) {
                        return new View[0];
                    }
                    return ChildItems.Values;
                }
            }

            public override ICollection<string> Keys {
                get {
                    if (_childItems == null) {
                        return new string[0];
                    }
                    return ChildItems.Keys;
                }
            }

            public override bool Remove(string key) {
                if (_childItems == null) {
                    return false;
                }
                return _childItems.Remove(key);
            }

            public override void Add(string key, View value) {
                value.map._parentReferenceValue = () => ComputedValue;
                ChildItems.Add(key, value);
            }

            public override void Clear() {
                if (_childItems == null) {
                    return;
                }
                ChildItems.Clear();
            }

            #endregion

            protected internal virtual object ComputedValue {
                get {
//                    Console.WriteLine("NO VALUE: {0}", GetType().Name);
                    return null;
                }
            }

            internal virtual void CopyToModel() {
                if (_childItems != null) {
                    foreach (var i in _childItems.Values) {
                        i.CopyToModel();
                    }
                }
            }

            internal Map RootMap {
                 get {
                     return ParentView == null ? this : ParentView._map.RootMap;
                 }
            }

            internal virtual Map OnAccess(View thisView) {
                if (Initializers != null) {
                    lock (this) {
                        while (Initializers.Count > 0) {
                            
                            var childView = Initializers.Dequeue()();
                            if (childView != null) {

                                if (childView._map is ElementMap) {
                                    MergeElement(childView);
                                    return this;
                                }

                                var resolvedName = thisView.ResolveAlias(childView._map.MemberName);
                                if (resolvedName.StartsWith("::")) {
                                    RootMap.AddChild(() => Unroll(resolvedName.Substring(2), childView));
                                } else {
                                    MergeChild(thisView, Unroll(resolvedName, childView));                                    
                                }
                            }
                        }
                    }
                }
                return this;
            }

            internal virtual Map AddChild(ToRoute route) {
                if (route != null) {
                    lock (this) {
                        if (Initializers == null) {
                            Initializers = new Queue<ToRoute>();
                        }
                        Initializers.Enqueue(route);
                    }
                }
                return this;
            }

            internal virtual Map AddChildren(IEnumerable<ToRoute> routes) {
                if (routes != null) {
                    lock (this) {
                        if (Initializers == null) {
                            Initializers = new Queue<ToRoute>();
                        }
                        foreach (var i in routes) {
                            Initializers.Enqueue(i);
                        }
                    }
                }
                return this;
            }

            protected internal virtual void MergeElement( View childView) {
            }

            protected virtual void CopyElementsTo(Map childMap) {
            }

            protected virtual void MergeChild(View thisView, View childView) {
                if (childView._map is ElementMap) {
                    MergeElement(childView);
                    return;
                }

                var name = childView._map.MemberName;

                // ensure this child's parent is set correctly.
                childView.ParentView = thisView;

                if (!ChildItems.Keys.Contains(name)) {
                    // we're first -- add it to the view, and get out.
                    // childView.ParentReference = () => ReferenceValue;
                    Add(name, childView);
                    return;
                }

                // modifying this view
                var currentView = ChildItems[name];

                // first, copy over any property that is there.
                if (childView.HasProperty) {
                    currentView.AggregatePropertyNode.InsertRange(0, childView.AggregatePropertyNode);
                }

                // copy aliases
                if (childView._aliases.IsValueCreated) {
                    currentView._aliases.Value.AddRange(childView._aliases.Value);
                }

                // copy node metadata
                if (childView._metadata.IsValueCreated) {
                    currentView._metadata.Value.AddRange(childView._metadata.Value);
                }

                // if the child view map is replaceable, then simply steal it's child routes 
                if (childView._map is IReplaceable) {
                    currentView._map.AddChildren(childView._map.Initializers);
                    currentView._map.GetMacroValue += childView._map.GetMacroValue;
                    return;
                }

                // if this current view map is replaceable, then let's go the other way.
                if (currentView._map is IReplaceable) {
                    currentView._map.AddChildren(childView._map.Initializers);
                    childView._map.Initializers = currentView._map.Initializers;
                    childView._map._parentReferenceValue = currentView._map._parentReferenceValue;
                    childView._map.GetMacroValue += currentView._map.GetMacroValue;

                    // and move any existing children over to the new map.
                    foreach (var key in currentView._map.Keys) {
                        childView._map.Add(key, currentView._map[key]);
                    }


                    // handle any Elements
                    currentView._map.CopyElementsTo(childView._map);

                    // set the new map as ours.
                    currentView._map = childView._map;
                    return;
                }

                // if neither is replaceable, then we're in a kind of pickle.
                throw new ClrPlusException("Neither map is replaceable");
            }
        }

        protected class DictionaryMap<TParent, TKey, TVal> : Map, IElements, IHasValueFromBackingStorage  {
            private DictionaryDelegate<TParent, TKey, TVal> _route;
            private List<ToRoute> _childInitializers = new List<ToRoute>();

            private IDictionary<TKey, TVal> Dictionary { get {
                return _route(() => (TParent)_parentReferenceValue());
            }}

            internal DictionaryMap(string memberName, DictionaryDelegate<TParent, TKey, TVal> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                    
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _parentReferenceValue;
                }
            }

            public override ICollection<string> Keys {
                get {
                    return ChildItems.Keys.Union((from object i in Dictionary.Keys where i != null select i.ToString())).ToArray();
                }
            }

            internal override Map OnAccess(View thisView) {
                // each of the children that already exist (either from the property sheet or the backing collection)
                // must have routes created for each of the routes in this container.

                // and then we hold onto the routes in case there are more elements added after this. ?


                if(Initializers != null) {
                    lock(this) {
                        foreach (var key in Keys) {
                            var childItem = this[key];
                            foreach (var i in Initializers) {
                                if (!childItem._map.Initializers.Contains(i)) {
                                    childItem._map.Initializers.Enqueue(i);
                                }
                            }
                        }

                        _childInitializers.AddRange(Initializers);
                        Initializers.Clear();
                    }
                }
                return this;
            }

            public override void Add(string key, View value) {
                // we should see if this ever gets called on a node that hasn't been initialized by OnAccess...

                base.Add(key, value);
            }

         
            public override View this[string key] {
                get {
                    // if the view we have 
                    if (!ChildItems.ContainsKey(key)) {
                        var _key = (TKey)(object)key;

                        if (Dictionary.ContainsKey(_key)) {
                            var accessor = new Accessor(() => Dictionary[_key], v => Dictionary[_key] = (TVal)v);
                            var childMap = new ValueMap<object>(key, (p) => accessor, null);

                            childMap.GetMacroValue += name => name == "__ELEMENT_ID__" || name == MemberName ? key : null;
                            MergeChild(ParentView, new View(childMap));
                        }
                    }
                    return ChildItems[key];
                }
                set {
                    base[key] = value;
                }
            }

            public IDictionary<string, View> ElementDictionary {
                get {
                    return this;
                }
            }

            string IHasValueFromBackingStorage.Value {
                get {
                    var result = Dictionary;
                    if (result is IHasSingleValue) {
                        return (result as IHasSingleValue).GetSingleValue();
                    }

                    return (from object i in result.Values where i != null select i).Aggregate("", (current, each) => current + ", " + each.ToString()).Trim(',', ' ');
                }
            }

            IEnumerable<string> IHasValueFromBackingStorage.Values {
                get {
                    var result = Dictionary;
                    if (result == null) {
                        yield break;
                    }
                    foreach (var i in from object i in result.Values where i != null select i) {
                        yield return i.ToString();
                    }
                }
            }

            protected internal override void MergeElement(View childView) {
                // dictionaries children  == elements.
                var item = childView._map as ElementMap;
                if (item != null) {
                    // translate elements into child nodes here.
                    var _key = (TKey)(object)item.Parameter;
                    var accessor = new Accessor(() => Dictionary[_key], v => Dictionary[_key] = (TVal)v);

                    var childMap = new ValueMap<object>(item.Parameter, (p) => accessor, item.Initializers);
                    

                    childMap.GetMacroValue += name => name == "__ELEMENT_ID__" || name == MemberName ? item.Parameter : null;

                    MergeChild(ParentView, new View(childMap) {
                        _propertyNode = childView._propertyNode
                    });

                } else {
                    throw new ClrPlusException("map really should be an elementmap here...");
                }
            }
        }

        protected class ElementMap : Map, IReplaceable {
            protected internal string Parameter;

            internal ElementMap(string memberName, string parameter, INode node)
                : base(memberName, (node is ObjectNode) ? (node as ObjectNode).Routes : null) {
                Parameter = parameter;
            }
        }

        protected class EnumerableMap<TParent> : Map, IHasValueFromBackingStorage, IPrefersMultipleValues {
            private EnumerableDelegate<TParent> _route;

            internal EnumerableMap(string memberName, EnumerableDelegate<TParent> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _route(() => (TParent)_parentReferenceValue());
                }
            }

            string IHasValueFromBackingStorage.Value {
                get {
                    var result = (IEnumerable)ComputedValue;
                    if (result != null) {
                        return result.Cast<object>().Aggregate("", (current, i) => current + "," + result);
                    }
                    return string.Empty;
                }
            }

            IEnumerable<string> IHasValueFromBackingStorage.Values {
                get {
                    var result = (IEnumerable)ComputedValue;
                    if (result != null) {
                        return result.Cast<object>().Select(each => each.ToString());
                    }
                    return Enumerable.Empty<string>();
                }
            }
        }

        protected class ListMap<TParent> : Map, ICanSetBackingValues, IHasValueFromBackingStorage, IPrefersMultipleValues {
            private ListDelegate<TParent> _route;

            internal ListMap(string memberName, ListDelegate<TParent> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _route(() => (TParent)_parentReferenceValue());
                }
            }

            public void Reset() {
                ((IList)ComputedValue).Clear();
            }

            public void AddValue(string value) {
                ((IList)ComputedValue).Add(value);
            }

            string IHasValueFromBackingStorage.Value {
                get {
                    var result = ((IList)ComputedValue);
                    if (result != null) {
                        return result.Cast<object>().Aggregate("", (current, i) => current + "," + result);
                    }
                    return string.Empty;
                }
            }

            IEnumerable<string> IHasValueFromBackingStorage.Values {
                get {
                    var result = ((IList)ComputedValue);
                    if (result != null) {
                        return result.Cast<object>().Select(each => each.ToString());
                    }
                    return Enumerable.Empty<string>();
                }
            }
        }

        protected class PlaceholderMap : Map, IReplaceable {
            private List<View> _elements;

            protected internal override void MergeElement(View childView) {
                // store it until something comes looking for this.
                (_elements ?? (_elements = new List<View>())).Add(childView);
            }

            protected override void CopyElementsTo(Map childMap) {
                if(_elements.IsNullOrEmpty()) {
                    return;
                }

                foreach(var i in _elements) {
                    childMap.MergeElement(i);
                }
            }

            internal PlaceholderMap(string memberName, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
            }
        }

        protected class NodeMap : PlaceholderMap, IReplaceable {
            internal NodeMap(string memberName, INode node)
                : base(memberName, node is ObjectNode ? (node as ObjectNode).Routes : null) {
            }
        }

        protected class ObjectMap<TParent> : Map, IHasValueFromBackingStorage, IPrefersComputedValue  {
            private RouteDelegate<TParent> _route;

            internal ObjectMap(string memberName, RouteDelegate<TParent> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {

                // when this map is activated, add our children to it.
                AddChild(() => {
                    AddChildren(MemberRoutes);
                    return null;
                });
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _route(  () => (TParent)_parentReferenceValue());
                }
            }

            private IEnumerable<ToRoute> MemberRoutes {
                get {
                    var result = ComputedValue;

                    if (result != null) {
                        var type = result.GetType();

                        foreach (var each in type.GetPersistableElements()) {
                            var ppi = each;

                            switch (ppi.ActualType.GetPersistableInfo().PersistableCategory) {
                                case PersistableCategory.String:
                                    yield return (ppi.Name.MapTo(new Accessor(() => ppi.GetValue(result, null), v => ppi.SetValue(result, v, null))));
                                    break;

                                case PersistableCategory.Nullable:
                                case PersistableCategory.Enumeration:
                                case PersistableCategory.Parseable:
                                    // parsable types should probably be returned as a Property.

                                    yield return (ppi.Name.MapTo(new Accessor(() => ppi.GetValue(result, null), v => {
                                        // if the value is null, try to set null..
                                        if (v == null) {
                                            ppi.SetValue(result, null, null);
                                            return;
                                        }

                                        // if the types are compatible, assign directly
                                        if (ppi.ActualType.IsInstanceOfType(v)) {
                                            ppi.SetValue(result, v, null);
                                            return;
                                        }

                                        // try to parse it from string.
                                        ppi.SetValue(result, ppi.ActualType.ParseString(v.ToString()), null);
                                    })));
                                    break;

                                case PersistableCategory.Array:
                                    string s = ppi.Name;
                                    yield return (ppi.Name.MapTo(() => (IEnumerable)ppi.GetValue(result, null)));
                                    break;

                                case PersistableCategory.Enumerable:
                                    if (typeof (IList).IsAssignableFrom(ppi.ActualType)) {
                                        // it's actually an IList
                                        yield return (ppi.Name.MapTo(() => (IList)ppi.GetValue(result, null)));
                                    } else {
                                        // everything else
                                        yield return (ppi.Name.MapTo(() => (IEnumerable)ppi.GetValue(result, null)));
                                    }
                                    break;

                                case PersistableCategory.Dictionary:
                                    yield return (ppi.Name.MapTo(() => (IDictionary)ppi.GetValue(result, null)));
                                    break;

                                case PersistableCategory.Other:
                                    yield return (ppi.Name.MapTo(() => ppi.GetValue(result, null)));
                                    break;
                            }
                        }
                    }
                }
            }

            string IHasValueFromBackingStorage.Value {
                get {
                    var result = ComputedValue;
                    return result == null ? string.Empty : result.ToString();
                }
            }

            IEnumerable<string> IHasValueFromBackingStorage.Values {
                get {
                    var result = ComputedValue;
                    if (result == null) {
                        yield break;
                    }
                    if (result.GetType().IsIEnumerable()) {
                        foreach (var i in from object i in (IEnumerable)result where i != null select i) {
                            yield return i.ToString();
                        }
                    } else {
                        yield return result.ToString();
                    }
                }
            }
        }

        

        protected class ValueMap<TParent> : Map, ICanSetBackingValue, IHasValueFromBackingStorage, IPrefersSingleValue {
            private ValueDelegate<TParent> _route;

            internal ValueMap(string memberName, ValueDelegate<TParent> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _route(() => (TParent)_parentReferenceValue()).Value;
                }
            }

            public void Reset() {
                SetValue("");
            }

            public void AddValue(string value) {
                SetValue(ComputedValue + ", " + value);
            }

            public void SetValue(string value) {
                _route(() => (TParent)_parentReferenceValue()).Value = value;
            }

            string IHasValueFromBackingStorage.Value {
                get {
                    var result = ComputedValue;
                    if (result == null) {
                        return string.Empty;
                    }
                    return result.ToString();
                }
            }

            IEnumerable<string> IHasValueFromBackingStorage.Values {
                get {
                    var result = ComputedValue;
                    if (result != null) {
                        if (result is IEnumerable) {
                            foreach (var each in ((IEnumerable)result).Cast<object>().Where(each => each != null)) {
                                yield return each.ToString();
                            }
                        } else {
                            yield return result.ToString();
                        }
                    }
                }
            }
        }
    }
}