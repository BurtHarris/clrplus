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
    using System.Dynamic;
    using System.Linq;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;

    public delegate View Route(View context, Selector selector);

    public class View : DynamicObject {
        protected readonly XDictionary<Selector, Route> Routes = new XDictionary<Selector, Route>();

        protected View() {
        }

        public View AddRoute(Selector selector, Route route) {
            Routes.Add(selector, route);
            return this;
        }

        public View AddRoute(Selector selector, Func<object> accessor) {
            Routes.Add(selector, (c, s) => new Reference(accessor));
            return this;
        }

        public View AddRoute(Selector selector, Func<View, object> accessor) {
            Routes.Add(selector, (c, s) => new Reference(()=>accessor(c)));
            return this;
        }

        public View AddRoute(Selector selector, Func<View, Selector, object> accessor) {
            Routes.Add(selector, (c, s) => new Reference(() => accessor(c,s)));
            return this;
        }

        public View AddRoutes(object routes) {
            if (routes == null) {
                return this;
            }
            var elements = routes.GetType().GetReadableElements().ToArray();

            foreach (var element in elements) {
                var e = element;

                if (e.ActualType == typeof (Route)) {
                    AddRoute(new Selector {
                        Name = e.Name
                    }, e.GetValue(routes, null) as Route);
                    continue;
                }

                if (e.ActualType.IsAssignableFrom(typeof (View))) {
                    AddRoute(new Selector {
                        Name = e.Name
                    }, (c, s) => (View)e.GetValue(routes, null));
                    continue;
                }

                switch (e.ActualType.GetPersistableInfo().PersistableCategory) {
                    case PersistableCategory.Nullable:
                    case PersistableCategory.String:
                    case PersistableCategory.Enumeration:
                    case PersistableCategory.Parseable:
                    case PersistableCategory.Array:
                    case PersistableCategory.Enumerable:
                        var view = (View)Property.Create(e.ActualType, () => e.GetValue(routes, null), v => {});
                        AddRoute(new Selector {
                            Name = e.Name
                        }, (context, selector) => view);
                        continue;

                    case PersistableCategory.Other:
                        var viewobj = new Reference(() => e.GetValue(routes, null));
                        AddRoute(new Selector {
                            Name = e.Name
                        }, (context, selector) => viewobj);
                        continue;
                }
            }
            return this;
        }

        internal virtual View BuildRoutesFromNodes(PropertySheet propertySheet) {
            foreach (var import in propertySheet.Imports) {
                BuildRoutesFromNodes(import);
            }
            return AddRoutesFromNode(propertySheet);
        }

        protected virtual View AddRoutesFromNode(INode node) {
            var propertySheetNode = node as ObjectNode;

            if (propertySheetNode == null) {
                return null;
            }

            foreach (var selector in propertySheetNode.Keys) {
                var childItem = propertySheetNode[selector];

                if (childItem is PropertyNode) {
                    AddPropertyRoute(selector, childItem as PropertyNode);
                    continue;
                }
                var memberName = selector.Prefix;

                if (selector.IsCompound) {
                    var childSelector = selector.Suffix;
                    // if there is a '.' in the name, the object is really a child of one of the current node's children.
                    if (Routes.ContainsKey(memberName)) {
                        // already a child with that name ? 
                        var currentRoute = Routes[memberName];

                        Routes[memberName] = (context, selector1) => {
                            var result = currentRoute(context, selector1);
                            result.GetChildView(childSelector).AddRoutesFromNode(childItem);
                            return result;
                        };
                    } else {
                        // add a route for that child object where it sets 
                        Routes[memberName] = (context, selector1) => {
                            var result = LookupChildObject(memberName);
                            result.GetChildView(childSelector).AddRoutesFromNode(childItem);
                            return result;
                        };
                    }
                } else {
                    if (Routes.ContainsKey(memberName)) {
                        var currentRoute = Routes[memberName];
                        Routes[memberName] = (context, selector1) => currentRoute(context, selector1).AddRoutesFromNode(childItem);
                    } else {
                        Routes[memberName] = (context, selector1) => context.LookupChildObject(selector1).AddRoutesFromNode(childItem);
                    }
                }
            }
            return this;
        }

        protected virtual View AddPropertyRoute(Selector selector, PropertyNode childPropertyNode) {
            var memberName = selector.Prefix;

            if (selector.IsCompound) {
                var childSelector = selector.Suffix;

                // if there is a '.' in the name, the object is really a child of one of the current node's children.
                if (Routes.ContainsKey(memberName)) {
                    // already a child with that name ? 
                    var currentRoute = Routes[memberName];

                    Routes[memberName] = (context, selector1) => {
                        var result = currentRoute(context, selector1);
                        result.GetChildView(childSelector).AddPropertyRoute(childSelector, childPropertyNode);
                        return result;
                    };
                } else {
                    // add a route for that child object where it sets 
                    Routes[memberName] = (context, selector1) => {
                        var result = LookupChildObject(memberName);
                        result.GetChildView(childSelector).AddPropertyRoute(childSelector, childPropertyNode);
                        return result;
                    };
                }
            } else {
                if (Routes.ContainsKey(memberName)) {
                    // the existing route should return a Property.
                    var currentRoute = Routes[memberName];
                    Routes[memberName] = (context, selector1) => {
                        var vp = (currentRoute(context, selector1) as Property);
                        if (vp == null) {
                            throw new ClrPlusException("PropertyNode came back as an object!");
                        }
                        vp.PropertyNode.AddRange(childPropertyNode);
                        return vp;
                    };
                } else {
                    // create a route to a view PropertyNode.
                    Routes[memberName] = (context, selector1) => {
                        var vp = context.LookupChildProperty(selector1);
                        vp.PropertyNode.AddRange(childPropertyNode);
                        return vp;
                    };
                }
            }
            return this;
        }

        public virtual View GetChildView(Selector selector) {
            throw new ClrPlusException("Only instances of Reference can have child objects");
        }

        public virtual View LookupChildObject(Selector selector) {
            throw new ClrPlusException("Only instances of Reference can have child objects");
        }

        public virtual Property LookupChildProperty(Selector selector) {
            throw new ClrPlusException("Only instances of Reference can have child properties");
        }

        public virtual void CopyToBackingObject() {
        }
    }
}