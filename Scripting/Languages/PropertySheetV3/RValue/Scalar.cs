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
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Utility;

    public class Scalar : IValue {
        
        public static Scalar Empty = new Scalar(null, string.Empty);
        public IValueContext Context {
            get;
            set;
        }
        private readonly string _content;

        public Scalar(ObjectNode context, IEnumerable<Token> singleExpression) {
            var item = singleExpression.ToList();

            // trim off whitespace 
            while (item.Count > 0 && item[0].IsWhitespaceOrComment) {
                item.RemoveAt(0);
            }
            while (item.Count > 0 && item[item.Count - 1].IsWhitespaceOrComment) {
                item.RemoveAt(item.Count - 1);
            }

            // may have to expand out certian types of tokens here.
            _content = item.Aggregate("", (current, each) => current + each.Data);
            Context = context;
        }

        public Scalar(ObjectNode context, string value) {
            _content = value;
            Context = context;
        }

        public string Value {
            get {
                if (Context == null) {
                    return _content;
                }
                return Context.ResolveMacrosInContext(_content,null);
            }
        }

        public IEnumerable<string> Values {
            get {
                return Value.Split(new[] {
                    ','
                }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public static implicit operator string(Scalar rvalue) {
            return rvalue.Value;
        }

        public static implicit operator string[](Scalar rvalue) {
            return rvalue.Values.ToArray();
        }
    }
}