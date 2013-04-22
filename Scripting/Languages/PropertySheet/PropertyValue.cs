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

namespace ClrPlus.Scripting.Languages.PropertySheet {
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Core.Extensions;

    public class PropertyValue : IPropertyValue {
        private static readonly IEnumerable<object>[] NoCollection = new[] {
            "".SingleItemAsEnumerable()
        };

        internal readonly string[] _collectionNames;

        internal readonly PropertyRule ParentPropertyRule;
        private readonly List<string> _values = new List<string>();

        public SourceLocation SourceLocation {get; internal set;}
        public string Label {get; private set;}

        internal PropertyValue(PropertyRule parent, string label, IEnumerable<string> collectionNames = null) {
            ParentPropertyRule = parent;
            Label = label;
            _collectionNames = (collectionNames == null) ? null : collectionNames.Where(each => !string.IsNullOrEmpty(each)).ToArray();
        }

        internal IPropertyValue Actual(string label) {
            if (_collectionNames.IsNullOrEmpty()) {
                return Label == label ? this : null; // this should shortcut nicely when there is no collection.
            }

            var values = Permutations.Where(each => ParentPropertySheet.ResolveMacros(Label, each) == label)
                                     .SelectMany(each => _values
                                         .Select(value => ParentPropertySheet.ResolveMacros(value, each)))
                                     .ToArray();

            if (values.Length > 0) {
                return new ActualPropertyValue {
                    SourceLocation = SourceLocation,
                    IsSingleValue = values.Length == 1,
                    HasMultipleValues = values.Length > 1,
                    SourceString = "",
                    Value = values[0],
                    Values = values,
                    ParentRule = ParentPropertyRule.ParentRule
                };
            }

            return null;
        }

        private PropertySheet ParentPropertySheet {
            get {
                return ParentPropertyRule.ParentRule.ParentPropertySheet;
            }
        }

        private int RecursiveStep(int currentIndex, IEnumerator<object>[] enumerators) {
            if (currentIndex < enumerators.Length) {
                if (enumerators[currentIndex].MoveNext()) {
                    return currentIndex;
                }
                enumerators[currentIndex].Reset();
                enumerators[currentIndex].MoveNext();
                return RecursiveStep(currentIndex + 1, enumerators);
            }
            return currentIndex;
        }

        private IEnumerable<object> GetCollection(string name) {
            var result = ParentPropertySheet.GetCollection(name);
            return (result.IsNullOrEmpty() ? "".SingleItemAsEnumerable() : result).ToArray();
        }

        private IEnumerable<object[]> Permutations {
            get {
                if (_collectionNames.IsNullOrEmpty()) {
                    yield return new object[0];
                    yield break;
                }
                var iterators = new IEnumerator<object>[_collectionNames.Length];
                for (int i = 0; i < _collectionNames.Length; i++) {
                    iterators[i] = GetCollection(_collectionNames[i]).GetEnumerator();
                    if (i > 0) {
                        iterators[i].MoveNext();
                    }
                }

                while (RecursiveStep(0, iterators) < _collectionNames.Length) {
                    yield return iterators.Select(each => each.Current).ToArray();
                }
            }
        }

        public IEnumerable<string> CollectionValues {
            get {
                return Permutations.SelectMany(each => _values.Select(value => ParentPropertySheet.ResolveMacros(value, each)));
            }
        }

        public IEnumerator<string> GetEnumerator() {
            // an enumerator for all the collection values, regardless of replacement.
            return CollectionValues.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public string Value {
            get {
                return ParentPropertySheet.ResolveMacros(this.FirstOrDefault());
            }
            set {
                _values.Clear();
                _values.Add(value);
            }
        }

        public IEnumerable<string> SourceValues {
            get {
                return _values.ToArray();
            }
            set {
                _values.Clear();
                _values.AddRange(value);
            }
        }

        public IEnumerable<string> Values {
            get {
                return this.Select(each => ParentPropertySheet.ResolveMacros(each));
            }
        }

        public int Count {
            get {
                if (_collectionNames.IsNullOrEmpty() || ParentPropertySheet.GetCollection == null) {
                    return _values.Count;
                }
                return _collectionNames.Aggregate(_values.Count, (current, x) => current*GetCollection(x).Count());
            }
        }

        public bool IsSingleValue {
            get {
                return Count == 1;
            }
        }

        public bool HasMultipleValues {
            get {
                return Count > 1;
            }
        }

        public void Add(string value) {
            _values.Add(value);
        }

        public void Clear() {
            _values.Clear();
        }

        public string SourceString {
            get {
                if (_collectionNames.IsNullOrEmpty()) {
                    if (Label.IsNullOrEmpty()) {
                        if (_values.Count == 1) {
                            return PropertySheet.QuoteIfNeeded(_values[0]) + ";\r\n";
                        }
                        if (_values.Count > 1) {
                            return "{\r\n        " + string.Join(",\r\n        ", _values.Select(PropertySheet.QuoteIfNeeded)) + "\r\n    };\r\n\r\n";
                        }
                        if (_values.Count == 0) {
                            return @"""""; // WARNING--THIS SHOULD NOT BE HAPPENING. EMPTY VALUE LISTS ARE SIGN THAT YOU HAVE NOT PAID ENOUGH ATTENTION";
                        }
                    }
                    if (_values.Count == 1) {
                        return "{0} = {1};\r\n".format(PropertySheet.QuoteIfNeeded(Label), PropertySheet.QuoteIfNeeded(_values[0]));
                    }
                    if (_values.Count > 1) {
                        return "{0} = {1}".format(PropertySheet.QuoteIfNeeded(Label), "{\r\n        " + string.Join(",\r\n        ", _values.Select(PropertySheet.QuoteIfNeeded)) + "\r\n    };\r\n\r\n");
                    }
                    if (_values.Count == 0) {
                        return @"{0} = """"; // WARNING--THIS SHOULD NOT BE HAPPENING. EMPTY VALUE LISTS ARE SIGN THAT YOU HAVE NOT PAID ENOUGH ATTENTION".format(PropertySheet.QuoteIfNeeded(Label));
                    }
                }

                // it's a lambda somehow...
                if (Label.IsNullOrEmpty()) {
                    return _values.Aggregate("{", (current, v) => current + ("\r\n        (" + string.Join(", ", _collectionNames.Select(PropertySheet.QuoteIfNeeded)) + ")  => " + PropertySheet.QuoteIfNeeded(v) + ";")) + "\r\n    };\r\n\r\n";
                }

                return
                    _values.Aggregate("{", (current, v) => current + ("\r\n        (" + string.Join(", ", _collectionNames.Select(PropertySheet.QuoteIfNeeded)) + ")  => " + PropertySheet.QuoteIfNeeded(Label) + " = " + PropertySheet.QuoteIfNeeded(v) + ";")) +
                        "\r\n    };\r\n\r\n";
            }
        }

        internal IEnumerable<string> ResolvedLabels {
            get {
                return Permutations.Select(each => ParentPropertySheet.ResolveMacros(Label, each));
            }
        }
    }
}