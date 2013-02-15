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
    public static class ViewExtensions {
        // public static View AddChildRoutes(this object obj, object route) {
        //return AddChildRoutes(() => obj, route);
        // }

#if false
        public static View AddChildRoutes<T>(this T obj, object route) {
            return AddChildRoutes(() => obj, route);
        }

        public static View AddChildRoutes<T>(this Func<T> objAccessor, object route) {
            View result = null;
            switch(typeof(T).GetPersistableInfo().PersistableCategory) {
                case PersistableCategory.Nullable:
                case PersistableCategory.String:
                case PersistableCategory.Enumeration:
                case PersistableCategory.Parseable:
                case PersistableCategory.Array:
                case PersistableCategory.Enumerable:
                    result = (View)Property.Create(typeof(T), () => {
                        var r = objAccessor();
                        result.AddRoutes(route);
                        return r;
                    }
                        , (v) => { });
                    _AddRoute(new Selector { Name = e.Name }, (context, selector) => view);
                    continue;

                case PersistableCategory.Other:
                    var viewobj = new Reference(objAccessor);
                    _AddRoute(new Selector { Name = e.Name }, (context, selector) => viewobj);
                    continue;
            }
        }
#endif
    }
}