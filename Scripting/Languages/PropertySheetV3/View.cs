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
    using System.Dynamic;
    using System.Linq;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;

    public class View : DynamicObject {
        protected readonly XDictionary<Selector, Route> _routes = new XDictionary<Selector, Route>();

        protected View() {
        }

        protected void AddRoute(string selectorName, Route route) {
        }

        public View AddRoutes(object routes) {
            if (routes == null) {
                return this;
            }
            foreach (var element in routes.GetType().GetPersistableElements().Where(element => element.ActualType == typeof(Route))) {
                _routes.Add(new Selector {Name = element.Name}, element.GetValue(routes, null) as Route);
            }
            return this;
        }

        public virtual View AddAllRoutesFrom(IModel model) {
            foreach (var import in model.GetImportedPropertySheets()) {
                AddAllRoutesFrom(import);
            }
            return AddRoutesFrom(model);
        }

       
        public virtual View AddRoutesFrom(IItem item) {
            var node = item as INode;

            if(node == null) {
                return null;
            }

            foreach(var selector in node.Keys) {
                var childItem = node[selector];
                
                if(childItem is IProperty) {
                    return AddPropertyRoute(selector, childItem as IProperty);
                }

                var memberName = selector.Prefix;

                if(selector.IsCompound) {
                    var childSelector = selector.Suffix;
                    // if there is a '.' in the name, the object is really a child of one of the current node's children.
                    if(_routes.ContainsKey(memberName)) {
                        // already a child with that name ? 
                        _routes[memberName] = (context, selector1) => {
                            var result = _routes[memberName](context, selector1);
                            result.GetChildView(childSelector).AddRoutesFrom(childItem);
                            return result;
                        };
                    }
                    else {
                        // add a route for that child object where it sets 
                        _routes[memberName] = (context, selector1) => {
                            var result = GetChildView(memberName);
                            result.GetChildView(childSelector).AddRoutesFrom(childItem);
                            return result;
                        };
                    }
                }
                else {
                    if(_routes.ContainsKey(memberName)) {
                        _routes[memberName] = (context, selector1) => _routes[memberName](context, selector1).AddRoutesFrom(childItem);
                    }
                    else {
                        _routes[memberName] = (context, selector1) => context.GetChildView(selector1).AddRoutesFrom(childItem);
                    }
                }
            }
            return this;
        }

        protected virtual View AddPropertyRoute(Selector selector, IProperty childProperty) {
            var memberName = selector.Prefix;

            if(selector.IsCompound) {
                var childSelector = selector.Suffix;
                
                // if there is a '.' in the name, the object is really a child of one of the current node's children.
                if(_routes.ContainsKey(memberName)) {
                    // already a child with that name ? 
                    _routes[memberName] = (context, selector1) => {
                        var result = _routes[memberName](context, selector1);
                        result.GetChildView(childSelector).AddPropertyRoute(childSelector, childProperty);
                        return result;
                    };
                }
                else {
                    // add a route for that child object where it sets 
                    _routes[memberName] = (context, selector1) => {
                        var result = GetChildView(memberName);
                        result.GetChildView(childSelector).AddPropertyRoute(childSelector, childProperty);
                        return result;
                    };
                }
            }
            else {
                if(_routes.ContainsKey(memberName)) {
                    // the existing route should return a ViewProperty.

                    _routes[memberName] = (context, selector1) => {
                        var vp = (_routes[memberName](context, selector1) as ViewProperty<object>);
                        vp.Property.CopyFrom(childProperty);
                        return vp;
                    };
                }
                else {
                    // create a route to a view Property.
                    new ViewProperty<object>()
                    _routes[memberName] = (context, selector1) => context.GetChildView(selector1).AddRoutesFrom(childItem);
                }
            }
            return this;
        }

        public virtual View GetChildView(Selector selector) {
            throw new ClrPlusException("Only instances of ViewObject can have child objects");
        }
    }

    public class AggregateProperty : Property {
        public void CopyFrom(Property propertyInModel) {
            AddRange( propertyInModel );
        }
    }

    public class ViewProperty<T> : View {
        private readonly Func<T> _get;
        private readonly Action<T> _set;
        private AggregateProperty _property;

        public AggregateProperty Property {
            get {
                return _property ?? (_property = new AggregateProperty());
            }
        }

        public ViewProperty(Func<T> getAccessor, Action<T> setAccessor) {
            _get = getAccessor;
            _set = setAccessor;
        }

        public ViewProperty(Func<T> getAccessor)
            : this(getAccessor, v => {
            }) {
        }

        public ViewProperty(Action<T> setAccessor)
            : this(() => default(T), setAccessor) {
        }

        public ViewProperty(Func<T> getAccessor, Action<T> setAccessor, object routes)
            : this(getAccessor, setAccessor) {
            AddRoutes(routes);
        }

        public ViewProperty(Func<T> getAccessor, object routes)
            : this(getAccessor, v => {
            }, routes) {
        }

        public ViewProperty(Action<T> setAccessor, object routes)
            : this(() => default(T), setAccessor, routes) {
        }

        public T Value {
            get {
                // has the value already been set?
                // yes, return the cached value
                // no, 
                // does it have an RValue from the propsheet
                // yes, 
                //     get the value from the rvalue, 
                //     cache the value
                //     return the value.
                // no,
                //     get the value from the getter
                //     cache it
                //     return the value

                return default(T);
            }
        }
    }

    public class ViewObject : View {
        private Func<object> _backingObjectAccessor;
        private Dictionary<string, PersistablePropertyInformation> _backingObjectPropertyInfo;

        public ViewObject(Func<object> backingObjectAccessor) {
            _backingObjectAccessor = backingObjectAccessor;
        }

        public ViewObject(object backingObject) {
            _backingObjectAccessor = () => backingObject;
        }

        public ViewObject(Func<object> backingObjectAccessor, object routes) {
            _backingObjectAccessor = backingObjectAccessor;
            if(routes != null) {
                AddRoutes(routes);
            }
        }

        public ViewObject(object backingObject, object routes) {
            _backingObjectAccessor = () => backingObject;
            if(routes != null) {
                AddRoutes(routes);
            }
        }

        public override View GetChildView(Selector selector) {
            // if there is an exact selector match:
            if(_routes.ContainsKey(selector)) {
                return _routes[selector](this, selector);
            }

            // otherwise, if we've got a parameter, try it without the parameter. 
            // HMM: if there isn't a match for this, it's gonna return just the base object you know...
            if(!string.IsNullOrEmpty(selector.Parameter)) {
                var parameterless = new Selector {
                    Name = selector.Name
                };
                if(_routes.ContainsKey(parameterless)) {
                    var result = _routes[parameterless](this, selector);
                    if(result != null) {
                        _routes[selector] = (context, sel) => result;
                        return result;
                    }
                }
            }

            // if there isn't anything there, see if there is a @default rule.
            if(_routes.ContainsKey(new Selector { Name = "default" })) {
                var result = _routes[selector](this, selector);
                if(result != null) {
                    _routes[selector] = (context, sel) => result;
                    return result;
                }
            }

            var backingObject = _backingObjectAccessor();
            // if there isn't a match at this point, I guess we should check the backing object for a child with that name.
            if(_backingObjectPropertyInfo == null) {
                _backingObjectPropertyInfo = backingObject.GetType().GetPersistableElements().ToDictionary(each => each.Name, each => each);
                _backingObjectAccessor = () => backingObject; // reduce the accessor to the result.
            }

            if(_backingObjectPropertyInfo.ContainsKey(selector.Name)) {
                var result = new ViewObject(_backingObjectPropertyInfo[selector.Name].GetValue(backingObject, null));
                _routes[selector] = (context, sel) => result;
                return result;
            }

            // if we *still* haven't found anything, I guess we'll make a new virtual view for that 
            var anonResult = new ViewObject(new object());
            _routes[selector] = (context, sel) => anonResult;
            return anonResult;
        }


        public IEnumerable<string> Keys {
            get {
                return null;
            }
        }

        public IEnumerable<Selector> GetChildSelectors() {
            return null;
        }

        public IEnumerable<Selector> GetPropertySelectors() {
            return null;
        }

        public IEnumerable<string> GetChildNames() {
            return GetChildSelectors().Select(each => each.Name).Distinct();
        }

        public IEnumerable<string> GetPropertyNames() {
            return GetPropertySelectors().Select(each => each.Name).Distinct();
        }

        public void CopyToBackingObject() {
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result) {
            return base.TryGetIndex(binder, indexes, out result);
        }
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            return base.TryGetMember(binder, out result);
        }
        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value) {
            return base.TrySetIndex(binder, indexes, value);
        }
        public override bool TrySetMember(SetMemberBinder binder, object value) {
            return base.TrySetMember(binder, value);
        }

#if false
        public override IEnumerable<string> GetDynamicMemberNames() {
            return base.GetDynamicMemberNames();
        }

        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object result) {
            return base.TryBinaryOperation(binder, arg, out result);
        }
        public override bool TryDeleteIndex(DeleteIndexBinder binder, object[] indexes) {
            return base.TryDeleteIndex(binder, indexes);
        }
        public override bool TryCreateInstance(CreateInstanceBinder binder, object[] args, out object result) {
            return base.TryCreateInstance(binder, args, out result);
        }
        public override bool TryDeleteMember(DeleteMemberBinder binder) {
            return base.TryDeleteMember(binder);
        }
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            return base.TryInvokeMember(binder, args, out result);
        }
        public override bool TryConvert(ConvertBinder binder, out object result) {
            return base.TryConvert(binder, out result);
        }
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result) {
            return base.TryInvoke(binder, args, out result);
        }
        public override bool TryUnaryOperation(UnaryOperationBinder binder, out object result) {
            return base.TryUnaryOperation(binder, out result);
        }
#endif
    }
#if didnt_use
    public class ViewNode : AbstractDictionary<Selector, INode>, INode {
        public IModel Root { get; private set; }
        public INode Parent { get; private set; }
        public Selector Selector { get; private set; }

        public override bool Remove(Selector key) {
            // modifing nodes not permitted here.
            throw new NotImplementedException();
        }

        public override bool IsReadOnly {
            get {
                return true;
            }
        }

        public override INode this[Selector key] {
            get {
                // look up the item from the root.
                // TODO: implement

                return null;
            }
            set {
                // setting nodes not permitted here.
                throw new NotImplementedException();
            }
        }


        private ICollection<Selector> _keysCache;
        public override ICollection<Selector> Keys {
            get {
                // generate the list of selectors available here. 
                // TODO: implement
                if(_keysCache == null) {
                    var importedSheets = ((IDictionary<string, IModel>)Root.Imports).Values;

                }
                return _keysCache;
            }
        }

        public ViewNode(IModel root) {
            Root = root;
            Selector = null;
            Parent = null;
        }

        protected ViewNode(IModel root, Selector selector, INode parent) {
            Root = root;
            Selector = selector;
            Parent = parent;
        }

        private bool IsRootViewNode {
            get {
                return Selector == null;
            }
        }

        #region Metadata
        private IDictionary<string, RValue> _metadata;
        public IDictionary<string, RValue> Metadata { get { return _metadata ?? (_metadata = new ReadOnlyDelegateDictionary<string, RValue>(MetadataKeys, MetadataGet)); } }

        private RValue MetadataGet(string s) {
            // TODO: implement
            throw new NotImplementedException();
        }

        private ICollection<string> MetadataKeys() {
            // TODO: implement
            throw new NotImplementedException();
        }
        #endregion

        private IDictionary<Selector, IProperty> _properties;
        public IDictionary<Selector, IProperty> Properties { get { return _properties ?? (_properties = new ReadOnlyDelegateDictionary<Selector, IProperty>(PropertyKeys, PropertyGet)); } }

        private IProperty PropertyGet(Selector s) {
            // TODO: implement
            throw new NotImplementedException();
        }

        private ICollection<Selector> PropertyKeys() {
            // TODO: implement
            throw new NotImplementedException();
        }



        public void AddAlias(string aliasName, Selector aliasReference) {
            // TODO: implement
            throw new NotImplementedException();
        }

        public Alias GetAlias(string aliasName) {
            // TODO: implement
            throw new NotImplementedException();
        }
    }
#endif
}