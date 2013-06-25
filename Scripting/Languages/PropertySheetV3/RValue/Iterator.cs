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
    using System.Text;
    using Core.Collections;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheet;
    using Languages.PropertySheetV3;
    using Mapping;
    using Utility;
    using PropertySheet = PropertySheetV3.PropertySheet;
    using PropertySheetParser = PropertySheetV3.PropertySheetParser;

    public class Iterator : List<IValue>, IValue {
        private readonly SourceLocation[] _sourceLocations = SourceLocation.Unknowns;
        public readonly List<Token> Template = new List<Token>();
        public IValueContext Context {
            get;
            set;
        }

        public Iterator(ObjectNode context,params SourceLocation[] sourceLocation) {
            _sourceLocations = sourceLocation;
            Context = context;
        }

        public Iterator(ObjectNode context, IValue chainedSource)
            : this(context, chainedSource.SourceLocations.ToArray()) {
            Add(chainedSource);
        }

        public string GetValue(IValueContext currentContext) {
            
            var v = GetValues(currentContext).ToArray();
            switch (v.Length) {
                case 0:
                    return string.Empty;

                case 1:
                    return this[0].GetValue(currentContext);
            }
            return v.Aggregate("", (current, each) => current + ", " + each).Trim(',', ' ');
            
        }

        public IEnumerable<string> GetValues(IValueContext currentContext) {
            // need to take all the parameters, and resolve each of them into a collection
            // then matrix out a final collection by looping thru each of the values and 
            // processing the template.
            // then, should return the collection of generated values 
            var t = Template.Select(each => (string)each.Data.ToString()).Aggregate((c, e) => c + e);
            return Permutations.Select(each => (currentContext ?? Context).ResolveMacrosInContext(t, each, false));
        }

        public static implicit operator string(Iterator rvalue) {
            return rvalue.GetValue(rvalue.Context);
        }

        public static implicit operator string[](Iterator rvalue) {
            return rvalue.GetValues(rvalue.Context).ToArray();
        }

        protected IEnumerable<object[]> Permutations {
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
                
                var value = Context.GetMacroValues(parameter.GetValue(Context),null);
                if (value != null && value.Any()) {
                    return value;
                }

                // hmm. didn't seem to resolve to a value.
                // that's ok, we'll just treat it as a collection 
            }
            return parameter.GetValues(Context);
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

    public class ObjectIterator  : Iterator {
        private static IEnumerable<Token> open = new[] { new Token { Type = TokenType.OpenBrace, Data = "{" } };
        private static IEnumerable<Token> close = new[] { new Token { Type = TokenType.CloseBrace, Data = "}" } };
        private static IEnumerable<Token> eof = new[] { new Token { Type = TokenType.Eof, Data = "" } };

        public ObjectIterator(ObjectNode context, params SourceLocation[] sourceLocation) : base(context, sourceLocation) {
        }

        public string Prefix { get; set;}

        public IEnumerable<ToRoute> GetContents(IValueContext currentContext) {
            int counter = 0;

            IEnumerable<ToRoute> result = Enumerable.Empty<ToRoute>();
            var ps = new PropertySheetV3.PropertySheet((Context as ObjectNode).Root);
            var context = ps.Children[(Context as ObjectNode).Selector];
            var propertySheetParser = new PropertySheetParser(Enumerable.Empty< Token>(), ps, "");
           
            foreach (var permutation in Permutations ) {
                IEnumerable<Token> tokens = DoIt(Template).ToArray();

                if (tokens.FirstOrDefault().Type != TokenType.OpenBrace) {
                    tokens = open.Concat(tokens).Concat(close);
                } 

                //tokens = PropertySheetTokenizer.Tokenize((currentContext ?? Context).ResolveMacrosInContext(tokens.Select(each => each.Data.ToString()).Aggregate((cur, each) => cur + each),permutation), TokenizerVersion.V3);

                tokens = PropertySheetTokenizer.Tokenize((currentContext ?? Context).ResolveMacrosInContext(
                    tokens.Select(each => each.Data.ToString()
                ).Aggregate((cur, each) => cur + each), permutation, true).Replace("${#", "${"), TokenizerVersion.V3);
                
#if DEBUG
                var tokenArray = tokens.ToArray();

                Event<Verbose>.Raise("Prefix:'{0}#{1}'".format(Prefix, counter), DoIt(tokenArray).ToArray().Select(each => (string)each.Data.ToString()).CollapseToString(""));

                Event<Debug>.Raise("Prefix:'{0}#{1}'".format(Prefix, counter), tokenArray.Select(each => (string)each.Data.ToString()).CollapseToString(""));

                Event<Debug>.Raise("Prefix:'{0}#{1}'".format(Prefix,counter), DoIt(tokenArray).ToArray().Select(each => (string)each.Data.ToString()).CollapseToString(""));
                propertySheetParser.ResetParser(tokenArray);
#else
                propertySheetParser.ResetParser(tokens);
#endif



                propertySheetParser.ParseItemAsDictionary(context, "{0}#{1}#".format(Prefix, counter++)) ;
                result = result.Concat(context.Routes);
            }

            return result;
        }

        public IEnumerable<Token> DoIt(IEnumerable<Token> tokens ) {
            return tokens.Select(token => {
                switch (token.Type) {
                    case TokenType.SelectorParameter:
                        token.Data = "[" + token.Data + "]";
                        break;
                    case TokenType.EmbeddedInstruction:
                        token.Data = "<-- " + token.Data + " -->";
                        break;
                    case TokenType.StringLiteral:
                    
                        token.Data = @"@""" + ((string)token.Data).Replace(@"""", @"""""") + @"""";
                        break;
                    // case TokenType.MacroExpression:
                        // token.Data = (currentContext ?? Context).ResolveMacrosInContext("${" + token.Data + "}", macros);
                       // token.Data = "${" + token.Data + "}";
                        //break;

                    default:
                        // token.Data = token.Data = (currentContext ?? Context).ResolveMacrosInContext(token.Data.ToString(), macros);
                        break;
                }
                return token;
            });
        }
    }
}