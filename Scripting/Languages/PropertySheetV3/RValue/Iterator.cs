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
    using System.Text;
    using Core.Extensions;

    public class Iterator : List<IValue>, IValue {
        public readonly StringBuilder Template;
        public IValueContext Context {
            get;
            set;
        }

        public Iterator(ObjectNode context) {
            Template = new StringBuilder();
            Context = context;
        }

        public Iterator(ObjectNode context, IValue chainedSource)
            : this(context) {
            Add(chainedSource);
        }

        public string Value {
            get {
                var v = Values.ToArray();
                switch (v.Length) {
                    case 0:
                        return string.Empty;

                    case 1:
                        return this[0].Value;
                }
                return v.Aggregate("", (current, each) => current + ", " + each).Trim(',', ' ');
            }
        }

        public IEnumerable<string> Values {
            get {
                // need to take all the parameters, and resolve each of them into a collection
                // then matrix out a final collection by looping thru each of the values and 
                // processing the template.
                // then, should return the collection of generated values 
                var t = Template.ToString();
                return Permutations.Select(each => Context.ResolveMacrosInContext(t, each));
            }
        }

        public static implicit operator string(Iterator rvalue) {
            return rvalue.Value;
        }

        public static implicit operator string[](Iterator rvalue) {
            return rvalue.Values.ToArray();
        }

        private IEnumerable<object[]> Permutations {
            get {
                if (this.IsNullOrEmpty()) {
                    yield return new object[0];
                    yield break;
                }
                var iterators = new IEnumerator<object>[Count];
                for (int i = 0; i < Count; i++) {
                    iterators[i] = ResolveParameter(this[i]).ToList().GetEnumerator();
                    if (i > 0) {
                        iterators[i].MoveNext();
                    }
                }

                while (RecursiveStep(0, iterators) < Count) {
                    yield return iterators.Select(each => each.Current).ToArray();
                }
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

        private IEnumerable<string> ResolveParameter(IValue parameter) {
            // the RValue could be a selector or it can actually be an RValue or RValueCollection
            // really, we can't know for sure until we go to actually extract the value.
            if (parameter is Scalar) {
                // if this is an Scalar, then it's possible that it can match for a value 
                // in the view.
                var value = Context.TryGetRValueInContext(parameter.Value);
                if (value != null) {
                    return value;
                }

                // hmm. didn't seem to resolve to a value.
                // that's ok, we'll just treat it as a collection 
            }
            return parameter.Values;
        }
    }
}