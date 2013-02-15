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

namespace ClrPlus.Scripting.Languages.PropertySheetV3.RValue {
    using System.Collections.Generic;
    using System.Linq;

    public class Collection :List<IValue>, IValue {
        public ObjectNode Context { get; private set; }

        public Collection(ObjectNode context) {
            Context = context;
        }

        public Collection(ObjectNode context, IEnumerable<IValue> values) {
            Context = context;
            AddRange(values);
        }

        public new Collection Add(IValue value) {
            if (value is Collection) {
                return AddRange(value as Collection);
            }
            base.Add(value);
            return this;
        }

        public new Collection AddRange(IEnumerable<IValue> values) {
            base.AddRange(values);
            return this;
        }

        public IEnumerable<string> Values { get {
            if (Count == 0) {
                return new [] {string.Empty};
            }
            
            return this.Select(each => Context.ResolveMacrosInContext(each.Value));
        }}

        public string Value {
            get {
                switch(Count) {
                    case 0:
                        return string.Empty;

                    case 1:
                        return this[0].Value;
                }
                return this.Aggregate("", (current, each) => current + ", " + each.Value).Trim(',', ' ');
            }
        }

        public static implicit operator string(Collection rvalue) {
            return rvalue.Value;
        }

        public static implicit operator string[](Collection rvalue) {
            return rvalue.Values.ToArray();
        }
    }
}