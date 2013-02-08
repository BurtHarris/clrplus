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
    using System.Collections.Generic;
    using System.Linq;

    public class RVCollection : RValue {
        public readonly List<RValue> Values;
        public RVCollection() {
            Values = new List<RValue>();
        }
        public RVCollection(List<RValue> values) {
            Values = values;
        }

        public RVCollection Add(RValue value) {
            Values.Add(value);
            return this;
        }

        public override RVSingle Single() {
            switch (Values.Count) {
                case 0:
                    return RVSingle.Empty;
                
                case 1:
                    return Values.First().Single();

                default:
                    return new RVSingle(Values.Aggregate("", (current, each) => current +", " + each.Single().Value).Trim(',',' '));
            }
        }

        public override RVCollection Collection() {
            return this;
        }
    }
}