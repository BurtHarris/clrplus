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
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using Core.Extensions;

    /// <summary>
    ///     A PropertyRule represents a single property name, with potentially multiple property-labels, each label can have 1 or more values.
    /// </summary>
    public abstract class PropertyRule : DynamicObject {
        internal readonly Rule ParentRule;
        public SourceLocation SourceLocation {get; internal set;}
        public string Name {get; set;}

        protected PropertyRule(Rule parent, string name) {
            ParentRule = parent;
            Name = name;
        }

        internal abstract string SourceString {get;}

        public abstract IEnumerable<PropertyValue> PropertyValues {get;}

        public abstract string Value {
            get; set;}
        public abstract IEnumerable<string> Values {get;}
        public abstract IEnumerable<string> Labels {get;}
        public abstract bool HasValues {get;}
        public abstract bool HasValue {get;}
        public abstract IPropertyValue this[string label] {get;}
        public abstract PropertyValue GetPropertyValue(string label, IEnumerable<string> collections = null);
    }

    public class StandardPropertyRule : PropertyRule {
        private readonly List<PropertyValue> _propertyValues = new List<PropertyValue>();

        /// <summary>
        ///     PropertyRule object must be created by the Rule.
        /// </summary>
        /// <param name="parent"> </param>
        /// <param name="name"> </param>
        internal StandardPropertyRule(Rule parent, string name) : base(parent, name) {
        }

        internal override string SourceString {
            get {
                return _propertyValues.Aggregate("", (current, v) => current + "    {0} : {1}".format(Name, v.SourceString));
            }
        }

        public override IEnumerable<PropertyValue> PropertyValues {
            get {
                return _propertyValues.ToArray();
            }
        }

        public override string ToString() {
            var items = Labels.Select(each => new {
                label = each,
                values = this[each] ?? Enumerable.Empty<string>()
            });
            var result = items.Where(item => item.values.Any()).Aggregate("", (current1, item) => current1 + (item.values.Count() == 1
                ? PropertySheet.QuoteIfNeeded(Name) + PropertySheet.QuoteIfNeeded(item.label) + " = " + PropertySheet.QuoteIfNeeded(item.values.First()) + ";\r\n"
                : PropertySheet.QuoteIfNeeded(Name) + PropertySheet.QuoteIfNeeded(item.label) + " = {\r\n" + item.values.Aggregate("", (current, each) => current + "        " + PropertySheet.QuoteIfNeeded(each) + ",\r\n") + "    };\r\n"));

            return result;
        }

        public override string Value {
            get {
                var v = this[string.Empty];
                return v == null ? null : this[string.Empty].Value;
            } set {
                var v = this[string.Empty] as PropertyValue;
                if (v != null) {
                    v.Value = value;
                }
            }
        }

        public override IEnumerable<string> Values {
            get {
                return this[string.Empty] ?? Enumerable.Empty<string>();
            }
        }

        public override IEnumerable<string> Labels {
            get {
                return _propertyValues.SelectMany(each => each.ResolvedLabels).Distinct();
            }
        }

        public override bool HasValues {
            get {
                return _propertyValues.Count > 0;
            }
        }

        public override bool HasValue {
            get {
                return _propertyValues.Count > 0;
            }
        }

        public override IPropertyValue this[string label] {
            get {
                // looks up the property collection
                return (from propertyValue in _propertyValues let actual = propertyValue.Actual(label) where actual != null select actual).FirstOrDefault();
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            var primary = ParentRule.ParentPropertySheet.PreferDashedNames ? binder.Name.CamelCaseToDashed() : binder.Name;
            var secondary = ParentRule.ParentPropertySheet.PreferDashedNames ? binder.Name : binder.Name.CamelCaseToDashed();

            result = GetPropertyValue(_propertyValues.All(each => each.Label != primary) && _propertyValues.Any(each => each.Label == secondary) ? secondary : primary);
            return true;
        }

        /// <summary>
        ///     Gets Or Adds a PropertyValue with the given label and collection.
        /// </summary>
        /// <param name="label"> </param>
        /// <param name="collections"> </param>
        /// <returns> </returns>
        public override PropertyValue GetPropertyValue(string label, IEnumerable<string> collections = null) {
            var result = _propertyValues.FirstOrDefault(each => each.Label == label);
            if (result == null) {
                _propertyValues.Add(result = new PropertyValue(this, label, collections.IsNullOrEmpty() ? null : collections));
            }
            return result;
        }
    }
}