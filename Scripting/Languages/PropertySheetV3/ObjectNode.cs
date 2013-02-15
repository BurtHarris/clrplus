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

namespace ClrPlus.Scripting.Languages.PropertySheetV3 {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;
    using RValue;

    public class ObjectNode : XDictionary<Selector, INode>, INode {
        private static readonly Regex _macro = new Regex(@"(\$\{(.*?)\})");
        private readonly Lazy<XDictionary<string, Alias>> _aliases = new Lazy<XDictionary<string, Alias>>(() => new XDictionary<string, Alias>());
        private readonly Lazy<XDictionary<string, IValue>> _metadata = new Lazy<XDictionary<string, IValue>>(() => new XDictionary<string, IValue>());

        public IDictionary<Selector, ObjectNode> Children;
        public StringExtensions.GetMacroValueDelegate GetMacroValue;
        public IDictionary<Selector, PropertyNode> Properties;
        protected List<PropertySheet> _imports;
        protected View _view;

        private ObjectNode() {
            Properties = new DelegateDictionary<Selector, PropertyNode>(
                clear: Clear,
                remove: Remove,
                keys: () => Keys.Where(each => this[each] is PropertyNode).ToList(),
                set: (index, value) => this.AddOrSet(index, value),
                get: index => {
                    if (ContainsKey(index)) {
                        var v = this[index] as PropertyNode;
                        if (v == null) {
                            throw new ClrPlusException("Index {0} is not a PropertyNode, but an Object".format(index));
                        }
                        return v;
                    }
                    var r = new PropertyNode();
                    this.AddOrSet(index, r);
                    return r;
                });

            Children = new DelegateDictionary<Selector, ObjectNode>(
                clear: Clear,
                remove: Remove,
                keys: () => Keys.Where(each => this[each] is ObjectNode).ToList(),
                set: (index, value) => this.AddOrSet(index, value),
                get: index => {
                    if (ContainsKey(index)) {
                        var v = this[index] as ObjectNode;
                        if (v == null) {
                            throw new ClrPlusException("Index {0} is not a Object, but a PropertyNode".format(index));
                        }
                        return v;
                    }
                    var r = new ObjectNode(this, index);
                    this.AddOrSet(index, r);
                    return r;
                });
        }

        protected ObjectNode(object backingObject)
            : this(backingObject, null) {
        }

        protected ObjectNode(object backingObject, object routes)
            : this() {
            // only propertysheets get to have a view
            Root = this;
            Parent = null;
            _view = new Reference(backingObject ?? new object(), routes);
            _imports = new List<PropertySheet>();
        }

        internal ObjectNode(ObjectNode root) : this() {
            // this is for imported sheets that share the same root.
            Parent = null;
            Root = root;
            Selector = Selector.Empty;
            _imports = new List<PropertySheet>();
        }

        internal ObjectNode(ObjectNode parent, Selector selector) : this() {
            Parent = parent;
            Root = parent == null ? this : parent.Root;
            Selector = selector;
        }

        public Selector Selector {get; private set;}
        public ObjectNode Root {get; private set;}
        public ObjectNode Parent {get; private set;}
        public string Filename {get; private set;}

        public IEnumerable<PropertySheet> Imports {
            get {
                return _imports ?? Enumerable.Empty<PropertySheet>();
            }
        }

        public dynamic View {
            get {
                return _view;
            }
        }

        internal Reference CurrentView {
            get {
                if (Parent == null) {
                    if (Selector == null) {
                        // this is the root object, we can return the _view;
                        return Root._view as Reference;
                    }
                    // get the child of the root view.
                    return _view.GetChildView(Selector) as Reference;
                }
                return Parent.CurrentView.GetChildView(Selector) as Reference;
            }
        }

        protected string FullPath {
            get {
                if (Parent != null) {
                    return Parent.FullPath + "//" + Selector;
                }
                return Selector.ToString();
            }
        }

        public IDictionary<string, IValue> Metadata {
            get {
                return _metadata.Value;
            }
        }

        internal IEnumerable<string> TryGetRValueInContext(string property) {
            // this looks for the appropriate value given the property (searching macros, etc)
            // and returns a fully resolved value for that.
            var v = CurrentView;

            if (v.Keys.Contains(property)) {
                var prop = v.GetChildView(new Selector {
                    Name = property
                }) as Property;
                if (prop != null) {
                    return prop.Values;
                }
            }
            return null;
        }

        private int GetIndex(string innerMacro) {
            int ndx;
            if (!Int32.TryParse(innerMacro, out ndx)) {
                return innerMacro.Equals("each", StringComparison.CurrentCultureIgnoreCase) ? 0 : -1;
            }
            return ndx;
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
                    // var replacement = GetMacroValue(innerMacro);
                    string replacement = null;

                    // get the first responder.
                    foreach (StringExtensions.GetMacroValueDelegate del in GetMacroValue.GetInvocationList()) {
                        replacement = del(innerMacro);
                        if (replacement != null) {
                            break;
                        }
                    }

                    if (!eachItems.IsNullOrEmpty()) {
                        // try resolving it as an ${each.property} style.
                        // the element at the front is the 'this' value
                        // just trim off whatever is at the front up to and including the first dot.
                        try {
                            var ndx = GetIndex(innerMacro);

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

                                            var v = eachItems[ndx].SimpleEval(innerMacro);
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

        public string ResolveMacrosInContext(string text, object[] items = null) {
            GetMacroValue = GetMacroValue ?? ((s) => null);
            return ProcessMacroInternal(text, items);
        }

        public virtual void AddAlias(string aliasName, Selector aliasReference) {
            _aliases.Value.Add(aliasName, new Alias(aliasName, aliasReference));
        }

        public virtual Alias GetAlias(string aliasName) {
            throw new NotImplementedException();
        }

        public void AddRoute(Selector selector, Route route) {
            _view.AddRoute(selector, route);
        }

        public void AddRoute(Selector selector, Func<object> accessor) {
            _view.AddRoute(selector, accessor);
        }

        public void AddRoute(Selector selector, Func<View, object> accessor) {
            _view.AddRoute(selector, accessor);
        }

        public void AddRoute(Selector selector, Func<View, Selector, object> accessor) {
            _view.AddRoute(selector, accessor);
        }

        public void AddRoutes(object routes) {
            _view.AddRoutes(routes);
        }

        public void SaveFile(string filename) {
            var text = GetPropertySheetSource();
            File.WriteAllText(filename, text);
        }


        public string GetPropertySheetSource() {
            return "";
        }

        public Selector ResolveAliasesInPath(Selector path) {
            return path;
            //return null;
        }

        internal string GetAliasForPath(Selector path) {
            return null;
        }
    }
}