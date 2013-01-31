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
    using System.Linq;
    using Utility;

    public class RVSingle : RValue {
        public static RVSingle Empty = new RVSingle(string.Empty);

        public readonly string Value;

        public RVSingle(IEnumerable<Token> singleExpression) {
            var item = singleExpression.ToList();

            // trim off whitespace 
            while(item.Count > 0 && item[0].IsWhitespaceOrComment) {
                item.RemoveAt(0);
            }
            while(item.Count > 0 && item[item.Count - 1].IsWhitespaceOrComment) {
                item.RemoveAt(item.Count - 1);
            }

            // may have to expand out certian types of tokens here.
            Value = item.Aggregate("", (current, each) => current + each.Data);
        }
        public RVSingle(string value) {
            Value = value;
        }

        public override RVSingle Single() {
            return this;
        }

        public override RVCollection Collection() {
            return new RVCollection(Value.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select( each => (RValue)new RVSingle(each)).ToList());
        }
    }
}