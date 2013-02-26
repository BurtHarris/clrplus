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

namespace ClrPlus.Core.Extensions {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Collections;

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NotPersistableAttribute : Attribute {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class PersistableAttribute : Attribute {
        public string Name {get; set;}
        public Type SerializeAsType {get; set;}
        public Type DeserializeAsType {get; set;}
    }

    [AttributeUsage(AttributeTargets.Interface)]
    public class ImplementedByAttribute : Attribute {
        public Type[] Types;
        public string[] TypeNames {get {
            return Types.Select(each => each.FullName).ToArray();
        } set {
            Types = value.Select(Type.GetType).ToArray();
        }}
    }

    public enum PersistableCategory {
        Parseable,
        Array,
        Enumerable,
        Dictionary,
        Nullable,
        String,
        Enumeration,
        Other
    }

    public class PersistableInfo {
        private static Type IteratorType = Type.GetType("System.Linq.Enumerable.Iterator<>");

        internal PersistableInfo(Type type) {
            Type = type;

            if (type.IsEnum) {
                PersistableCategory = PersistableCategory.Enumeration;
                return;
            }

            if (type == typeof (string)) {
                PersistableCategory = PersistableCategory.String;
                return;
            }

            if (typeof (IDictionary).IsAssignableFrom(type)) {
                PersistableCategory = PersistableCategory.Dictionary;
                if (type.IsGenericType) {
                    var genericArguments = type.GetGenericArguments();
                    DictionaryKeyType = genericArguments[0];
                    DictionaryValueType = genericArguments[1];
                } else {
                    DictionaryKeyType = typeof (object);
                    DictionaryValueType = typeof (object);
                }
                return;
            }

            if (type.IsArray) {
                // an array of soemthing.
                PersistableCategory = PersistableCategory.Array;
                ElementType = type.GetElementType();
                return;
            }

            if (typeof (IEnumerable).IsAssignableFrom(type)) {
                PersistableCategory = PersistableCategory.Enumerable;
                if (type.IsGenericType) {
                    ElementType = type.GetGenericArguments().Last();
                    /* 
                     * scratch code to identify if we're looking at an iterator or somethig.  Don't think we need to get weird tho'
                     * 
                     * var et = type.GetGenericArguments().Last();
                    var It = type.IsAssignableFrom((Type)Activator.CreateInstance(IteratorType.MakeGenericType(type.GetGenericArguments().Last()), true));

                    var t = type;
                    Type[] genericArguments;

                    do {
                        if (t == typeof(object) || t == null) {
                            throw new ClrPlusException("Critical Failure in PersistableInfo/Enumerator [1].");
                        }
                        genericArguments = t.GetGenericArguments();
                        if (genericArguments.Length == 0) {
                            throw new ClrPlusException("Critical Failure in PersistableInfo/Enumerator [2].");
                        }
                        t = t.BaseType;
                    } while (genericArguments.Length > 1);
                    ElementType = genericArguments[0];
                     */
                } else {
                    ElementType = typeof (object);
                }
                return;
            }

            if (type.IsGenericType) {
                // better be Nullable
                switch (type.Name.Split('`')[0]) {
                    case "Nullable":
                        PersistableCategory = PersistableCategory.Nullable;
                        NullableType = type.GetGenericArguments()[0];
                        return;
                }
            }

            if (type.IsParsable()) {
                PersistableCategory = PersistableCategory.Parseable;
                return;
            }

            PersistableCategory = PersistableCategory.Other;
        }

        public PersistableCategory PersistableCategory {get; set;}
        public Type Type {get; set;}
        public Type ElementType {get; set;}
        public Type DictionaryKeyType {get; set;}
        public Type DictionaryValueType {get; set;}
        public Type NullableType {get; set;}
    }

   

    public static class AssociativeLazyCache {
        private static class C<TKey, TValue> {
            internal static readonly IDictionary<object, IDictionary<TKey, TValue>> Cache = new XDictionary<object, IDictionary<TKey, TValue>>();
        }

        public static TValue Get<TKey, TValue>(TKey key, Func<TValue> valueFunc) {
            return Get(null, key, valueFunc);
        }

        public static TValue Get<TKey, TValue>( Object scope, TKey key, Func<TValue> valueFunc) {
            var cache = C<TKey, TValue>.Cache;
            IDictionary<TKey, TValue> inner;

            lock (cache) {
                if (!cache.ContainsKey(scope)) {
                    cache.Add(scope, (inner = new XDictionary<TKey, TValue>()));
                } else {
                    inner = cache[scope];
                }
            }

            lock (inner) {
                if (!inner.ContainsKey(key)) {
                    inner[key] = valueFunc();
                }
                return inner[key];
            }
        }
    }

    public class PersistablePropertyInformation {
        public string Name;
        public Type SerializeAsType;
        public Type DeserializeAsType;
        public Type ActualType;
        public Action<object, object, object[]> SetValue;
        public Func<object, object[], object> GetValue;
    }

    public static class TypeExtensions {
        private static readonly IDictionary<Type, MethodInfo> _tryParsers = new XDictionary<Type, MethodInfo>();
        private static readonly IDictionary<Type, ConstructorInfo> _tryStrings = new XDictionary<Type, ConstructorInfo>();
        private static readonly MethodInfo _castMethod = typeof (Enumerable).GetMethod("Cast");
        private static readonly MethodInfo _toArrayMethod = typeof (Enumerable).GetMethod("ToArray");
        private static readonly IDictionary<Type, MethodInfo> _castMethods = new XDictionary<Type, MethodInfo>();
        private static readonly IDictionary<Type, MethodInfo> _toArrayMethods = new XDictionary<Type, MethodInfo>();
        

        private static readonly XDictionary<Type, PersistableInfo> _piCache = new XDictionary<Type, PersistableInfo>();
        private static readonly XDictionary<Type, PersistablePropertyInformation[]> _ppiCache = new XDictionary<Type, PersistablePropertyInformation[]>();
        private static readonly XDictionary<Type, PersistablePropertyInformation[]> _readablePropertyCache = new XDictionary<Type, PersistablePropertyInformation[]>();

        public static PersistableInfo GetPersistableInfo(this Type t) {
            return _piCache.GetOrAdd(t, () => new PersistableInfo(t));
        }

        public static PersistablePropertyInformation[] GetReadableElements(this Type type) {
            return _readablePropertyCache.GetOrAdd(type, () =>
                (from each in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                 let persistableAttribute = each.GetCustomAttributes(typeof(PersistableAttribute), true).FirstOrDefault() as PersistableAttribute
                 where !each.GetCustomAttributes(typeof(NotPersistableAttribute), true).Any() &&
                         (each.IsPublic || persistableAttribute != null)
                 select new PersistablePropertyInformation {
                     SetValue = null,
                     GetValue = (o, objects) => each.GetValue(o),
                     SerializeAsType = null,
                     DeserializeAsType = null,
                     ActualType = each.FieldType,
                     Name = each.Name
                 }).Union((from each in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                           let getMethodInfo = each.GetGetMethod(true)
                           let persistableAttribute = each.GetCustomAttributes(typeof(PersistableAttribute), true).FirstOrDefault() as PersistableAttribute
                           where
                               ((getMethodInfo != null) &&
                                   !each.GetCustomAttributes(typeof(NotPersistableAttribute), true).Any() &&
                                   each.GetGetMethod(true).IsPublic) ||
                                   persistableAttribute != null
                           select new PersistablePropertyInformation {
                               SetValue = null,
                               GetValue = getMethodInfo != null ? new Func<object, object[], object>(each.GetValue) : null,
                               SerializeAsType = null,
                               DeserializeAsType = null,
                               ActualType = each.PropertyType,
                               Name = (persistableAttribute != null ? persistableAttribute.Name : null) ?? each.Name
                           })).ToArray()
                );
        }

        public static PersistablePropertyInformation[] GetPersistableElements(this Type type) {
            return _ppiCache.GetOrAdd(type, () =>
                (from each in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    let persistableAttribute = each.GetCustomAttributes(typeof (PersistableAttribute), true).FirstOrDefault() as PersistableAttribute
                    where
                        !each.IsInitOnly && !each.GetCustomAttributes(typeof (NotPersistableAttribute), true).Any() &&
                            (each.IsPublic || persistableAttribute != null)
                    select new PersistablePropertyInformation {
                        SetValue = (o, o1, arg3) => each.SetValue(o, o1),
                        GetValue = (o, objects) => each.GetValue(o),
                        SerializeAsType = (persistableAttribute != null ? persistableAttribute.SerializeAsType : null) ?? each.FieldType,
                        DeserializeAsType = (persistableAttribute != null ? persistableAttribute.DeserializeAsType : null) ?? each.FieldType,
                        ActualType = each.FieldType,
                        Name = (persistableAttribute != null ? persistableAttribute.Name : null) ?? each.Name
                    }).Union((from each in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        let setMethodInfo = each.GetSetMethod(true)
                        let getMethodInfo = each.GetGetMethod(true)
                        let persistableAttribute = each.GetCustomAttributes(typeof (PersistableAttribute), true).FirstOrDefault() as PersistableAttribute
                        where
                            ((setMethodInfo != null && getMethodInfo != null) &&
                                !each.GetCustomAttributes(typeof (NotPersistableAttribute), true).Any() &&
                                (each.GetSetMethod(true).IsPublic && each.GetGetMethod(true).IsPublic)) ||
                                persistableAttribute != null
                        select new PersistablePropertyInformation {
                            SetValue = setMethodInfo != null ? new Action<object, object, object[]>(each.SetValue) : null,
                            GetValue = getMethodInfo != null ? new Func<object, object[], object>(each.GetValue) : null,
                            SerializeAsType = (persistableAttribute != null ? persistableAttribute.SerializeAsType : null) ?? each.PropertyType,
                            DeserializeAsType = (persistableAttribute != null ? persistableAttribute.DeserializeAsType : null) ?? each.PropertyType,
                            ActualType = each.PropertyType,
                            Name = (persistableAttribute != null ? persistableAttribute.Name : null) ?? each.Name
                        })).ToArray()
                );
        }

        

        public static object ToArrayOfType(this IEnumerable<object> enumerable, Type collectionType) {
            return _toArrayMethods.GetOrAdd(collectionType, () => _toArrayMethod.MakeGenericMethod(collectionType))
                                 .Invoke(null, new[] {
                                     enumerable.CastToIEnumerableOfType(collectionType)
                                 });
        }

        public static object CastToIEnumerableOfType(this IEnumerable<object> enumerable, Type collectionType) {
            lock (collectionType) {
                return _castMethods.GetOrAdd(collectionType, () => _castMethod.MakeGenericMethod(collectionType)).Invoke(null, new object[] {
                    enumerable
                });
            }
        }

      

        private static MethodInfo GetTryParse(Type parsableType) {
            lock (_tryParsers) {
                if (!_tryParsers.ContainsKey(parsableType)) {
                    if (parsableType.IsPrimitive || parsableType.IsValueType || parsableType.GetConstructor(new Type[] {
                    }) != null) {
                        _tryParsers.Add(parsableType, parsableType.GetMethod("TryParse", new[] {
                            typeof (string), parsableType.MakeByRefType()
                        }));
                    } else {
                        // if they don't have a default constructor, 
                        // it's not going to be 'parsable'
                        _tryParsers.Add(parsableType, null);
                    }
                }
            }
            return _tryParsers[parsableType];
        }

        private static ConstructorInfo GetStringConstructor(Type parsableType) {
            lock (_tryStrings) {
                if (!_tryStrings.ContainsKey(parsableType)) {
                    _tryStrings.Add(parsableType, parsableType.GetConstructor(new[] {
                        typeof (string)
                    }));
                }
            }
            return _tryStrings[parsableType];
        }

        public static bool IsConstructableFromString(this Type stringableType) {
            return GetStringConstructor(stringableType) != null;
        }

        public static bool IsParsable(this Type parsableType) {
            if (parsableType.IsDictionary() || parsableType.IsArray) {
                return false;
            }
            return parsableType.IsEnum || parsableType == typeof (string) || GetTryParse(parsableType) != null || IsConstructableFromString(parsableType);
        }

        public static object ParseString(this Type parsableType, string value) {
            if (parsableType.IsEnum) {
                return Enum.Parse(parsableType, value);
            }

            if (parsableType == typeof (string)) {
                return value;
            }

            var tryParse = GetTryParse(parsableType);

            if (tryParse != null) {
                if (!string.IsNullOrEmpty(value)) {
                    var pz = new[] {
                        value, Activator.CreateInstance(parsableType)
                    };

                    // returns the default value if it's not successful.
                    tryParse.Invoke(null, pz);
                    return pz[1];
                }
                return Activator.CreateInstance(parsableType);
            }

            return string.IsNullOrEmpty(value) ? null : GetStringConstructor(parsableType).Invoke(new object[] {
                value
            });
        }

        public static bool IsDictionary(this Type dictionaryType) {
            return typeof (IDictionary).IsAssignableFrom(dictionaryType);
        }

        public static bool IsIEnumerable(this Type ienumerableType) {
            return typeof (IEnumerable).IsAssignableFrom(ienumerableType);
        }
    }


}