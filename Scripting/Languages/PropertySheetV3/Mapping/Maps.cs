namespace ClrPlus.Scripting.Languages.PropertySheetV3.Mapping {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Tasks;

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

        protected internal class Map : AbstractDictionary<string, View> {
#if DEBUG
            private static int ___i___ = 0;
            private readonly int OID = ___i___++;
#endif

            internal View ParentView;
            protected internal View _thisView;
            protected IDictionary<string, View> _childItems;
            protected Dictionary<string, Func<View>> _dynamicViewInitializers;
            internal protected Action _resolveValue;

            protected internal Queue<ToRoute> Initializers;
            internal GetMacroValueDelegate GetMacroValue;
            private string _memberName;
            protected internal bool _active;

            internal bool Active {
                get {
                    return (_active) || _childItems != null && _childItems.Values.Any(i => i._map.Active);
                } 
                set {
                    _active = value;
                }
            }

            internal string MemberName {
                get {
                    return _memberName ;
                }
                set {
                    _memberName = value;
                }
            }
#if DEBUG
            internal string Identity {
                get {
                    var p = ParentView;
                    var name = "";
                    if (ParentView != null) {
                        name = ParentView._map.Identity + ".";
                    }

                    if(string.IsNullOrEmpty(_memberName)) {
                        if(this is ElementMap) {
                            return name + "[parameter:"+ (this as ElementMap).Parameter +"]";
                        }
                        return name + this.GetType().Name + "-???";
                    }
                    return name+_memberName+"({0})".format(OID);
                }
            }
#endif 

           // protected Value _parentReferenceValue;

            protected Map(string memberName, IEnumerable<ToRoute> childRoutes) {
             /*   _parentReferenceValue = () => {
                  Event<Error>.Raise("View.Map","Accessing unset value--is this a parent object?  {0}", MemberName);
                  return null;
                };
                */
                MemberName = memberName;

                if (childRoutes != null ) {
                    var cr = childRoutes.ToArray();
                    if (cr.Length > 0) {
                        Initializers = new Queue<ToRoute>(cr);
                    }
                }
            }

            #region DictionaryImplementation

            protected internal virtual IDictionary<string, View> ChildItems {
                get {
                    return _childItems ?? (_childItems = new XDictionary<string, View>());
                }
            }

            public override View this[string key] {
                get {
                    if (_childItems == null) {
                        throw new ClrPlusException("Element '{0}' does not exist in map".format(key));
                    }
                    
                    var result = _childItems[key];

                    if (result._map is IReplaceable && _thisView != null && _thisView.FallbackRoute != null) {
                        // if the map is replaceable, and we have a fallback route, let's apply that.
                        var view = _thisView.FallbackRoute();
                        view._map.MemberName = key;
                        MergeChild(_thisView, view);
                    }
                    return result;
                }
                set {
                    // value.map._parentReferenceValue = () => ComputedValue;

                    ChildItems[key] = value;
                }
            }

            public override ICollection<View> Values {
                get {
                    if (_childItems == null) {
                        return new View[0];
                    }
                    return ChildItems.Keys.Select(each => this[each]).ToArray();
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
                // value._map._parentReferenceValue = () => ComputedValue;
                
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
                    return null;
                }
            }

            internal virtual void CopyToModel() {
                if (_childItems != null) {
                    foreach (var i in Values) {
                        if (i._map.Active) {
                            i.CopyToModel();
                        }
                    }
                }
            }

            internal Map RootMap {
                 get {
                     return ParentView == null ? this : ParentView._map.RootMap;
                 }
            }

            internal virtual Map OnAccess(View thisView) {
                _thisView = thisView;
                if (Initializers != null) {
                    lock (this) {
                        while (Initializers.Count > 0) {

                            var childRoute = Initializers.Dequeue();
                            var childView = childRoute();

                            if (childView != null) {

                                if(childView.MemberName == "") {
                                    // this is a fallback map, used to match anything.
                                   thisView.FallbackRoute = childRoute;
                                    continue;
                                }


                                if (childView._map is ElementMap) {
                                    MergeElement(childView);
                                    return this;
                                }

                                var resolvedName = thisView.ResolveAlias(childView._map.MemberName);
                                if (resolvedName.StartsWith("::")) {
                                    RootMap.AddChildRoute(() => Unroll(resolvedName.Substring(2), childView));
                                } else {
                                    MergeChild(thisView, Unroll(resolvedName, childView));                                    
                                }
                            }
                        }
                    }
                }
                return this;
            }

            internal virtual Map AddChildRoute(ToRoute route) {
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

            internal virtual Map AddChildRoutes(IEnumerable<ToRoute> routes) {
                if (routes != null) {
                    lock (this) {
                        if (Initializers == null) {
                            Initializers = new Queue<ToRoute>();
                        }
                        foreach (var i in routes) {
                            if (i != null) {
                                Initializers.Enqueue(i);
                            }
                        }
                    }
                }
                return this;
            }

            protected internal virtual void MergeElement( View childView) {
                Console.WriteLine("NOT SUPPOSED TO MERGE ELEMENT HERE");
            }

            protected virtual void CopyElementsTo(Map childMap) {
            }

            protected internal virtual void MergeChild(View thisView, View childView) {
                if (childView._map is ElementMap) {
                    MergeElement(childView);
                    return;
                }

                var name = childView._map.MemberName;

                // ensure this child's parent is set correctly.
                childView.ParentView = thisView;

                //_thisView = thisView;

                if (!ChildItems.Keys.Contains(name)) {
                    // before adding this, let's check if it's a node map that has 
                    // 
                    if (childView._map is NodeMap) {
                        if ((childView._map as NodeMap)._node is ExpansionPropertyNode) {
                            // this node is a placeholder for an object expansion.
                            var iterator = ((childView._map as NodeMap)._node as ExpansionPropertyNode).ObjectIterator;
                            AddChildRoutes( iterator.GetContents(thisView) );
                            return;
                        }
                    }

                    // we're first -- add it to the view, and get out.
                    // childView.ParentReference = () => ReferenceValue;
                    Add(name, childView);
                    return;
                }

                // modifying this view
                var currentView = ChildItems[name];
                currentView.ParentView = thisView;

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

                currentView.SourceLocations = currentView.SourceLocations.Union(childView.SourceLocations);

                if (childView.GetType().IsGenericType && !currentView.GetType().IsGenericType) {
                    // the new child view has a more specific type.
                    // we need to abandon the old view, and use the new child for the view.
                   // View old = currentView;
                    // currentView = childView;
                    // childView = old;
                    // ChildItems[name] = currentView;
                }


                // if the child view map is replaceable, then simply steal it's child routes 
                if (childView._map is IReplaceable) {
                    //Console.WriteLine("Leveraging {0}/{1}\r\n        into {2}/{3}", childView._map.Identity, childView._map.GetType().Name, currentView._map.Identity, currentView._map.GetType().Name);
                    
                    currentView.FallbackRoute = currentView.FallbackRoute ?? childView.FallbackRoute;

                    currentView._map.Active = childView._map.Active || currentView._map.Active;
                    currentView._map.AddChildRoutes(childView._map.Initializers);
                    
                    // childView._map.GetMacroValue += currentView._map.GetMacroValue;
                    // currentView._map.GetMacroValue = childView._map.GetMacroValue;

                    foreach (var key in currentView._map.Keys) {
                        currentView._map[key].ParentView = currentView;
                    }
                    return;
                }

                // if this current view map is replaceable, then let's go the other way.
                if (currentView._map is IReplaceable) {
                    // Console.WriteLine("Replacing {0}/{1}\r\n        with {2}/{3}", currentView._map.Identity, currentView._map.GetType().Name, childView._map.Identity, childView._map.GetType().Name);
                    currentView.FallbackRoute = currentView.FallbackRoute ?? childView.FallbackRoute;

                    var oldMap = currentView._map;
                    var newMap = childView._map;
                    currentView._map = newMap;



                    newMap._thisView = oldMap._thisView;
                    
                    
                    newMap.ParentView = thisView;

                    //if (oldMap._childItems != null && oldMap.Count > 0) {
                      //  if (childView._map._childItems == null || childView._map._childItems.Count == 0) {
                        //    childView._map._childItems = oldMap._childItems;
                        //}
                        
                    //}

                    newMap.Active = newMap.Active || oldMap.Active;

                    oldMap.AddChildRoutes(newMap.Initializers);
                    newMap.Initializers = oldMap.Initializers;
                    // newMap._parentReferenceValue = oldMap._parentReferenceValue;
                    newMap.GetMacroValue += oldMap.GetMacroValue;

                    // and move any existing children over to the new map.
                    foreach (var key in oldMap.Keys) {
                        newMap.Add(key, oldMap[key]);
                    }
                    foreach (var key in newMap.Keys) {
                        newMap[key].ParentView = currentView;
                    }
                    newMap._resolveValue = oldMap._resolveValue;
                    
                    // handle any Elements
                    oldMap.CopyElementsTo(newMap);

                    return;
                }

                if (currentView._map is ValueMap<object> && childView._map is ValueMap<object> ) {
                    currentView._map.Active = childView._map.Active || currentView._map.Active;
                    currentView._map.AddChildRoutes(childView._map.Initializers);
                    currentView._map.GetMacroValue += childView._map.GetMacroValue;
                    return;
                }

                // if neither is replaceable, then we're in a kind of pickle.
#if DEBUG
                throw new ClrPlusException("Neither map is replaceable [{0}] vs [{1}]".format( currentView._map.Identity , childView._map.MemberName ));
#else 
                throw new ClrPlusException("Neither map is replaceable [{0}] vs [{1}]".format(currentView._map.MemberName, childView._map.MemberName));
#endif
            }
        }

        protected class ReplaceableMap : Map, IReplaceable {
            internal ReplaceableMap(string memberName, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
            }
        }

        protected class IndexedChildMap<TParent> : ReplaceableMap {
            public IEnumerable<ToRoute> childInitializers;
            private bool _initialized;
            private IndexedChildRouteDelegate<TParent> _route;

            internal IndexedChildMap(string memberName, IndexedChildRouteDelegate<TParent> childAccessor, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = childAccessor;
                _active = true;
            }

            internal override Map OnAccess(View thisView) {
                base.OnAccess(thisView);

                if (!_initialized) {
                    _initialized = true;
                    // when this is accessed, we should go thru the parent's #'d children

                    foreach (var index in ParentView.GetIndexedPropertyNames()) {
                        var indexedItem = ParentView.GetProperty(index);
                        var item = indexedItem.map.ChildItems.Values.FirstOrDefault();
                        if(item != null) {
                            // create the map, and execute it and merge the view now.
                            var parentValue = _route(() => (TParent)ParentView.map.ComputedValue, item);

                            var myItem = index.MapTo(() => parentValue)();
                            myItem._map.Active = true;
                            MergeChild(ParentView, myItem);
                        }
                    }

                    /*
                     // old numeric-only index method.
                    for (int i = 0; i <= ParentView.Count; i++) {
                        string s = i.ToString();
                        var indexedItem = ParentView.GetProperty(s);
                        var item = indexedItem.map.ChildItems.Values.FirstOrDefault();
                        if (item != null) {
                            // create the map, and execute it and merge the view now.
                            var parentValue = _route(() => (TParent)_parentReferenceValue(), item);

                            var myItem = s.MapTo(() => parentValue)();
                            myItem._map.Active = true;
                            MergeChild( ParentView, myItem );
                        }

                    }
                     * */
                }
                return this;
            }
        }

        protected class DictionaryMap<TParent, TKey, TVal> : Map, IElements, IHasValueFromBackingStorage  {
            private DictionaryDelegateWithView<TParent, TKey, TVal> _route;
            private List<ToRoute> _childInitializers = new List<ToRoute>();
            public CacheEnumerable<ToRoute> childInitializers;
            private readonly Func<string, string> _keyExchanger;

            private IDictionary<TKey, TVal> Dictionary { get {
                return _route(() => (TParent)ParentView.map.ComputedValue, _thisView);
            }}

            internal DictionaryMap(string memberName, DictionaryDelegateWithView<TParent, TKey, TVal> route, IEnumerable<ToRoute> childRoutes)
                : this(memberName, route,x => x, childRoutes) {
                _route = route;
            }

            internal DictionaryMap(string memberName, DictionaryDelegateWithView<TParent, TKey, TVal> route, Func<string,string> keyExchanger, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = route;
                _keyExchanger = keyExchanger;
            }

            protected internal override object ComputedValue {
                get {
                    return ParentView.map.ComputedValue;
                }
            }

            public override ICollection<string> Keys {
                get {
                    return ChildItems.Keys.Union((from object i in Dictionary.Keys where i != null select i.ToString())).ToArray();
                }
            }

            public override bool ContainsKey(string key) {
                return base.ContainsKey(_keyExchanger(key));
            }

            public override bool Remove(string key) {
                return base.Remove(_keyExchanger(key));
            }

            public override bool TryGetValue(string key, out View value) {
                return base.TryGetValue(_keyExchanger(key), out value);
            }

            internal override void CopyToModel() {
                if(_childItems != null) {
                    foreach(var k in Keys) {
                        this[k].CopyToModel();
                    }
                }
            }


            internal override Map OnAccess(View thisView) {
                // each of the children that already exist (either from the property sheet or the backing collection)
                // must have routes created for each of the routes in this container.

                // and then we hold onto the routes in case there are more elements added after this. ?
                if(Initializers != null && Initializers.Count > 0 ) {
                    lock(this) {
                        while(Initializers.Count > 0) {
                            var childView = Initializers.Dequeue()();
                            if(childView != null) {
                                if (childView._map is ElementMap) {
                                    MergeElement(childView);
                                } else {
                                    MergeChild(thisView, childView);
                                }
                            }
                        }
                    }

                    // now, run the childinitializers over each child.
                    /*
                    if (childInitializers != null) {
                        foreach (var key in Keys) {
                            var childItem = this[key];
                            foreach (var i in childInitializers) {
                                if (childItem._map.Initializers == null) {
                                    childItem._map.Initializers = new Queue<ToRoute>();
                                }
                                if (!childItem._map.Initializers.Contains(i)) {
                                    childItem._map.Initializers.Enqueue(i);
                                }
                            }
                        }
                    }
                     * */
                }
                return this;
            }

            public override void Add(string key, View value) {
                // we should see if this ever gets called on a node that hasn't been initialized by OnAccess...
                InitializeChildItem(value);
                
                base.Add(_keyExchanger(key), value);
            }

            private HashSet<Map> ici = new HashSet<Map>();
            private void InitializeChildItem(View value) {
                if (!ici.Contains(value._map)) {
                    ici.Add(value._map);

                    if(childInitializers != null) {
                        foreach(var i in childInitializers) {
                            if(value._map.Initializers == null) {
                                value._map.Initializers = new Queue<ToRoute>();
                            }
                            if(!value._map.Initializers.Contains(i)) {
                                value._map.Initializers.Enqueue(i);
                            }
                        }

                    }

                }
            }

            protected internal override void MergeChild(View thisView, View childView) {
                base.MergeChild(thisView, childView);
                InitializeChildItem(childView);
            }

            public override View this[string key] {
                get {
                    key = _keyExchanger(key);

                    // if the view we have 
                    if (!ChildItems.ContainsKey(key)) {
                        var _key = (TKey)(object)key;
                       //  var x = Dictionary[_key];

                        // if (Dictionary.ContainsKey(_key)) {
                            var accessor = new Accessor(() => Dictionary[_key], v => Dictionary[_key] = (TVal)v);
                            var childMap = new ValueMap<object>(key, (p,v) => accessor, null);

                            childMap.GetMacroValue += (name, context) => {
                                return name == "ElementId" || name == MemberName ? key.SingleItemAsEnumerable() : null;
                            };
                            MergeChild(ParentView, new View(childMap));
                        // }
                    }
                    InitializeChildItem(ChildItems[key]);
                    return ChildItems[key];
                }
                set {

                    InitializeChildItem(value);
                    key = _keyExchanger(key);

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
                    var _key = (TKey)(object)_keyExchanger(item.Parameter);

                    var accessor = new Accessor(() => Dictionary[_key], v => Dictionary[_key] = (TVal)v);

                    var childMap = new ValueMap<object>(_key.ToString(), (p,v) => accessor, item.Initializers);


                    childMap.GetMacroValue += (name, context) => {
                        return name == "ElementId" || name == MemberName ? _key.ToString().SingleItemAsEnumerable() : null;
                    };

                    MergeChild(ParentView, new View(childMap) {
                        _propertyNode = childView._propertyNode
                    });

                } else {
                    throw new ClrPlusException("map really should be an elementmap here...");
                }
            }
        }

        protected class ElementMap : ReplaceableMap {
            protected internal string Parameter;

            internal ElementMap(string memberName, string parameter, INode node)
                : base(memberName, (node is ObjectNode) ? (node as ObjectNode).Routes : null) {
                Parameter = parameter;
                Active = true;
            }
        }

        protected class EnumerableMap<TParent> : Map, IHasValueFromBackingStorage, IPrefersMultipleValues {
            private EnumerableDelegateWithView<TParent> _route;

            internal EnumerableMap(string memberName, EnumerableDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _route(() => (TParent)ParentView.map.ComputedValue, _thisView);
                }
            }

            string IHasValueFromBackingStorage.Value {
                get {
                    var result = (IEnumerable)ComputedValue;
                    if (result != null) {
                        return result.Cast<object>().Aggregate("", (current, i) => current + "," + result).Trim(',',' ');
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
            private ListDelegateWithView<TParent> _route;

            internal ListMap(string memberName, ListDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _route(() => (TParent)ParentView.map.ComputedValue, _thisView);
                }
            }

            public void Reset() {
                ((IList)ComputedValue).Clear();
            }

            public void AddValue(string value) {
                // how do we transform the given value 
                // into the value that we're trying to add?
                //value = ParentView.ResolveMacrosInContext(value);
                ((IList)ComputedValue).Add(value);
            }

            string IHasValueFromBackingStorage.Value {
                get {
                    var result = ((IList)ComputedValue);
                    if (result != null) {
                        return result.Cast<object>().Aggregate("", (current, i) => current + "," + result).Trim(',',' ');
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

        protected class PlaceholderMap : ReplaceableMap {
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

        protected class NodeMap : PlaceholderMap {
            internal INode _node;
            internal NodeMap(string memberName, INode node)
                : base(memberName, node is ObjectNode ? (node as ObjectNode).Routes : null) {
                _node = node;
                _active = true;    
            }
        }

        protected class RoutableMap: Map {
            internal RoutableMap(string memberName, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {

                // when this map is activated, add our children to it.
                AddChildRoute(() => {

                    AddChildRoutes(MemberRoutes);
                    return null;
                });
            }

            protected IEnumerable<ToRoute> MemberRoutes {
                get {
                    var result = ComputedValue;

                    if(result != null) {
                        var type = result.GetType();

                        // allows a member object to add a field/property called "MemberRoutes" and have it define child routes too.
                        var customMemberRoutesProperty = type.GetReadableElements().FirstOrDefault(ppi => ppi.Name == "MemberRoutes");
                        if(customMemberRoutesProperty != null) {
                            var customRoutes = customMemberRoutesProperty.GetValue(result, null) as IEnumerable<ToRoute>;
                            if(customRoutes != null) {
                                foreach(var i in customRoutes) {
                                    yield return i;
                                }
                            }
                        }

                        foreach(var each in type.GetPersistableElements().Where(each => each.Name != "MemberRoutes")) {
                            var ppi = each;

                            switch(ppi.ActualType.GetPersistableInfo().PersistableCategory) {
                                case PersistableCategory.String:
                                    yield return (ppi.Name.MapTo(new Accessor(() => {
                                        return ppi.GetValue(result, null);
                                    }, v => {
                                        ppi.SetValue(result, v, null);
                                    })));
                                    break;

                                case PersistableCategory.Nullable:
                                case PersistableCategory.Enumeration:
                                case PersistableCategory.Parseable:
                                    // parsable types should probably be returned as a Property.

                                    yield return (ppi.Name.MapTo(new Accessor(() => ppi.GetValue(result, null), v => {
                                        // if the value is null, try to set null..
                                        if(v == null) {
                                            ppi.SetValue(result, null, null);
                                            return;
                                        }

                                        // if the types are compatible, assign directly
                                        if(ppi.ActualType.IsInstanceOfType(v)) {
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
                                    if(typeof(IList).IsAssignableFrom(ppi.ActualType)) {
                                        // it's actually an IList
                                        yield return (ppi.Name.MapTo(() => (IList)ppi.GetValue(result, null)));
                                    }
                                    else {
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

                        // then check to see if the object has a function "IEnumerable<ToRoute> GetMemeberRoutes(View view)" and call it
                        var fn = type.GetMethod("GetMemberRoutes", new[] { typeof(View) });

                        if(fn != null) {
                            var moreRoutes = fn.Invoke(result, new object[] {
                                _thisView
                            }) as IEnumerable<ToRoute>;
                            if(moreRoutes != null) {
                                foreach(var i in moreRoutes) {
                                    yield return i;
                                }
                            }
                        }

                    }
                }
            }
        }

        protected class ObjectMap<TParent> : RoutableMap, IHasValueFromBackingStorage, IPrefersComputedValue {
            private RouteDelegateWithView<TParent> _route;

            internal ObjectMap(string memberName, RouteDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _route(() => (TParent)ParentView.map.ComputedValue, _thisView);
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
                     if (result is string ) {
                         var r = result.ToString().Split( new[] {','} , StringSplitOptions.RemoveEmptyEntries);
                         if (r.Length > 1) {
                             foreach (var s in r) {
                                 yield return s.Trim();
                             }
                         } else {
                             yield return result.ToString();
                         }

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

        protected class ValueMap<TParent> : RoutableMap, ICanSetBackingValue, IHasValueFromBackingStorage, IPrefersSingleValue {
            private ValueDelegateWithView<TParent> _route;

            internal bool RoutesMatch(ValueMap<TParent> otherMap ) {
                return (otherMap._route == _route);
            }

            internal ValueMap(string memberName, ValueDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes)
                : base(memberName, childRoutes) {
                _route = route;
            }

            protected internal override object ComputedValue {
                get {
                    return _route(() => (TParent)ParentView.map.ComputedValue, _thisView).Value;
                }
            }

            public void Reset() {
                SetValue("");
            }

            public void AddValue(string value) {
                var v = ComputedValue;
                
                if (v != null) {
                    var val = v.ToString();
                    if (!string.IsNullOrEmpty(val)) {
                        SetValue(v + ", " + value);
                        return;
                    }
                }
                SetValue(value);    
            }

            public void SetValue(string value) {
                //value = ParentView.ResolveMacrosInContext(value);
                _route(() => (TParent)ParentView.map.ComputedValue, _thisView).Value = value;
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
                        if(result is string) {
                            var r = result.ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if(r.Length > 1) {
                                foreach(var s in r) {
                                    yield return s.Trim();
                                }
                            }
                            else {
                                yield return result.ToString();
                            }

                            yield break;
                        }

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