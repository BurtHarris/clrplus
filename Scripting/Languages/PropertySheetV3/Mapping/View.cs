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
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Dynamic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Core.Collections;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheet;
    using RValue;
    using Utility;

    public delegate IEnumerable<string> GetMacroValueDelegate(string valueName, IValueContext context);
    public delegate string GetSingleMacroValueDelegate(string valueName, IValueContext context);

    // ReSharper disable PossibleNullReferenceException
    public partial class View : DynamicObject, IValueContext {


        protected static string[] _emptyStringArray = new string[0];
        private static readonly Regex _macro = new Regex(@"(\$\{(.*?)\})");
        //private Action _resolveValue;
        public IEnumerable<SourceLocation> SourceLocations = new SourceLocation[0];

        private PropertyNode _propertyNode;

        private readonly Lazy<List<IDictionary<string, IValue>>> _metadata = new Lazy<List<IDictionary<string, IValue>>>(() => new List<IDictionary<string, IValue>>());
        private readonly Lazy<List<IDictionary<string, string>>> _aliases = new Lazy<List<IDictionary<string, string>>>(() => new List<IDictionary<string, string>>());

        private static readonly IDictionary<string, IValue> _empty = new Dictionary<string, IValue>();
        private IDictionary<string, IValue> _metadataValue;
        protected ToRoute FallbackRoute;

        private Map _map;

        private Map map {
            get {
                return _map.OnAccess(this);
            }
        }

        public View ParentView {
            get {
                return _map.ParentView;
            }
            set {
                //_map._thisView = this;
                _map.ParentView = value;
            }
        }

        protected PropertyNode AggregatePropertyNode {
            get {
                return _propertyNode ?? (_propertyNode = new PropertyNode());
            }
        }

        protected bool HasProperty {
            get {
                return _propertyNode != null && _propertyNode.Any();
            }
        }

        

        #region Metadata 
        internal IDictionary<string, IValue> Metadata {
            get {
                if(_metadataValue == null) {
                    if(_metadata.IsValueCreated) {
                        _metadataValue = new XDictionary<string, IValue>();
                        foreach(var i in _metadata.Value) {
                            foreach(var k in i.Keys) {
                                _metadataValue[k] = i[k];
                            }
                        }
                    }
                    else {
                        _metadataValue = _empty;
                    }
                }
                return _metadataValue;
            }
        }

        public string GetMetadataValue(string metadataName, IValueContext context, bool checkParent = true) {
            return Metadata.ContainsKey(metadataName) ? Metadata[metadataName].GetValue(context) : (ParentView == null || checkParent == false) ? null : ParentView.GetMetadataValue(metadataName, context);
        }

        public IEnumerable<string> GetMetadataValues(string metadataName, IValueContext context, bool checkParent = true) {
            return Metadata.ContainsKey(metadataName) ? Metadata[metadataName].GetValues(context) : (ParentView == null || checkParent == false) ? Enumerable.Empty<string>() : ParentView.GetMetadataValues(metadataName, context);
        }

        public string GetMetadataValueHarder(string metadataName, string parameter, bool checkParent = true) {
            if(string.IsNullOrEmpty(parameter) || ParentView == null || ParentView.ParentView == null || !ParentView.ParentView.HasChild(MemberName)) {
                return GetMetadataValue(metadataName, this, checkParent);
            }
            return GetMetadataValue(metadataName, this, false) ?? ParentView.ParentView.GetProperty(MemberName).GetMetadataValue(metadataName, this, false) ?? (checkParent ? ParentView.GetMetadataValue(metadataName, this) : null);
        }

        public IEnumerable<string> GetMetadataValuesHarder(string metadataName, string parameter, bool checkParent = true) {
            if(string.IsNullOrEmpty(parameter) || ParentView == null || ParentView.ParentView == null || !ParentView.ParentView.HasChild(MemberName)) {
                return GetMetadataValues(metadataName, this, checkParent);
            }
            var result = GetMetadataValues(metadataName, this, false);
            if(!result.IsNullOrEmpty()) {
                return result;
            }
            result = ParentView.ParentView.GetProperty(MemberName).GetMetadataValues(metadataName, this, false);
            if(!result.IsNullOrEmpty()) {
                return result;
            }

            return (checkParent ? ParentView.GetMetadataValues(metadataName, this) : Enumerable.Empty<string>());
        }

        public IEnumerable<string> GetMetadataKeys(string prefix = null) {
            return string.IsNullOrEmpty(prefix) ? Metadata.Keys : Metadata.Keys.Where(each => each.StartsWith(prefix));
        }

        public IDictionary<string, string> GetMetadataItems(string prefix) {
            return GetMetadataKeys(prefix).ToXDictionary(each => each.Substring(prefix.Length), each => GetMetadataValue(each, this));
        }
        #endregion

        #region Macros
        private static readonly string[] _defines = new[] { "define.", "defines." };
        private IEnumerable<string> ResolveDefinedMacro(string valueName, IValueContext context) {
            if(Metadata != _empty) {
                foreach(var i in _defines) {
                    var match = i + valueName;
                    if(Metadata.ContainsKey(match)) {
                        var define = Metadata[match];

                        var result = define.GetValues(context ?? this);

                        return result;
                    }
                }
            }
            // return null if there is not a match
            return null;
        }

        private IEnumerable<string> AcceptFirstAnswer(GetMacroValueDelegate getMacroDelegate, string innerMacro, IValueContext originalContext) {
            if(getMacroDelegate == null) {
                return null;
            }
            var delegates = getMacroDelegate.GetInvocationList();
            return delegates.Count() > 1 ? delegates.Reverse().Select(each => AcceptFirstAnswer(each as GetMacroValueDelegate, innerMacro, originalContext)).FirstOrDefault(each => each != null) : getMacroDelegate(innerMacro, originalContext);
        }

        private IEnumerable<string> LookupMacroValue(string innerMacro, IValueContext originalContext) {

            return AcceptFirstAnswer(_map.GetMacroValue, innerMacro, originalContext ?? this) ?? (ParentView != null ? ParentView.LookupMacroValue(innerMacro, originalContext ?? this) : null);
        }

        private IEnumerable<string> LookupPropertyForMacro(string valuename, IValueContext context) {
            var v = GetProperty(ResolveAlias(valuename), false);

            if (v == null || v._map is ElementMap) {
                return null;
            }

            return v.Values;
        }


        public string GetSingleMacroValue(string innerMacro, object[] items = null) {
            var v = LookupMacroValue(innerMacro, this);
            if ( v != null ) {
                var a = v.ToArray();
                if (a.Length > 0) {
                    return ResolveMacrosInContext(a.CollapseToString(), items);        
                }
            }
            return null;
        }

        public IEnumerable<string> GetMacroValues(string innerMacro, object[] items = null) {
            var vals = LookupMacroValue(innerMacro, this);
            return vals == null? null: vals.Select(each => ResolveMacrosInContext(each, items));
        }

        public string ResolveMacrosInContext(string value, object[] eachItems = null, bool itemsOnly = false) {
            bool keepGoing;

            if(string.IsNullOrEmpty(value)) {
                return value;
            }

            do {
                keepGoing = false;

                var matches = _macro.Matches(value);
                foreach(var m in matches) {
                    var match = m as Match;
                    var innerMacro = match.Groups[2].Value;
                    var outerMacro = match.Groups[1].Value;

                    string replacement = null;

                    var ndx = GetIndex(innerMacro);
                    if(!itemsOnly && ndx < 0) {
                        // get the first responder.
                        var indexOfDot = innerMacro.IndexOf('.');

                        if(indexOfDot > -1) {
                            var membr = innerMacro.Substring(0, indexOfDot);
                            var val = LookupMacroValue(membr, this);
                            if(val != null) {
                                var obval = val.SimpleEval2(innerMacro.Substring(indexOfDot + 1).Trim());
                                if(obval != null) {
                                    replacement = obval.ToString();
                                }
                            }
                        }
                        else {
                            var mv = LookupMacroValue(innerMacro, this);
                            if (!mv.IsNullOrEmpty()) {
                                replacement = mv.CollapseToString();
                            }
                            // replacement = LookupMacroValue(innerMacro, this).CollapseToString();
                        }
                    }

                    if(!eachItems.IsNullOrEmpty()) {
                        // try resolving it as an ${each.property} style.
                        // the element at the front is the 'this' value
                        // just trim off whatever is at the front up to and including the first dot.
                        try {
                            if(ndx >= 0) {
                                if(ndx < eachItems.Length) {
                                    value = value.Replace(outerMacro, eachItems[ndx].ToString());
                                    keepGoing = true;
                                }
                            }
                            else {
                                if(innerMacro.Contains(".")) {
                                    var indexOfDot = innerMacro.IndexOf('.');
                                    ndx = GetIndex(innerMacro.Substring(0, indexOfDot));
                                    if(ndx >= 0) {
                                        if(ndx < eachItems.Length) {
                                            innerMacro = innerMacro.Substring(indexOfDot + 1).Trim();

                                            var v = eachItems[ndx].SimpleEval2(innerMacro);
                                            if(v != null) {
                                                var r = v.ToString();
                                                value = value.Replace(outerMacro, r);
                                                keepGoing = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch {
                            // meh. screw em'
                        }
                    }

                    if(replacement != null) {
                        value = value.Replace(outerMacro, replacement);
                        keepGoing = true;
                        break;
                    }
                }
            } while(keepGoing);
            return value.Replace("${ElementId}", "").Replace("${conditionFolder}", "");
        }

        #endregion

        /* public IEnumerable<string> TryGetRValueInContext(string property) {
            var p = GetProperty(property);
            if (p != null) {
                return p.Values;
            }
            return null;
        }
        */

        #region aliases
        public Selector ResolveAlias(Selector selector) {
            var resolved = ResolveAlias(selector.Name);
            if(resolved != selector.Name) {
                return new Selector(resolved, selector.Parameter, selector.SourceLocation, selector.AfterTheParameter);
            }
            return selector;
        }

        public string ResolveAlias(string aliasName) {
            if(_aliases.IsValueCreated) {
                foreach(var aliases in _aliases.Value.Where(aliases => aliases.ContainsKey(aliasName))) {
                    return aliases[aliasName];
                }
            }

            // return the original alias name if there isn't a match.
            if(ParentView != null) {
                return ParentView.ResolveAlias(aliasName);
            }
            return aliasName;
        }
        #endregion

        protected readonly static Action RESOLVED = () => {};
        protected View(Map instance) {
            instance._thisView = this;
            _map = instance;

            // add the property lookup as the first macro resolver 
            // (since they get reversed, it ends up being last) 
              _map.GetMacroValue += LookupPropertyForMacro;

            // add the Defined Macro resolver next
            // (which makes it higher priority than properties)
            _map.GetMacroValue += ResolveDefinedMacro;

            _map._resolveValue = () => {
                // is there a property, and can it take a value?
                if (_map._thisView.HasProperty && _map is ICanSetBackingValues) {
                    // prefer those who can take a collection
                    _map._thisView.AggregatePropertyNode.SetResults(map as ICanSetBackingValues, _map._thisView);
                } else {
                    // but single values are good too.
                    if(_map._thisView.HasProperty && _map is ICanSetBackingValue) {
                        _map._thisView.AggregatePropertyNode.SetResult(map as ICanSetBackingValue, _map._thisView);
                    }
                }
                _map.Active = true;
                // regardless, never call this again...
                _map._resolveValue = RESOLVED;
            };
        }

       
        internal void InitializeAtRootLevel(INode node) {
            if(node is PropertyNode) {
                AggregatePropertyNode.AddRange(node as PropertyNode);
            }
            if(node is ObjectNode) {
                _aliases.Value.Add((node as ObjectNode).Aliases.Value);
            }
            if(node.Metadata.IsValueCreated) {
                _metadata.Value.Add(node.Metadata.Value);
            }


            if(node is RootPropertySheet) {
                /*
                foreach(var i in (node as RootPropertySheet).Imports) {
                    if(i.Metadata.IsValueCreated) {
                        _metadata.Value.Add(i.Metadata.Value);
                    }
                    _aliases.Value.Add(i.Aliases.Value);
                }
                 */
            }
            _map.Active = true;
        }

        protected View(Map instance, INode node)
            : this(instance) {
            InitializeAtRootLevel(node);
        }

        protected static View Unroll(string memberName, View view) {
            if (view._map.MemberName != memberName) {
                var p = memberName.IndexOf('.');
                if (p > -1) {
                    return new View(new PlaceholderMap(memberName.Substring(0, p), new ToRoute[] {
                        (() => Unroll(memberName.Substring(p + 1), view))
                    }) {
                        Active = view._map.Active
                    });
                }
                view._map.MemberName = memberName;
            }
            return view;
        }

        protected static Map Unroll(string memberName, Func<string, Map> map) {
            var p = memberName.IndexOf('.');
            if (p > -1) {
                return new PlaceholderMap(memberName.Substring(0, p), new ToRoute[] {
                    (() => new View(Unroll(memberName.Substring(p + 1), map)))
                });
            }
            return map(memberName);
        }

        protected static Map Unroll(Selector selector, INode node) {
            if (selector.IsCompound) {
                return new PlaceholderMap(selector.Prefix.Name, new ToRoute[] {
                    (() => new View(Unroll(selector.Suffix, node), node))
                }) {
                    Active = true
                };
            }

            if (selector.HasParameter) {
                // add an initializer to the new node that adds the element to the child container.
                return new PlaceholderMap(selector.Name, new ToRoute[] {
                    (() => new View(new ElementMap(null, selector.Parameter, node), node))
                }) {
                    Active = true
                };
            }

            return new NodeMap(selector.Name, node);
        }

        public string MemberName { get {
            return _map.MemberName;
        }}

        public static implicit operator string(View v) {
            return v.Value;
        }

        public static implicit operator string[](View v) {
            return v.Values.ToArray();
        }

        internal View(Selector selector, INode node)
            : this(Unroll(selector, node), node) {
            SourceLocations = selector.SourceLocation.SingleItemAsEnumerable();
        }

        private View RootView {
            get {
                return ParentView == null ? this : ParentView.RootView;
            }
        }

        public IEnumerable<string> GetIndexedPropertyNames() {
            var ret =  map.Keys.Where(each => each.StartsWithNumber()).SortBy(each => each, new Core.Extensions.Comparer<string>((left, right) => {
                var l = (left ?? string.Empty).Split('#');
                var r = (right ?? string.Empty).Split('#');
                var c = Math.Min(l.Length , r.Length );
                for (var i = 0; i < c; i++) {
                    var n = l[i].ToInt32(-1) - r[i].ToInt32(-1);
                    if (n != 0) {
                        return n;
                    }
                }
                // shorter array means lower number.
                return l.Length - r.Length;
            }));

            var rrr = ret.ToArray();
            return rrr;
        }

        public IEnumerable<string> GetChildPropertyNames() {
            return map.Keys.Where( each => !each.StartsWithNumber());
        }

        public IEnumerable<string> ReplaceableChildren {
            get {
                return map.Keys.Where(each => map.ChildItems[each]._map is IReplaceable);
            }
        }

        public int Count {
            get {
                return GetIndexedPropertyNames().Count();
            }
        }


        public bool HasChild(string propertyName) {
            return map.ContainsKey(propertyName) || (map.Keys.Where(each => each.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase)).Select(i => map[i])).Any();
        }

        public bool HasChildren { get {
            return map.ChildItems.Keys.Any(each => !each.StartsWith("$$"));
        }}

        public bool IsPlaceholder() {
            return map is PlaceholderMap;
        }

        public bool IsObjectNode {
            get {
                return (map is NodeMap) && (_map as NodeMap)._node is ObjectNode;
            }
        }

        public View GetProperty(string propertyName, bool createImplicitly = true) {
            // this falls back to case insensitive matches if th property didn't exist.
            if (propertyName.Contains('.')) {
                return GetChild(propertyName, createImplicitly); // let that unroll the path.to.property
            }

            propertyName = ResolveAlias(propertyName);
            if (propertyName.StartsWith("::")) {
                return RootView.GetChild(propertyName.Trim(':'), createImplicitly);
            }

            if (map.ContainsKey(propertyName)) {
                return map[propertyName];
            }

            // cheat: let's see if there is a case insensitive version:
            var result = (map.Keys.Where(each => each.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase)).Select(i => map[i])).FirstOrDefault();
            if (result != null) {
                return result;
            }

            // let's add a placeholder node for it then. 
            
            return createImplicitly?  CreatePlaceholderView(propertyName) : null;
        }


        internal View GetChild(Selector selector, bool createImplicity=true) {
            if(selector == null || selector.IsEmpty ) {
                return this;
            }

            if (string.IsNullOrEmpty(selector.Name) && selector.Parameter != null) {
                selector = new Selector("*",selector.Parameter,selector.SourceLocation,selector.AfterTheParameter);
            }

            selector = ResolveAlias(selector);

            if(selector.IsGlobal) {
                return RootView.GetChild(selector.DeGlobaled, createImplicity);
            }
            
            if (selector.AfterTheParameter.Is()) {
                return GetChild(selector.WithoutAfterTheParameter, createImplicity).GetChild(selector.AfterSelector, createImplicity);
            }

            if(selector.IsCompound) {
                var name = selector.Prefix.Name;
                return HasChild(name) ? GetProperty(name, createImplicity).GetChild(selector.Suffix, createImplicity) : (createImplicity ? CreatePlaceholderView(name).GetChild(selector.Suffix, createImplicity) : null);
            }

            if (selector.IsSpecialCase) {
                // this ensures a special case where a selector is resolving, but it has an empty auto-condition in it.
                // this should make sure that '*[].foo' is the same thing as 'foo' 
                return GetChild(selector.AfterSelector, createImplicity);
            }

            if (selector.HasParameter) {
                return HasChild(selector.Name) ? GetProperty(selector.Name, createImplicity).GetElement(selector.Parameter) : (createImplicity ? CreatePlaceholderView(selector.Name).GetElement(selector.Parameter) : null);
            }

            return GetProperty(selector.Name, createImplicity);
        }

        private View CreatePlaceholderView(string placeholderName) {
            return map.GetOrAdd(placeholderName, () => new View(new PlaceholderMap(placeholderName, Enumerable.Empty<ToRoute>())));
        }

        public View GetElement(string elementName) {
            var child = map as IElements;
            if (child != null /* && child.ElementDictionary.ContainsKey(elementName) */) {
                return child.ElementDictionary[elementName];
            }
            Event<Error>.Raise("View.GetElement","object is not collection ");
            return null;
        }

        public IEnumerable<string> Values {
            get {
                if (!(_map is IHasValueFromBackingStorage)) {
                    // if we can't get the value, the propertynode is the only choice.
                    return HasProperty ? AggregatePropertyNode.GetValues(this).ToArray() : new string[0];
                }
                map._resolveValue(); // push the value to the backing object if neccesary first
                return (map as IHasValueFromBackingStorage).Values.ToArray();   
            }
            set {
                if(_map is ICanSetBackingValues) {
                    (_map as ICanSetBackingValues).Reset();
                    value.ForEach((_map as ICanSetBackingValues).AddValue );
                }
                else {
                    AggregatePropertyNode.SetCollection(new Collection(null, value.Select( each => new Scalar(null, each))));
                }
            }
        }

        public string Value {
            get {
                if (!(_map is IHasValueFromBackingStorage)) {
                    // if we can't get the value, the propertynode is the only choice.
                    return HasProperty ? AggregatePropertyNode.GetValue(this) : string.Empty;
                }
                map._resolveValue(); // push the value to the backing object if neccesary first
                return (map as IHasValueFromBackingStorage).Value;
            } 
            set {
                if (_map is ICanSetBackingValue) {
                    (_map as ICanSetBackingValue).SetValue(value);
                } else {
                    AggregatePropertyNode.SetValue(new Scalar(null,value ));
                }
            }
        }

        public void AddChildRoutes(IEnumerable<ToRoute> routes) {
            _map.AddChildRoutes(routes);
        }
        public void AddChildRoute(ToRoute route) {
            _map.AddChildRoutes(route.SingleItemAsEnumerable());
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
            // good for accessing child dictionary members.
            var index = indexes.Select(each => each.ToString()).Aggregate((current, each) => current + ", " + each).Trim(' ', ',');

            if (index.StartsWithNumber( )) {
                result = GetProperty(GetIndexedPropertyNames().Skip( index.ToInt32(0)).FirstOrDefault());
                return true;
            }

            var child = GetElement(index);

            if (child == null) {
                Console.WriteLine("object doesn't have child element [{0}] -- returning empty string", index);
                result = string.Empty;
                return true;
            }

            result = child;
            return true;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value) {
            // good for accessing child dictionary members.
            return base.TrySetIndex(binder, indexes, value);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            // returns a child reference 
            result = GetProperty(binder.Name);
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value) {
            return base.TrySetMember(binder, value);
        }

        public override bool TryConvert(ConvertBinder binder, out object result) {
            if (binder.Type == typeof (string[])) {
                result = Values;
                return true;
            }

            var ppi = binder.Type.GetPersistableInfo();
            switch (ppi.PersistableCategory) {
                case PersistableCategory.String:
                    result = Value;
                    return true;

                case PersistableCategory.Nullable:
                case PersistableCategory.Parseable:
                    result = binder.Type.ParseString(Value);
                    return true;

                case PersistableCategory.Array:
                case PersistableCategory.Enumerable:
                    if (ppi.ElementType.IsParsable()) {
                        result = Values.Select(each => ppi.ElementType.ParseString(each)).ToArrayOfType(ppi.ElementType);
                        return true;
                    }
                    break;

                case PersistableCategory.Enumeration:
                    var value = Value;
                    if (Enum.IsDefined(binder.Type, value)) {
                        result = Enum.Parse(binder.Type, value, true);
                        return true;
                    }
                    break;
            }

            result = map.ComputedValue;
            return true;
        }

        public void CopyToModel() {
            if (map.Active) {
                var x = Values;
                map.CopyToModel();
            }
        }

        private int GetIndex(string innerMacro) {
            int ndx;
            if (!Int32.TryParse(innerMacro, out ndx)) {
                return innerMacro.Equals("each", StringComparison.CurrentCultureIgnoreCase) ? 0 : -1;
            }
            return ndx;
        }

        public void AddMacro(string name, string value) {
            _map.GetMacroValue += (valueName, context) => valueName == name ? value.SingleItemAsEnumerable() : null;
        }

        public void AddMacroHandler(GetMacroValueDelegate macroHandler) {
            if (macroHandler != null) {
                _map.GetMacroValue += macroHandler;
            }
        }
        public void AddMacroHandler(GetSingleMacroValueDelegate macroHandler) {
            if (macroHandler != null) {
                _map.GetMacroValue += (s, c) => {
                    var v = macroHandler(s, c);
                    return v != null ? v.SingleItemAsEnumerable() : null;
                };
            }
        }
    }

    internal class View<TParent> : View {
        /*
        public View(Map instance, INode node) : base(instance,node) {
            
        }

        public View(Map instance) : base(instance) {
            
        }

        public View(Selector selector, INode node) : base(selector, node) {
            
        }
        */

        public View(string memberName, RouteDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes)
            : base(Unroll(memberName, (member) => new ObjectMap<TParent>(member, route, childRoutes))) {
        }

        internal View(RootPropertySheet rootNode, Route<TParent> backingObjectAccessor)
            : base(new ObjectMap<TParent>("ROOT", (p,v) => backingObjectAccessor, null)) {
            // used for the propertysheet itself.
        }

        internal View(string memberName, ValueDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes)
            : base(Unroll(memberName, (member) => new ValueMap<TParent>(member, route, childRoutes))) {
        }

        internal View(string memberName, ListDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes)
            : base(Unroll(memberName, (member) => new ListMap<TParent>(member, route, childRoutes))) {
        }

        internal View(string memberName, EnumerableDelegateWithView<TParent> route, IEnumerable<ToRoute> childRoutes)
            : base(Unroll(memberName, (member) => new EnumerableMap<TParent>(member, route, childRoutes))) {
        }

        public View(string memberName, IndexedChildRouteDelegate<TParent> childAccessor, IEnumerable<ToRoute> childRoutes)
            : base(Unroll( memberName, (member) => new IndexedChildMap<TParent>(member, childAccessor, null) {
                childInitializers = childRoutes
            })) {
        }

    }

    internal class View<TParent, TKey, TVal> : View {
        public View(string memberName, DictionaryDelegateWithView<TParent, TKey, TVal> route, IEnumerable<ToRoute> childRoutes)
            : base(Unroll(memberName, (member) => new DictionaryMap<TParent, TKey, TVal>(member, route, null) {
                childInitializers = childRoutes.ToCacheEnumerable()
            })) {
            // childRoutes are to be used as initializers for the children, not for the dictionary itself.
        }

        public View(string memberName, DictionaryDelegateWithView<TParent, TKey, TVal> route, Func<string, string> keyExchanger, IEnumerable<ToRoute> childRoutes)
            : base(Unroll(memberName, (member) => new DictionaryMap<TParent, TKey, TVal>(member, route, keyExchanger, null) {
                childInitializers = childRoutes.ToCacheEnumerable()
            })) {
            // childRoutes are to be used as initializers for the children, not for the dictionary itself.
        }
    }

    // ReSharper restore PossibleNullReferenceException
}