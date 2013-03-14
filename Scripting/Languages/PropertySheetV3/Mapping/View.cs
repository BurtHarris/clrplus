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
    using System.Dynamic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Core.Collections;
    using Core.Extensions;
    using RValue;
    using Utility;

    public delegate string GetMacroValueDelegate(string valueName, IValueContext context);

    // ReSharper disable PossibleNullReferenceException
    public partial class View : DynamicObject, IValueContext {
        protected static string[] _emptyStringArray = new string[0];
        private static readonly Regex _macro = new Regex(@"(\$\{(.*?)\})");
        private Action _resolveValue;
        private PropertyNode _propertyNode;

        private readonly Lazy<List<IDictionary<string, IValue>>> _metadata = new Lazy<List<IDictionary<string, IValue>>>(() => new List<IDictionary<string, IValue>>());
        private readonly Lazy<List<IDictionary<string, string>>> _aliases = new Lazy<List<IDictionary<string, string>>>(() => new List<IDictionary<string, string>>());

        private static IDictionary<string, IValue> _empty = new Dictionary<string, IValue>();
        private IDictionary<string, IValue> _metadataValue;

        private Map _map;

        private Map map {
            get {
                return _map.OnAccess(this);
            }
        }

        protected View ParentView {
            get {
                return _map.ParentView;
            }
            set {
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

        public string ResolveMacro(string valueName, IValueContext context) {
            if (Metadata != _empty) {
                var match = "defines." + valueName;
                if (Metadata.ContainsKey(match)) {
                    var define = Metadata[match];
                    var ctx = define.Context;
                    define.Context = context ?? this;
                    var result = define.Value;
                    define.Context = ctx;
                    return result;
                }
            }
            // return null if there is not a match
            return null;
        }

        public IDictionary<string, IValue> Metadata {
            get {
                if (_metadataValue == null) {
                    if (_metadata.IsValueCreated) {
                        _metadataValue = new XDictionary<string, IValue>();
                        foreach (var i in _metadata.Value) {
                            foreach (var k in i.Keys) {
                                _metadataValue[k] = i[k];
                            }
                        }
                    } else {
                        _metadataValue = _empty;
                    }
                }
                return _metadataValue;
            }
        }

        private string AcceptFirstAnswer(GetMacroValueDelegate getMacroDelegate, string innerMacro, IValueContext originalContext) {
            if (getMacroDelegate == null) {
                return null;
            }
            var delegates = getMacroDelegate.GetInvocationList();
            return delegates.Count() > 1 ? delegates.Reverse().Select(each => AcceptFirstAnswer(each as GetMacroValueDelegate, innerMacro, originalContext)).FirstOrDefault(each => each != null) : getMacroDelegate(innerMacro, originalContext);
        }

        private string SearchForMacro(string innerMacro, IValueContext originalContext) {
            return AcceptFirstAnswer(_map.GetMacroValue, innerMacro, originalContext) ?? (ParentView != null ? ParentView.SearchForMacro(innerMacro, originalContext) : null);
        }

        private string ProcessMacroInternal(string value, object[] eachItems) {
            bool keepGoing;
            if (value == null) {
                return null;
            }

            do {
                keepGoing = false;

                var matches = _macro.Matches(value);
                foreach (var m in matches) {
                    var match = m as Match;
                    var innerMacro = match.Groups[2].Value;
                    var outerMacro = match.Groups[1].Value;

                    string replacement = null;

                    var ndx = GetIndex(innerMacro);
                    if (ndx < 0) {
                        // get the first responder.
                        var indexOfDot = innerMacro.IndexOf('.');

                        if (indexOfDot > -1) {
                            var membr = innerMacro.Substring(0, indexOfDot);
                            var val = SearchForMacro(membr, this);
                            if (val != null) {
                                var obval = val.SimpleEval2(innerMacro.Substring(indexOfDot + 1).Trim());
                                if (obval != null) {
                                    replacement = obval.ToString();
                                }
                            }
                        } else {
                            replacement = SearchForMacro(innerMacro, this);
                        }
                    }

                    if (!eachItems.IsNullOrEmpty()) {
                        // try resolving it as an ${each.property} style.
                        // the element at the front is the 'this' value
                        // just trim off whatever is at the front up to and including the first dot.
                        try {
                            if (ndx >= 0) {
                                if (ndx < eachItems.Length) {
                                    value = value.Replace(outerMacro, eachItems[ndx].ToString());
                                    keepGoing = true;
                                }
                            } else {
                                if (innerMacro.Contains(".")) {
                                    var indexOfDot = innerMacro.IndexOf('.');
                                    ndx = GetIndex(innerMacro.Substring(0, indexOfDot));
                                    if (ndx >= 0) {
                                        if (ndx < eachItems.Length) {
                                            innerMacro = innerMacro.Substring(indexOfDot + 1).Trim();

                                            var v = eachItems[ndx].SimpleEval2(innerMacro);
                                            if (v != null) {
                                                var r = v.ToString();
                                                value = value.Replace(outerMacro, r);
                                                keepGoing = true;
                                            }
                                        }
                                    }
                                }
                            }
                        } catch {
                            // meh. screw em'
                        }
                    }

                    if (replacement != null) {
                        value = value.Replace(outerMacro, replacement);
                        keepGoing = true;
                        break;
                    }
                }
            } while (keepGoing);
            return value;
        }

#if turn_on_preandpostproc_macros 
        private StringExtensions.GetMacroValueDelegate PreprocessProperty { get {
            return _map.RootMap.PreprocessProperty;
        }}
#endif

        public string ResolveMacrosInContext(string value, object[] items = null) {
#if turn_on_preandpostproc_macros 
            if (PreprocessProperty != null) {
                foreach (StringExtensions.GetMacroValueDelegate preprocess in PreprocessProperty.GetInvocationList()) {
                    value = preprocess(value);
                }
            }
#endif
            value = ProcessMacroInternal(value, items);

#if turn_on_preandpostproc_macros 
            if (Root.PostprocessProperty != null) {
                foreach (StringExtensions.GetMacroValueDelegate postprocess in Root.PostprocessProperty.GetInvocationList()) {
                    value = postprocess(value);
                }
            }
#endif
            return value;
        }

        public IEnumerable<string> TryGetRValueInContext(string property) {
            var p = GetProperty(property);
            if (p != null) {
                return p.Values;
            }
            return null;
        }

        public string ResolveAlias(string aliasName) {
            if (_aliases.IsValueCreated) {
                foreach (var aliases in _aliases.Value.Where(aliases => aliases.ContainsKey(aliasName))) {
                    return aliases[aliasName];
                }
            }

            // return the original alias name if there isn't a match.
            if (ParentView != null) {
                return ParentView.ResolveAlias(aliasName);
            }
            return aliasName;
        }

        protected View(Map instance) {
            _map = instance;
            _map.GetMacroValue += ResolveMacro;

            _resolveValue = () => {
                // is there a property, and can it take a value?
                if (HasProperty && _map is ICanSetBackingValues) {
                    // prefer those who can take a collection
                    AggregatePropertyNode.SetResults(map as ICanSetBackingValues);
                } else {
                    // but single values are good too.
                    if (HasProperty && _map is ICanSetBackingValue) {
                        AggregatePropertyNode.SetResult(map as ICanSetBackingValue);
                    }
                }
                _map.Active = true;
                // regardless, never call this again...
                _resolveValue = () => {};
            };
        }

        protected View(Map instance, INode node)
            : this(instance) {
            if (node is PropertyNode) {
                AggregatePropertyNode.AddRange(node as PropertyNode);
            }
            if (node is ObjectNode) {
                _aliases.Value.Add((node as ObjectNode).Aliases.Value);
            }
            if (node.Metadata.IsValueCreated) {
                _metadata.Value.Add(node.Metadata.Value);
            }
            if (node is PropertySheet) {
                foreach (var i in (node as PropertySheet).AllImportedSheets) {
                    if (i.Metadata.IsValueCreated) {
                        _metadata.Value.Add(i.Metadata.Value);
                    }
                    _aliases.Value.Add(i.Aliases.Value);
                }
            }
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

        public static implicit operator string(View v) {
            return v.Value;
        }

        public static implicit operator string[](View v) {
            return v.Values.ToArray();
        }

        internal View(Selector selector, INode node)
            : this(Unroll(selector, node), node) {
        }

        internal View GetChild(Selector selector) {
            if (selector.IsCompound) {
                var name = selector.Prefix.Name;
                if (map.ContainsKey(name)) {
                    return map[name].GetChild(selector.Suffix);
                }
                return null;
            }

            if (selector.HasParameter) {
                return map.ContainsKey(selector.Name) ? map[selector.Name].GetElement(selector.Parameter) : null;
            }

            return GetProperty(selector.Name);
        }

        public IEnumerable<string> PropertyNames {
            get {
                return map.Keys;
            }
        }

        public int Count {
            get {
                return map.Keys.Select(each => each.ToInt32(-1)).Max();
            }
        }

        public View GetProperty(string propertyName) {
            // this falls back to case insensitive matches if th property didn't exist.
            return map.ContainsKey(propertyName) ? map[propertyName] : (map.Keys.Where(each => each.Equals(propertyName, StringComparison.InvariantCultureIgnoreCase)).Select(i => map[i])).FirstOrDefault();
        }

        public View GetElement(string elementName) {
            var child = map as IElements;
            if (child != null && child.ElementDictionary.ContainsKey(elementName)) {
                return child.ElementDictionary[elementName];
            }
            return null;
        }

        public IEnumerable<string> Values {
            get {
                if (!(_map is IHasValueFromBackingStorage)) {
                    // if we can't get the value, the propertynode is the only choice.
                    return HasProperty ? AggregatePropertyNode.Values : new string[0];
                }
                _resolveValue(); // push the value to the backing object if neccesary first
                return (map as IHasValueFromBackingStorage).Values;
            }
        }

        public string Value {
            get {
                if (!(_map is IHasValueFromBackingStorage)) {
                    // if we can't get the value, the propertynode is the only choice.
                    return HasProperty ? AggregatePropertyNode.Value : string.Empty;
                }
                _resolveValue(); // push the value to the backing object if neccesary first
                return (map as IHasValueFromBackingStorage).Value;
            }
        }

        internal void AddChildRoute(IEnumerable<ToRoute> routes) {
            _map.AddChildren(routes);
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
            // good for accessing child dictionary members.
            var index = indexes.Select(each => each.ToString()).Aggregate((current, each) => current + ", " + each).Trim(' ', ',');

            var ndx = index.ToInt32(-1);
            if (ndx > -1) {
                result = GetProperty(index);
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
            // returns a child reference or the value of a child if it is a property
            var child = GetProperty(binder.Name);

            if (child == null) {
                // result = null;
                // return false;
                Console.WriteLine("object doesn't have child [{0}] -- returning empty string", binder.Name);
                result = string.Empty;
                return true;
            }

            result = child;
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
                var x = Value;
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
    }

    internal class View<TParent> : View {
        public View(string memberName, RouteDelegate<TParent> route, params ToRoute[] childRoutes)
            : base(Unroll(memberName, (member) => new ObjectMap<TParent>(member, route, childRoutes))) {
        }

        internal View(PropertySheet rootNode, Route<TParent> backingObjectAccessor)
            : base(new ObjectMap<TParent>("ROOT", p => backingObjectAccessor, null), rootNode) {
            // used for the propertysheet itself.
        }

        internal View(string memberName, ValueDelegate<TParent> route, params ToRoute[] childRoutes)
            : base(Unroll(memberName, (member) => new ValueMap<TParent>(member, route, childRoutes))) {
        }

        internal View(string memberName, ListDelegate<TParent> route, params ToRoute[] childRoutes)
            : base(Unroll(memberName, (member) => new ListMap<TParent>(member, route, childRoutes))) {
        }

        internal View(string memberName, EnumerableDelegate<TParent> route, params ToRoute[] childRoutes)
            : base(Unroll(memberName, (member) => new EnumerableMap<TParent>(member, route, childRoutes))) {
        }
    }

    internal class View<TParent, TKey, TVal> : View {
        public View(string memberName, DictionaryDelegate<TParent, TKey, TVal> route, params ToRoute[] childRoutes)
            : base(Unroll(memberName, (member) => new DictionaryMap<TParent, TKey, TVal>(member, route, null) {
                childInitializers = childRoutes
            })) {
            // childRoutes are to be used as initializers for the children, not for the dictionary itself.
        }

        public View(string memberName, DictionaryDelegate<TParent, TKey, TVal> route, Func<string, string> keyExchanger, params ToRoute[] childRoutes)
            : base(Unroll(memberName, (member) => new DictionaryMap<TParent, TKey, TVal>(member, route, keyExchanger, null) {
                childInitializers = childRoutes
            })) {
            // childRoutes are to be used as initializers for the children, not for the dictionary itself.
        }
    }

    // ReSharper restore PossibleNullReferenceException
}