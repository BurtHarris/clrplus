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

namespace ClrPlus.Remoting {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Core.Collections;
    using Core.Extensions;

    public delegate object TypeInstantiator(UrlEncodedMessage message, string key, Type t);

    public class TypeCreator {
        private static readonly IDictionary<Type, Func<object, object>> _opImplicitMethods = new XDictionary<Type, Func<object, object>>();
        private readonly IDictionary<Type, TypeInstantiator> _instantiators = new XDictionary<Type, TypeInstantiator>();
        private readonly IDictionary<Type, Type> _substitutions = new XDictionary<Type, Type>();

        public void AddTypeInstantiator<T>(TypeInstantiator typeInstantiator) {
            _instantiators.Add(typeof(T), typeInstantiator);
        }

        public void AddTypeSubstitution(Type sourceType, Type targetType) {
            _substitutions.Add(sourceType, targetType);
        }

        public object CreateInstance( Type type) {
            return Activator.CreateInstance(_substitutions[type] ?? type, true);
        }

        public Object CreateObject(UrlEncodedMessage message, string key, Type targetType) {
            var instantiator = _instantiators[targetType];
            return instantiator != null ? instantiator(message, key, targetType) : CreateInstance(targetType);
        }

        public PersistableInfo GetPersistableInfo(Type argType) {
            return (_substitutions[argType] ?? argType).GetPersistableInfo();
        }

        private Func<object, object> GetOpImplicit(Type sourceType, Type destinationType) {
            lock(_opImplicitMethods) {
                if(!_opImplicitMethods.ContainsKey(sourceType)) {
                    var opImplicit =
                        (from method in sourceType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                         where method.Name == "op_Implicit" && method.ReturnType == destinationType && method.GetParameters()[0].ParameterType == sourceType
                         select method).Union(
                                (from method in destinationType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                 where method.Name == "op_Implicit" && method.ReturnType == destinationType && method.GetParameters()[0].ParameterType == sourceType
                                 select method
                                    )).FirstOrDefault();

                    if(opImplicit != null) {
                        _opImplicitMethods.Add(sourceType, obj => opImplicit.Invoke(null, new[] {
                            obj
                        }));
                    }
                    else {
                        // let's 'try harder'
                        var result =
                            destinationType.GetCustomAttributes(typeof(ImplementedByAttribute), false).Select(i => i as ImplementedByAttribute).SelectMany(attribute => attribute.Types).Select(target => GetOpImplicit(sourceType, target)).FirstOrDefault();
                        if(result == null) {
                            // still not found one? is it an IEnumerable conversion?
                            if(sourceType.IsIEnumerable() && destinationType.IsIEnumerable()) {
                                var sourceElementType = sourceType.GetPersistableInfo().ElementType;
                                var destElementType = destinationType.GetPersistableInfo().ElementType;
                                var elemConversion = GetOpImplicit(sourceElementType, destElementType);
                                if(elemConversion != null) {
                                    // it looks like we can translate the elements of the collection.
                                    if(destinationType.IsArray) {
                                        // get an array of converted values.
                                        result = obj => ((IEnumerable<object>)(obj)).Select(each => ImplicitlyConvert(each,destElementType)).ToArrayOfType(destElementType);
                                    }
                                    else if(destinationType.Name.StartsWith("IEnumerable")) {
                                        // just get an IEnumerable of the converted elements
                                        result = obj => ((IEnumerable<object>)(obj)).Select(each => ImplicitlyConvert(each, destElementType)).CastToIEnumerableOfType(destElementType);
                                    }
                                    else {
                                        // create the target collection type, and stuff the values into that.
                                        result = obj => {
                                            var v = (IList)CreateInstance(destinationType);
                                            foreach(object each in ((IEnumerable<object>)(obj))) {
                                                v.Add(ImplicitlyConvert(each,destElementType));
                                            }
                                            return v;
                                        };
                                    }
                                }
                            }
                        }
                        _opImplicitMethods.Add(sourceType, result);
                    }
                }
                return _opImplicitMethods[sourceType];
            }
        }

        public object ImplicitlyConvert(object obj, Type destinationType) {
            if(obj == null) {
                return null;
            }
            if(destinationType == typeof(string)) {
                return obj.ToString();
            }

            var opImplicit = GetOpImplicit(obj.GetType(), destinationType);
            if(opImplicit != null) {
                // return opImplicit.Invoke(null, new[] {obj});
                return opImplicit(obj);
            }
            return obj;
        }

        public bool ImplicitlyConvertsTo(Type type, Type destinationType) {
            if(type == destinationType) {
                return false;
            }
            if(typeof(string) == destinationType) {
                return true;
            }
            return GetOpImplicit(type, destinationType) != null;
        }
    }

    /// <summary
    ///     UrlEncodedMessages
    /// </summary>
    public class UrlEncodedMessage : IEnumerable<string> {

        private static TypeCreator _defaultTypeCreator = new TypeCreator();
        private TypeCreator _typeCreator;

        /// <summary>
        /// </summary>
        private static readonly char[] Query = new[] {
            '?'
        };

        /// <summary>
        /// </summary>
        private readonly char[] _separator = new[] {
            '&'
        };

        private readonly string _separatorString = "&";

        /// <summary>
        /// </summary>
        private static readonly char[] Equal = new[] {
            '='
        };

        /// <summary>
        /// </summary>
        public string Command;

        /// <summary>
        /// </summary>
        private readonly IDictionary<string, string> _data;

        private bool _storeTypeInformation;

        /// <summary>
        ///     Initializes a new instance of the <see cref="UrlEncodedMessage" /> class.
        /// </summary>
        /// <param name="rawMessage"> The raw message. </param>
        /// <param name="seperator"> </param>
        /// <param name="storeTypeInformation"> </param>
        /// <param name="typeCreator"> </param>
        /// <remarks>
        /// </remarks>
        public UrlEncodedMessage(string rawMessage = null, string seperator = "&", bool storeTypeInformation = false, TypeCreator typeCreator = null) {
            _separatorString = seperator;
            _separator = seperator.ToCharArray();
            _storeTypeInformation = storeTypeInformation;
            rawMessage = rawMessage ?? string.Empty;
            _typeCreator = typeCreator ?? _defaultTypeCreator;

            var parts = rawMessage.Split(Query, StringSplitOptions.RemoveEmptyEntries);
            switch (parts.Length) {
                case 0:
                    _data = new XDictionary<string, string>();
                    break;

                case 1:
                    if (rawMessage.IndexOf("=") > -1) {
                        Command = string.Empty;
                        _data = (parts.FirstOrDefault() ?? string.Empty).Split(_separator, StringSplitOptions.RemoveEmptyEntries).Select(
                            p => p.Split(Equal, StringSplitOptions.RemoveEmptyEntries))
                                                                        .ToXDictionary(s => s[0].UrlDecode(), s => s.Length > 1 ? s[1].UrlDecode() : String.Empty);
                        break;
                    }

                    Command = rawMessage;
                    _data = new XDictionary<string, string>();
                    break;

                default:
                    Command = parts.FirstOrDefault().UrlDecode();
                    // ReSharper disable PossibleNullReferenceException (the parts has two or more!)
                    _data = parts.Skip(1).FirstOrDefault().Split(_separator, StringSplitOptions.RemoveEmptyEntries).Select(
                        p => p.Split(Equal, StringSplitOptions.RemoveEmptyEntries))
                                 .ToXDictionary(s => s[0].UrlDecode(), s => s.Length > 1 ? s[1].UrlDecode() : String.Empty);
                    // ReSharper restore PossibleNullReferenceException
                    break;
            }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="UrlEncodedMessage" /> class.
        /// </summary>
        /// <param name="command"> The command. </param>
        /// <param name="data"> The data. </param>
        /// <param name="seperator"> </param>
        /// <param name="storeTypeInformation"> </param>
        /// <remarks>
        /// </remarks>
        public UrlEncodedMessage(string command, IDictionary<string, string> data, string seperator = "&", bool storeTypeInformation = false) {
            _separatorString = seperator;
            _separator = seperator.ToCharArray();
            _storeTypeInformation = storeTypeInformation;
            Command = command;
            _data = data;
        }

        /// <summary>
        ///     Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        ///     A <see cref="System.String" /> that represents this instance.
        /// </returns>
        /// <remarks>
        /// </remarks>
        public override string ToString() {
            return _data.Any()
                ? _data.Keys.Aggregate(string.IsNullOrEmpty(Command) ? string.Empty : Command.UrlEncode() + "?", (current, k) => current + (!string.IsNullOrEmpty(_data[k]) ? (k.UrlEncode() + "=" + _data[k].UrlEncode() + _separatorString) : string.Empty))
                : Command.UrlEncode();
        }

        public string ToSmallerString() {
            return _data.Any()
                ? _data.Keys.Aggregate(string.IsNullOrEmpty(Command) ? string.Empty : Command + "?",
                    (current, k) => current + (!string.IsNullOrEmpty(_data[k]) ? (k + "=" + _data[k].Substring(0, Math.Min(_data[k].Length, 512)) + _separatorString) : string.Empty))
                : Command;
        }

        /// <summary>
        ///     Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        ///     An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        /// <remarks>
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        /// <summary>
        ///     Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        ///     A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <remarks>
        /// </remarks>
        public IEnumerator<string> GetEnumerator() {
            return _data.Keys.GetEnumerator();
        }

        public string this[string key] {
            get {
                return GetValueAsString(key);
            }
            set {
                Add(key, value);
            }
        }

        private const string CollectionEx = @"^{0}\[(\d*)\]$";
        private const string ComplexCollectionEx = @"^{0}\[(\d*?)\]{1}$";

        private IEnumerable<string> GetCollectionOfString(string collectionName) {
            var rx = new Regex(CollectionEx.format(Regex.Escape(collectionName)));
            return (from k in _data.Keys
                let match = rx.Match(k)
                where match.Success
                select new {
                    index = match.Groups[1].Captures[0].Value.UrlDecode().ToInt32(),
                    value = _data[k]
                }).OrderBy(each => each.index).Select(each => each.value);
        }

        private IEnumerable<object> GetCollectionOfParsable(string collectionName, Type elementType) {
            var rx = new Regex(CollectionEx.format(Regex.Escape(collectionName)));
            return (from k in _data.Keys
                let match = rx.Match(k)
                where match.Success
                select new {
                    index = match.Groups[1].Captures[0].Value.UrlDecode().ToInt32(),
                    value = _data[k]
                }).OrderBy(each => each.index).Select(each => elementType.ParseString(each.value));
            /* var li = new List<Tuple<int,string>>();
            foreach( var k in _data.Keys) {
                var match = rx.Match(k);
                if( match.Success) {
                    var t = new Tuple<int, string>(match.Groups[1].Captures[0].Value.UrlDecode().ToInt32(), _data[k]);
                    li.Add( t);
                }
            }
            var ob = li.OrderBy(each => each.Item1).ToArray();
            var results = ob.Select(each => elementType.ParseString(each.Item2)).ToArray();
            return results; */
        }

        private object GetValueAsArrayOfParsable(string collectionName, Type elementType) {
            if (elementType == typeof (string)) {
                return GetCollectionOfString(collectionName).ToArray();
            }
            return GetCollectionOfParsable(collectionName, elementType).ToArrayOfType(elementType);
        }

        private object GetValueAsArrayOfComplex(string collectionName, Type elementType) {
            var rx = new Regex(@"^{0}\[(\d*?)\](.*)$".format(Regex.Escape(collectionName)));
            return (from k in _data.Keys
                let match = rx.Match(k)
                where match.Success
                select GetValue(string.Format("{0}[{1}]", collectionName, match.Groups[1].Captures[0].Value), elementType)).ToArrayOfType(elementType);
        }

        private object GetValueAsIEnumerableOfParsable(string collectionName, Type elementType) {
            if (elementType == typeof (string)) {
                return GetCollectionOfString(collectionName);
            }
            return GetCollectionOfParsable(collectionName, elementType).CastToIEnumerableOfType(elementType);
        }

        private object GetValueAsIEnumerableOfComplex(string collectionName, Type elementType) {
            var rx = new Regex(@"^{0}\[(\d*?)\](.*)$".format(Regex.Escape(collectionName)));
            return (from k in _data.Keys
                let match = rx.Match(k)
                where match.Success
                select GetValue(string.Format("{0}[{1}]", collectionName, match.Groups[1].Captures[0].Value), elementType)).CastToIEnumerableOfType(elementType);
        }

        private object GetValueAsArray(string collectionName, Type elementType, Type arrayType) {
            return elementType.IsParsable() ? GetValueAsArrayOfParsable(collectionName, elementType) : GetValueAsArrayOfComplex(collectionName, elementType);
        }

        private object GetValueAsIEnumerable(string collectionName, Type elementType, Type collectionType) {
            var collection = elementType.IsParsable() ? GetValueAsIEnumerableOfParsable(collectionName, elementType) : GetValueAsIEnumerableOfComplex(collectionName, elementType);

            if (collectionType.Name.StartsWith("IEnumerable")) {
                return collection;
            }

            // we need to get the collection and then insert the elements into the target type.
            var result = (IList)_typeCreator.CreateInstance(collectionType);

            foreach (var o in (IEnumerable)collection) {
                result.Add(o);
            }
            return result;
        }

        private object GetValueAsEnum(string key, Type enumerationType) {
            var v = GetValueAsString(key);
            if (string.IsNullOrEmpty(v)) {
                return null;
            }
            return Enum.Parse(enumerationType, v);
        }

        private IEnumerable<KeyValuePair<string, string>> GetKeyValueStringPairs(string collectionName) {
            var rx = new Regex(@"^{0}\[(.*?)\]$".format(Regex.Escape(collectionName)));
            return from k in _data.Keys let match = rx.Match(k) where match.Success select new KeyValuePair<string, string>(match.Groups[1].Captures[0].Value.UrlDecode(), _data[k]);
        }

        private object GetValueAsDictionaryOfParsable(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
            IDictionary result;

            if ((dictionaryType.Name.StartsWith("IDictionary")) || (dictionaryType.Name.StartsWith("XDictionary"))) {
                result = (IDictionary)Activator.CreateInstance(typeof (XDictionary<,>).MakeGenericType(keyType, valueType));
            } else {
                result = (IDictionary)Activator.CreateInstance(typeof (Dictionary<,>).MakeGenericType(keyType, valueType));
            }
            foreach (var each in GetKeyValueStringPairs(collectionName)) {
                result.Add(keyType.ParseString(each.Key), valueType.ParseString(each.Value));
            }
            return result;
        }

        private object GetValueAsDictionaryOfComplex(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
            var rx = new Regex(@"^{0}\[(.*?)\](.*)$".format(Regex.Escape(collectionName)));
            var results = from k in _data.Keys
                let match = rx.Match(k)
                where match.Success
                select new KeyValuePair<string, object>(match.Groups[1].Captures[0].Value.UrlDecode(), GetValue(string.Format("{0}[{1}]", collectionName, match.Groups[1].Captures[0].Value), valueType));

            IDictionary result;

            if ((dictionaryType.Name.StartsWith("IDictionary")) || (dictionaryType.Name.StartsWith("XDictionary"))) {
                result = (IDictionary)Activator.CreateInstance(typeof (XDictionary<,>).MakeGenericType(keyType, valueType));
            } else {
                result = (IDictionary)Activator.CreateInstance(typeof (Dictionary<,>).MakeGenericType(keyType, valueType));
            }

            foreach (var each in results) {
                result.Add(keyType.ParseString(each.Key), each.Value);
            }
            return result;
        }

        private object GetValueAsDictionary(string collectionName, Type keyType, Type valueType, Type dictionaryType) {
            return valueType.IsParsable() ? GetValueAsDictionaryOfParsable(collectionName, keyType, valueType, dictionaryType) : GetValueAsDictionaryOfComplex(collectionName, keyType, valueType, dictionaryType);
        }

        private string GetValueAsString(string key) {
            key = key ?? string.Empty;
            return _data.ContainsKey(key) ? _data[key] : string.Empty;
        }

        private object GetValueAsPrimitive(string key, Type primitiveType) {
            return primitiveType.ParseString(_data.ContainsKey(key) ? _data[key] : null);
        }

        private object GetValueAsNullable(string key, Type nullableType) {
            return _data.ContainsKey(key) ? nullableType.ParseString(_data[key]) : null;
        }

        private object GetValueAsOther(string key, Type targetType, object o = null) {

            o = o ?? _typeCreator.CreateObject(this, key, targetType);
            
            // we can't find a type to instantiate that object as. return null.
            if (o == null) {
                return null;
            }

            // the actual type we're now working with.
            targetType = o.GetType();


            lock (o) {
                // $5 to the guy who knows *why* I did this! (GS!)
                foreach (var p in targetType.GetPersistableElements()) {
                    if (p.SetValue != null) {
                        var v = GetValue(FormatKey(key, p.Name), p.DeserializeAsType);
                        if (v == null) {
                            p.SetValue(o, GetValue(FormatKey(key, p.Name), p.DeserializeAsType), null);
                            continue;
                        }

                        if((!p.ActualType.IsInstanceOfType(v)) && _typeCreator.ImplicitlyConvertsTo(p.DeserializeAsType,p.ActualType)) {
                            v = _typeCreator.ImplicitlyConvert(v,p.ActualType);
                        }

                        p.SetValue(o, v, null);
                    }
                }
            }
            return o;
        }

        private string FormatKey(string key, string subkey = null) {
            if (string.IsNullOrEmpty(key)) {
                key = ".";
            }
            if (!string.IsNullOrEmpty(subkey)) {
                key = key.EndsWith(".") ? key + subkey : key + "." + subkey;
            }
            return key;
        }

        private string FormatKeyIndex(string key, int index) {
            if (string.IsNullOrEmpty(key)) {
                key = ".";
            }
            return "{0}[{1}]".format(key, index);
        }

        private string FormatKeyIndex(string key, string index) {
            if (string.IsNullOrEmpty(key)) {
                key = ".";
            }
            return "{0}[{1}]".format(key, index.UrlEncode());
        }

        /// <summary>
        ///     Adds the specified key.
        /// </summary>
        /// <param name="key"> The key. </param>
        /// <param name="value"> The value. </param>
        /// <remarks>
        /// </remarks>
        public void Add(string key, string value) {
            key = FormatKey(key);
            if (!string.IsNullOrEmpty(value)) {
                _data[key] = value;
            }
        }

        public void AddKeyValuePair(string key, string elementName, object elementValue) {
            Add(FormatKeyIndex(key, elementName), elementValue, elementValue.GetType());
        }

        public void AddKeyValueCollection(string key, IEnumerable<KeyValuePair<string, string>> collection) {
            key = FormatKey(key);
            foreach (var each in collection) {
                AddKeyValuePair(key, each.Key, each.Value);
            }
        }

        public void AddDictionary(string key, IDictionary collection) {
            key = FormatKey(key);
            foreach (var each in collection.Keys) {
                if (each.GetType().IsParsable()) {
                    AddKeyValuePair(key, each.ToString(), collection[each]);
                }
            }
        }

        public void AddStringCollection(string key, IEnumerable<string> values) {
            if (values != null) {
                var index = 0;
                foreach (var s in values.Where(s => !string.IsNullOrEmpty(s))) {
                    Add(FormatKeyIndex(key, index++), s);
                }
            }
        }

        public void AddCollection(string key, IEnumerable values, Type serializeElementAsType) {
            if (values != null) {
                var index = 0;
                for (var enmerator = values.GetEnumerator(); enmerator.MoveNext();) {
                    if (enmerator.Current != null) {
                        Add(FormatKeyIndex(key, index++), enmerator.Current, serializeElementAsType);
                    }
                }
            }
        }

        /// <summary>
        ///     Gets the collection.
        /// </summary>
        /// <param name="key"> The key for the collection. </param>
        /// <returns> </returns>
        /// <remarks>
        /// </remarks>
        public IEnumerable<string> GetCollection(string key) {
            key = FormatKey(key);
            var rx = new Regex(@"^{0}\[\d*\]$".format(Regex.Escape(key)));
            return from k in _data.Keys where rx.IsMatch(k) select _data[k];
        }

        public void Add(string argName, object arg, Type argType) {
            argName = FormatKey(argName);
            if (arg == null) {
                return;
            }

            if(_typeCreator.ImplicitlyConvertsTo(arg.GetType(),argType)) {
                arg = _typeCreator.ImplicitlyConvert(arg, argType);
            }

            if (_storeTypeInformation) {
                Add(argName + "$T$", argType.FullName);
            }

            var custom = CustomSerializer.GetCustomSerializer(argType);
            if (custom != null) {
                custom.SerializeObject(this, argName, arg);
            }

            if (argType == typeof (string) || argType.IsEnum || argType.IsParsable()) {
                Add(argName, arg.ToString());
                return;
            }

            if (argType.IsDictionary()) {
                AddDictionary(argName, (IDictionary)arg);
                return;
            }

            if (argType.IsArray || argType.IsIEnumerable()) {
                AddCollection(argName, (IEnumerable)arg, argType.GetPersistableInfo().ElementType);
                return;
            }

            // fall through to reflection-based serialization.
            foreach (var p in argType.GetPersistableElements()) {
                if (p.GetValue != null) {
                    Add(FormatKey(argName, p.Name), p.GetValue(arg, null), p.SerializeAsType);
                }
            }
        }

        public void Add(string key, object value) {
            Add(key, value, value.GetType());
        }

        public static implicit operator string(UrlEncodedMessage value) {
            return value.ToString();
        }

        public T GetValue<T>(string key) {
            return (T)GetValue(key, typeof (T));
        }

        public object GetValue(string key, Type argType, object o = null) {
            key = FormatKey(key);

            var custom = CustomSerializer.GetCustomSerializer(argType);
            if (custom != null) {
                return custom.DeserializeObject(this, key);
            }

            var pi = _typeCreator.GetPersistableInfo(argType);

            switch (pi.PersistableCategory) {
                case PersistableCategory.String:
                    return GetValueAsString(key);

                case PersistableCategory.Parseable:
                    return GetValueAsPrimitive(key, pi.Type);

                case PersistableCategory.Nullable:
                    return GetValueAsNullable(key, pi.NullableType);

                case PersistableCategory.Enumerable:
                    return GetValueAsIEnumerable(key, pi.ElementType, pi.Type);

                case PersistableCategory.Array:
                    return GetValueAsArray(key, pi.ElementType, pi.Type);

                case PersistableCategory.Dictionary:
                    return GetValueAsDictionary(key, pi.DictionaryKeyType, pi.DictionaryValueType, pi.Type);

                case PersistableCategory.Enumeration:
                    return GetValueAsEnum(key, pi.Type);

                case PersistableCategory.Other:
                    return GetValueAsOther(key, pi.Type, o);
            }
            return o;
        }

        public T DeserializeTo<T>(T intoInstance = default(T), string key = null) {
            return (T)GetValue(key, typeof (T), intoInstance);
        }
    }
}