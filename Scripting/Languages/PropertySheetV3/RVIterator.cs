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
    using System.Text;

    using Tokens = System.Collections.Generic.List<Utility.Token>;

    
    public class RVIterator : RValue {
        public class RVIteratorParameter {
            protected RVIteratorParameter() {
            }
        }

        public class RVIteratorValueParameter : RVIteratorParameter {
            public readonly RValue RValue;

            public RVIteratorValueParameter(RValue rValue) {
                RValue = rValue;
            }
        }

        public class RVIteratorReferenceParameter {
            public readonly Selector RValueReference;

            public RVIteratorReferenceParameter(Selector rValueReference) {
                RValueReference = rValueReference;
            }
        }


        public readonly List<RVIteratorParameter> Sources;
        public readonly StringBuilder Template;

        public RVIterator() {
            Sources = new List<RVIteratorParameter>();
            Template = new StringBuilder();
        }

        public RVIterator(RVIteratorParameter chainedSource): this() {
            Sources.Add(chainedSource);
        }

        public RVIterator(RValue chainedSource)
            : this(new RVIteratorValueParameter(chainedSource)) {
            
        }

        public override RVSingle Single() {
            return Collection().Single();
        }

        public override RVCollection Collection() {
            // temporary...
            return new RVCollection( new List<RValue> {new RVSingle( "Iterate over collection items") });
        }
    }
}