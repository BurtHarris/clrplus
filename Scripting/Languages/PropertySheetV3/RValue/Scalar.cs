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
    using Languages.PropertySheet;
    using Utility;

    public class Scalar : IValue {
        
        public static Scalar Empty = new Scalar(null, string.Empty);
        public IValueContext Context {
            get;
            set;
        }

        private readonly string _content;
        private readonly SourceLocation[] _sourceLocations = SourceLocation.Unknowns;

        public Scalar(ObjectNode context, IEnumerable<Token> singleExpression, string sourceFile) {
            var item = singleExpression.ToList();

            // trim off whitespace 
            while (item.Count > 0 && item[0].IsWhitespaceOrComment) {
                item.RemoveAt(0);
            }
            _sourceLocations = new[] { new SourceLocation(item.FirstOrDefault(), sourceFile) };

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

        public string GetValue(IValueContext currentContext) {
            if (Context == null) {
                return _content;
            }
            return (currentContext ?? Context).ResolveMacrosInContext(_content, null, false);
        }

        public IEnumerable<string> GetValues(IValueContext currentContext) {
            
                return GetValue(currentContext).Split(new[] {
                    ','
                }, StringSplitOptions.RemoveEmptyEntries).Select( each => each.Trim());
            
        }

        public static implicit operator string(Scalar rvalue) {
            return rvalue.GetValue(rvalue.Context);
        }

        public static implicit operator string[](Scalar rvalue) {
            return rvalue.GetValues(rvalue.Context).ToArray();
        }

        public IEnumerable<string> SourceText {
            get {
                yield return "";
            }
        }

        public IEnumerable<SourceLocation> SourceLocations {
            get {
                return _sourceLocations;
            }
        }
    }
}