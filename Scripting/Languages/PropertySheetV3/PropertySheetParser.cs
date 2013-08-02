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
    using System.IO;
    using System.Linq;
    using Core.Collections;
    using Core.Exceptions;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheet;
    using RValue;
    using Utility;
    using Tokens = System.Collections.Generic.IEnumerable<Utility.Token>;
    using TokenTypes = System.Collections.Generic.IEnumerable<Utility.TokenType>;
    using Tailcall = Continuation;

    public delegate Continuation Continuation();

    // ReSharper disable PossibleMultipleEnumeration
    public class PropertySheetParser {
        protected readonly Tailcall Done;
        protected readonly Tailcall Invalid;

        private readonly PropertySheet _propertySheet;
        private IEnumerator<Token> _enumerator;

        private Token _token;
        private bool _rewind;

        // [DebuggerDisplay("NextToken = {Peek.Type}")]
        protected Token Next {
            get {
                // if the current token is supposed to be looked at again, 
                // don't move, just clear the flag.
                if (_rewind) {
                    _rewind = false;
                    return _token;
                }

                if (_enumerator.MoveNext()) {
                    return _token = _enumerator.Current;
                }
                return _token = new Token {
                    Type = TokenType.Eof,
                    Data = ""
                };
            }
        }

#if DEBUGX
        protected Token Peek {
            get {
                if(_rewind) {
                    return _token;
                }

                var myEnum = _enumerator.Clone();
                if(myEnum.MoveNext()) {
                    return _token = _enumerator.Current;
                }
                return _token = new Token {
                    Type = TokenType.Eof
                };
            }
        }
#endif
        // [DebuggerDisplay("NextToken = {Peek.Type}")]
        protected TokenType NextType {
            get {
                var t = Next;
                return t.Type;
            }
        }

        protected Tailcall Continue {
            get {
                // return Type == TokenType.Eof ? Done : _R_.Value;
                return Type == TokenType.Eof ? Done : Invalid;
            }
        }

        protected TokenType Type {
            get {
                return _token.Type;
            }
        }

        protected string Data {
            get {
                return _token.Data.ToString();
            }
        }

        protected Token Token {
            get {
                return _token;
            }
        }

        protected readonly string Filename;

        internal PropertySheetParser(IEnumerable<Token> tokens, PropertySheet propertySheet, string filename) {
            Filename = filename;
            _enumerator = tokens.GetEnumerator();

            _propertySheet = propertySheet;

            Done = () => null;
            Invalid = () => null;
        }

        internal void ResetParser(IEnumerable<Token> tokens) {
            _enumerator = tokens.GetEnumerator();
            var x = Next;

        }


        internal void Parse() {
            Global();
        }

        public static readonly TokenTypes Semicolon = new[] {
            TokenType.Semicolon
        };

        public static readonly TokenTypes Comma = new[] {
            TokenType.Comma
        };

        public static readonly TokenTypes CommaOrCloseBrace = new[] {
            TokenType.Comma, TokenType.CloseBrace
        };

        public static readonly TokenTypes CommaOrCloseParenthesis = new[] {
            TokenType.Comma, TokenType.CloseParenthesis
        };

        public static readonly TokenTypes SemicolonOrComma = new[] {
            TokenType.Semicolon, TokenType.Comma
        };

        public static readonly TokenTypes SemicolonCommaOrCloseBrace = new[] {
            TokenType.Semicolon, TokenType.Comma, TokenType.CloseBrace
        };

        public static readonly TokenTypes OpenBrace = new[] {
            TokenType.OpenBrace
        };

        public static readonly TokenTypes OpenBraceInstructionOrColonEquals = new[] {
            TokenType.OpenBrace, TokenType.EmbeddedInstruction, TokenType.ColonEquals, 
        };

        public static readonly TokenTypes MemberTerminator = new[] {
            TokenType.OpenBrace, TokenType.Colon, TokenType.PlusEquals, TokenType.Equal, TokenType.ColonEquals, 
        };

        public static readonly TokenTypes Equal = new[] {
            TokenType.Equal
        };

        public static readonly TokenTypes ColonOrEqual = new[] {
            TokenType.Colon, TokenType.Equal
        };

        public static readonly TokenTypes Comments = new[] {
            TokenType.LineComment, TokenType.MultilineComment
        };

        public static readonly TokenTypes WhiteSpaceOrComments = Comments.UnionA(TokenType.WhiteSpace);
        public static readonly TokenTypes WhiteSpaceCommentsOrSemicolons = WhiteSpaceOrComments.UnionA(TokenType.Semicolon);

        protected void Rewind() {
            _rewind = true;
        }

        protected T Rewind<T>(T t) {
            Rewind();
            return t;
        }

        protected Exception Fail(ErrorCode code, string format) {
            Event<SourceError>.Raise(code.ToString(), new SourceLocation(Token, Filename).SingleItemAsEnumerable(), format, Data);
            return new ClrPlusException("Fatal Error.");
        }

        /// <exception cref="ParseException">Collections must be nested in an object -- expected one of '.' , '#', '@alias' or identifier</exception>
        private Tailcall Global(Continuation onComplete = null) {
            if ((onComplete ?? Continue)() == Done) {
                return Done;
            }

            switch (NextAfter(WhiteSpaceCommentsOrSemicolons, false)) {
                case TokenType.Eof:
                    return Done;

                case TokenType.Pound:
                    return Global(ParseMetadataItem(_propertySheet));

                case TokenType.Identifier:
                case TokenType.Dot:
                case TokenType.SelectorParameter:
                case TokenType.EmbeddedInstruction:

                    switch (Data) {
                        case "@import":
                            return Global(ParseImport());
                        case "@alias":
                            return Global(ParseAlias(_propertySheet));
                    }

                    Rewind(); // wasn't an import or alias, let the ParseSelector have a go at it.

                    // old way : just parsed a dictionary. New way, it checks to see what kind of dictionary first.
                    // return Global(ParseItemsInDictionary(_propertySheet.Children[ParseSelector(OpenBrace)]));
                    
                    // return Global(ParseItemAsDictionary(_propertySheet.Children[ParseSelector(OpenBraceInstructionOrColonEquals)]));
                    return Global(ParseItemsInDictionary(_propertySheet,justOneItem:true));

                case TokenType.Colon:  
                    // not permitted at global level
                    throw Fail(ErrorCode.TokenNotExpected, "Collections must be nested in an object -- expected one of '.' , '#', '@alias' or identifier");

                case TokenType.Equal:
                    // not permitted at global level
                    throw Fail(ErrorCode.TokenNotExpected, "Assignments must be nested in an object -- expected one of '.' , '#', '@alias' or identifier");

                case TokenType.PlusEquals:
                    // not permitted at global level
                    throw Fail(ErrorCode.TokenNotExpected, "Collection modifiers must be nested in an object -- expected one of '.' , '#', '@alias' or identifier");

                case TokenType.Semicolon:
                    return Global(onComplete);
            }
            throw Fail(ErrorCode.TokenNotExpected, "Unexpected Token '{0}' -- expected one of '.' , '#', '@alias' or identifier");
        }

        /// <exception cref="ParseException">
        ///     Invalid token in selector declaration after < >  or [ ] --found '{0}'
        /// </exception>
        /// <exception cref="ParseException">
        ///     Duplicate < > instruction not permitted.
        /// </exception>
        /// <exception cref="ParseException">Duplicate [ ] parameter not permitted.</exception>
        /// <exception cref="ParseException">Reached terminator '{0}' -- expected selector declaration</exception>
        /// <exception cref="ParseException">Invalid token in selector declaration after < >  or [ ] --found '{0}'</exception>
        private Selector ParseSelector(TokenTypes terminators, SourceLocation sourceLocation = null, string selectorName = null, string parameter = null) {
            NextAfter(WhiteSpaceOrComments);
            sourceLocation = sourceLocation ?? new SourceLocation(Token, Filename);
            switch (Token.Type) {
                case TokenType.Colon:
                    if (selectorName == null && parameter == null) {
                        if (NextType != TokenType.Colon) {
                            throw Fail(ErrorCode.TokenNotExpected, "Single colon not permitted before selector name");
                        }
                        return ParseSelector(terminators,sourceLocation, "::");
                    }

                    if (terminators.Contains(Type)) {
                        if (string.IsNullOrEmpty(selectorName) && string.IsNullOrEmpty(parameter)) {
                           throw Fail(ErrorCode.InvalidSelectorDeclaration, "Reached terminator '{0}' -- expected selector declaration");
                        }
                        return new Selector(selectorName ?? "*", parameter, sourceLocation);
                    }
                    break;
                    
                case TokenType.NumericLiteral:
                case TokenType.Identifier:
                case TokenType.Dot:
                    if ( parameter != null ) {
                        if (selectorName == null) {
                            Rewind();
                            return new Selector("*", parameter,sourceLocation);
                        }
                        throw Fail(ErrorCode.TokenNotExpected, "Invalid token in selector declaration after < >  or [ ] --found '{0}'");
                    }
                    return ParseSelector(terminators,sourceLocation, (selectorName ?? "") + Token.Data);

                case TokenType.SelectorParameter:
                    if (parameter != null) {
                        throw Fail(ErrorCode.TokenNotExpected, "Duplicate [ ] parameter not permitted.");
                    }
                    return ParseSelector(terminators, sourceLocation, selectorName, Token.Data);

                default:
                    if (terminators.Contains(Type)) {
                        if (string.IsNullOrEmpty(selectorName)) {
                            if (string.IsNullOrEmpty(parameter)) {
                                throw Fail(ErrorCode.InvalidSelectorDeclaration, "Reached terminator '{0}' -- expected selector declaration");
                            }
                            return new Selector("*", parameter, sourceLocation);
                        }
                        return new Selector(selectorName, parameter, sourceLocation);
                    }
                    break; // fall thru to end fail.
            }
            throw Fail(ErrorCode.TokenNotExpected, "Invalid token in selector declaration--found '{0}'");
            }

        /// <exception cref="ParseException">@import filename must not be empty.</exception>
        private Tailcall ParseImport(Tokens path = null) {
            switch (path == null ? NextAfter(WhiteSpaceOrComments) : NextType) {
                case TokenType.Semicolon:
                    if (path.IsNullOrEmpty()) {
                        throw Fail(ErrorCode.InvalidImport, "@import filename must not be empty.");
                    }

                    return ImportFile(path.Aggregate("", (current, each) => current + each.Data.ToString()));

                case TokenType.StringLiteral:
                    if (path.IsNullOrEmpty()) {
                        var filename = Data;
                        if (TokenType.Semicolon != NextAfter(WhiteSpaceOrComments)) {
                            throw Fail(ErrorCode.TokenNotExpected, "Unexpected token '{0}' before ';' in @import directive");
                        }
                        return ImportFile(filename);
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Found string literal, expected unquoted token in @import directive (don't embed quotes in the middle of the filename");
            }

            int n;
            if ((n = Data.IndexOfAny(Path.GetInvalidPathChars())) > -1) {
                throw Fail(ErrorCode.InvalidImport, "invalid character '{0} in @import filename.".format(Data.Substring(n, 1)));
            }
            return ParseImport(path.ConcatHappily(Token));
        }

        private Tailcall ParseMetadataItem(ObjectNode context, INode metadataContainer = null) {
            metadataContainer = metadataContainer ?? context;

            switch (NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Identifier:
                    var identifier = Data;
                    switch (NextAfter(WhiteSpaceOrComments)) {
                        case TokenType.Equal:
                            metadataContainer.Metadata.Value.AddOrSet(identifier, ParseRValue(context, Semicolon, null));
                            //context.AddMetadata(identifier, ParseRValue(context, Semicolon));
                            return Continue;

                        case TokenType.Colon:
                            // should we really support this?
                            metadataContainer.Metadata.Value.AddOrSet(identifier, ParseRValue(context, Semicolon, null));
                            //context.AddMetadata(identifier, ParseRValue(context, Semicolon));
                            return Continue;

                        case TokenType.OpenBrace:
                            var metadata = ParseMetadataObject(context);

                            if (metadata != null && metadata.Count > 0) {
                                foreach (var key in metadata.Keys) {
                                    metadataContainer.Metadata.Value.AddOrSet(identifier+"."+key, metadata[key]);
                                }
                                //context.AddMetadata(identifier, metadata);
                            }
                            return Continue;
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Expected '=' in metadata declaration, found {0}");
            }
            throw Fail(ErrorCode.TokenNotExpected, "Expected identifier in metadata declaration, found {0}");
        }

        /// <exception cref="ParseException">Missing alias declaration in @import statement</exception>
        private Tailcall ParseAlias(ObjectNode context) {
            switch (NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Identifier:
                    var identifier = Data;
                    switch (NextAfter(WhiteSpaceOrComments)) {
                        case TokenType.Equal:
                            context.Aliases.Value.Add(identifier, ParseSelector(Semicolon));
                            return Continue;
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Expected '=' in alias declaration, found {0}");
            }
            throw Fail(ErrorCode.TokenNotExpected, "Expected identifier in alias declaration, found {0}");
        }

        private Tailcall ImportFile(string path) {
            // imports a file.
            _propertySheet.ImportFile(path);

            return Continue;
        }

        /// <exception cref="ParseException">Token '{0}' not expected in object declaration</exception>
        private XDictionary<string, IValue> ParseMetadataObject(ObjectNode context, XDictionary<string, IValue> result = null) {
            if (TokenType.CloseBrace == NextAfter(WhiteSpaceCommentsOrSemicolons)) {
                return result;
            }

            Rewind();

            var selector = ParseSelector(ColonOrEqual);

            // should be at the terminator still!
            switch (Type) {
                case TokenType.Equal:
                    result = result ?? new XDictionary<string, IValue>();
                    result.Add(selector.Name, ParseRValue(context, SemicolonCommaOrCloseBrace, null));
                    return ParseMetadataObject(context, result);

                case TokenType.Colon:
                    result = result ?? new XDictionary<string, IValue>();
                    result.Add(selector.Name, ParseRValue(context, SemicolonCommaOrCloseBrace, null));
                    return ParseMetadataObject(context, result);
            }
            throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected in metadata kvpair declaration");
        }

        internal Tailcall ParseItemAsDictionary(ObjectNode context, string prefix = null) {
            bool justOneItem = context.Selector.Name == "" || context.Selector.Name == "*";

        
            switch (Type) {
                case TokenType.OpenBrace:
                    return ParseItemsInDictionary(context,prefix:prefix);
                    
                case TokenType.EmbeddedInstruction:
                    context.SetNodeValue( new Instruction(context, Token.Data, new SourceLocation(Token,Filename)));
                    if (NextAfter(WhiteSpaceOrComments) == TokenType.Semicolon) {
                        return Continue();
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected after Instruction. Looking for ';'");
                    
                case TokenType.ColonEquals:
                    // expecting one of:
                    //      - an object declaration { }
                    //      - an instruction 
                    //      - an RValue to be used as the value source for an object.
                    switch (NextAfter(WhiteSpaceOrComments)) {
                        case TokenType.OpenBrace:
                            return ParseItemsInDictionary(context, prefix: prefix);

                        case TokenType.EmbeddedInstruction:
                            context.SetNodeValue(new Instruction(context, Token.Data, new SourceLocation(Token, Filename)));
                            if (NextAfter(WhiteSpaceOrComments) == TokenType.Semicolon) {
                                return Continue();
                            }
                            throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected after Instruction. Looking for ';'");
                    }
                    // hmm. didn't seem to be one of the first two types. Try to grab it as an RValue then.
                    Rewind();
                    context.SetNodeValue(ParseRValue(context, Semicolon, context));
                    return Continue();
            }
            throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected after Identifier. Looking for '{' , ':=' or Instruction.");
        }

        /// <exception cref="ParseException">Token '{0}' not expected in object declaration</exception>
        private Tailcall ParseItemsInDictionary(ObjectNode context, Continuation onComplete = null, bool justOneItem = false, string prefix = null) {
            
            switch(NextAfter(justOneItem ? WhiteSpaceOrComments : WhiteSpaceCommentsOrSemicolons, onComplete != null)) {
                case TokenType.Identifier:
                    if (Data == "@alias") {
                        ParseAlias(context);
                        return ParseItemsInDictionary(context, onComplete,prefix:prefix);
                    }
                    break;

                case TokenType.Pound:
                    // metadata entry
                    ParseMetadataItem(context);
                    return ParseItemsInDictionary(context, onComplete, prefix: prefix);

                case TokenType.OpenParenthesis:
                    //var iValue = ParseMatrixForEach(context, Semicolon, new ObjectIterator(context, new SourceLocation(Token, Filename)){Prefix = prefix ?? (context.IndexValue-1).ToString()});
                    // iValue is the ObjectIterator.

                    var n = new ExpansionPropertyNode();
                    context.Properties[Guid.NewGuid().ToString()] = n; // unique node for the item
                    n.ObjectIterator = ParseMatrixForEach(context, Semicolon, new ObjectIterator(context, new SourceLocation(Token, Filename)) {
                        Prefix = prefix ?? (context.CurrentIndex).ToString()
                    }) as ObjectIterator;

                    return justOneItem ? (onComplete ?? Continue)() : ParseItemsInDictionary(context, onComplete, prefix: prefix);

                case TokenType.CloseBrace:
                    return (onComplete ?? Continue)();

                case TokenType.Eof:
                    // we should only get here if we discovered the EOF outside of anything.
                    return onComplete;
            }

            Rewind();

            var selector = ParseSelector(MemberTerminator);
            if (selector.Name.StartsWith(".")) {
                selector = new Selector( (prefix ?? string.Empty) + context.NextIndexValue + selector.Name, selector.Parameter, selector.SourceLocation);
            }

            switch (Type) {
                case TokenType.Dot:
                case TokenType.Identifier: {
                    // if we're pointing to an identifier or a dot, we're about to take whatever is next, and include it in a dictionary as a way of syntatic sugar.
                    return ParseItemsInDictionary(context.Children[selector], () => ParseItemsInDictionary(context, onComplete, prefix: prefix), true);
                }
                    
                case TokenType.OpenBrace: {
                    if (justOneItem) {
                        return ParseItemsInDictionary(context.Children[selector], (onComplete ?? Continue));
                    }
                    return ParseItemsInDictionary(context.Children[selector], () => ParseItemsInDictionary(context, onComplete, prefix: prefix));
                }

                case TokenType.Colon: {
                    var p = context.Properties[selector];
                    p.SetCollection(ParseRValue(context, SemicolonCommaOrCloseBrace, p));
                    return justOneItem ? (onComplete ?? Continue)() : ParseItemsInDictionary(context, onComplete, prefix: prefix);
                }

                case TokenType.PlusEquals: {
                    var p = context.Properties[selector];
                    p.AddToCollection(ParseRValue(context, Semicolon, p));
                    return justOneItem ? (onComplete ?? Continue)() : ParseItemsInDictionary(context, onComplete, prefix: prefix);
                }

                // same as the part in ParseItemAsDictionary()
                case TokenType.EmbeddedInstruction:
                    var n = context.Children[selector];
                    n.SetNodeValue(new Instruction(n, Token.Data, new SourceLocation(Token, Filename)));
                    if (NextAfter(WhiteSpaceOrComments) == TokenType.Semicolon) {
                        return justOneItem ? (onComplete ?? Continue)() : ParseItemsInDictionary(context, onComplete, prefix: prefix);
                    }
                    
                    throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected after Instruction. Looking for ';'");

                // same as the part in ParseItemAsDictionary()
                case TokenType.ColonEquals:
                    // expecting one of:
                    //      - an object declaration { }
                    //      - an instruction 
                    //      - an RValue to be used as the value source for an object.
                    switch (NextAfter(WhiteSpaceOrComments)) {
                        case TokenType.OpenBrace:
                            if (justOneItem) {
                                return ParseItemsInDictionary(context.Children[selector], (onComplete ?? Continue));
                            }
                            return ParseItemsInDictionary(context.Children[selector], () => ParseItemsInDictionary(context, onComplete, prefix: prefix));

                        case TokenType.EmbeddedInstruction:
                            var no = context.Children[selector];
                            no.SetNodeValue(new Instruction(no, Token.Data, new SourceLocation(Token, Filename)));
                            if (NextAfter(WhiteSpaceOrComments) == TokenType.Semicolon) {
                                return justOneItem ? (onComplete ?? Continue)() : ParseItemsInDictionary(context, onComplete, prefix: prefix);
                            }

                            throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected after Instruction. Looking for ';'");
                    }
                    // hmm. didn't seem to be one of the first two types. Try to grab it as an RValue then.
                    Rewind();
                    context.SetNodeValue(ParseRValue(context, Semicolon, context));

                    return justOneItem ? (onComplete ?? Continue)() : ParseItemsInDictionary(context, onComplete, prefix: prefix);

                case TokenType.Equal: {
                    var p = context.Properties[selector];
                    p.SetValue(ParseRValue(context, SemicolonCommaOrCloseBrace, p));
                    return justOneItem ? (onComplete ?? Continue)() : ParseItemsInDictionary(context, onComplete, prefix: prefix);
                }
            }
            throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected in object declaration");
        }

        private PropertyNode GetProperty(ObjectNode context, Selector selector) {
            PropertyNode item;

            if (context.ContainsKey(selector)) {
                item = context[selector] as PropertyNode;
                if (item == null) {
                    throw Fail(ErrorCode.ChildExists, "Can't create collection, child {0} is already declared as an object".format(selector));
                }
            } else {
                context[selector] = (item = new PropertyNode());
            }
            return item;
        }

        /// <exception cref="ParseException">Reached end-of-file inside a collection assignment declaration</exception>
        private IValue ParseRValue(ObjectNode context, TokenTypes terminators, INode metadataContainer) {
            switch (NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Colon:
                    // this had better be a global scope operator ::
                    if (NextType == TokenType.Colon) {
                        return ParseRValueLiterally(context, terminators, new Token {
                            Type = TokenType.Identifier,
                            Data = "::",
                            Column = Token.Column,
                            Row = Token.Row
                        }.SingleItemAsEnumerable());
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Leading colon in front of RValue is not permitted--did you mean '::' (global scope)");

                case TokenType.OpenParenthesis:
                    return ParseMatrixForEach(context, terminators, new Iterator(context, new SourceLocation(Token, Filename)));

                case TokenType.OpenBrace:
                    return ParseCollection(context, terminators, metadataContainer);

                case TokenType.EmbeddedInstruction:
                    var result = new Instruction(context, Data, new SourceLocation(Token,Filename));

                    if (NextAfter(WhiteSpaceOrComments) == TokenType.Lambda) {
                        // aha. Found a iterator expression.
                        return ExpectingForeachExpression(context, terminators, new Iterator(context, result));
                    }

                    if (terminators.Contains(Type)) {
                        return result;
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Expected terminator after instruction.");
            }
            Rewind();
            return ParseRValueLiterally(context, terminators);
        }

        /// <exception cref="ParseException">RValue missing, found '{0}' terminator.</exception>
        private IValue ParseRValueLiterally(ObjectNode context, TokenTypes terminators, Tokens tokens = null) {
            if (NextType == TokenType.Lambda) {
                // aha. Found a lambda expression.
                return ExpectingForeachExpression(context, terminators, new Iterator(context, new Scalar(context, tokens,Filename)));
            }

            if (terminators.Contains(Type)) {
                if (tokens.IsNullOrEmpty()) {
                    throw Fail(ErrorCode.MissingRValue, "RValue missing, found '{0}' terminator.");
                }
                return new Scalar(context, tokens,Filename);
            }
            
            return ParseRValueLiterally(context, terminators, tokens.ConcatHappily(Token.Type == TokenType.SelectorParameter ? new Token() {
                Column = Token.Column,
                Row = Token.Row,
                Data = StringExtensions.format("[{0}]",  Token.Data),
                Type = TokenType.StringLiteral,
            }  : Token));
        }

        /// <exception cref="ParseException"></exception>
        private IValue ParseCollection(ObjectNode context, TokenTypes outerTerminators, INode metadataContainer, Collection collection = null) {
            if (Type == TokenType.CloseBrace) {
                // the close brace indicates we're close to the end, but this could turn out to be an inline foreach 
                if (NextAfter(WhiteSpaceOrComments) == TokenType.Lambda) {
                    return ExpectingForeachExpression(context, outerTerminators, new Iterator(context, collection));
                }

                // token now should be the outerTerminator.
                if (outerTerminators.Contains(Type)) {
                    Rewind();
                    return collection  ?? new Collection(context);
                }
                // not terminated, but something else after the close brace? bad.
                throw Fail(ErrorCode.TokenNotExpected, "Expected foreach ('=>') or expression a terminator , found '{0}'");
            }
            // check for empty collection first.
            if (NextAfter(WhiteSpaceCommentsOrSemicolons) == TokenType.CloseBrace) {
                return collection ?? new Collection(context);
            }

            if (metadataContainer != null) {
                if (Type == TokenType.Pound) {
                    // metadata entry
                    ParseMetadataItem(context, metadataContainer);
                    // NextAfter(WhiteSpaceCommentsOrSemicolons);
                    var q = Next;
                    return ParseCollection(context, outerTerminators, metadataContainer, collection);
                }
            }

            Rewind();
            return ParseCollection(context, outerTerminators, metadataContainer, (collection ?? new Collection(context)).Add(ParseRValue(context, SemicolonCommaOrCloseBrace, metadataContainer)));
        }

        /// <exception cref="ParseException">Unrecognized token '{0}' in matrix foreach</exception>
        private IValue ParseMatrixForEach(ObjectNode context, TokenTypes terminators, Iterator rvalue) {
            rvalue.Add(ParseRValue(context, CommaOrCloseParenthesis, null));

            switch (Type) {
                case TokenType.CloseParenthesis:
                    return ExpectingForEach(context, terminators, rvalue);

                case TokenType.Comma:
                    return ParseMatrixForEach(context, terminators, rvalue);
            }
            throw Fail(ErrorCode.TokenNotExpected, "Unrecognized token '{0}' in matrix foreach");
        }

        /// <exception cref="ParseException">Unrecognized token '{0}' in matrix foreach</exception>
        private IValue ExpectingForEach(ObjectNode context, TokenTypes terminators, Iterator expression) {
            switch (NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Lambda:
                    if (expression is ObjectIterator) {
                        return ExpectingForeachObjectExpression(context, terminators, expression as ObjectIterator);
                    }
                    return ExpectingForeachExpression(context, terminators, expression);
            }
            throw Fail(ErrorCode.TokenNotExpected, "Unrecognized token '{0}' in matrix foreach");
        }

        private IValue ExpectingForeachExpression(ObjectNode context, TokenTypes terminators, Iterator rvalue) {
            switch(rvalue.Template.Count == 0 ? NextAfter(WhiteSpaceOrComments) : NextAfter(Comments)) {
                case TokenType.Lambda:
                    // chained foreach.
                    return ExpectingForeachExpression(context, terminators, new Iterator(context, rvalue));
            }
            if (terminators.Contains(Type)) {
                // we got to the end. finish up the expression and return it as the rvalue;
                return rvalue;
            }
            rvalue.Template.Add(Token.Data);
            return ExpectingForeachExpression(context, terminators, rvalue);
        }

        private IValue ExpectingForeachObjectExpression(ObjectNode context, TokenTypes terminators, ObjectIterator rvalue, int depth = 0) {
            // do we need the context? 

            var x = rvalue.Template.Count == 0 ? NextAfter(WhiteSpaceOrComments) : NextAfter(Comments);

            /*
            switch(rvalue.Template.Count == 0 ? NextAfter(WhiteSpaceOrComments) : NextAfter(Comments)) {
                case TokenType.Lambda:
                    // can't chain foreach object expressions.
                    throw Fail(ErrorCode.TokenNotExpected, "Object ForEach Expressions can not be nested.");
            }
             */

            if (depth == 0) {
                if (terminators.Contains(Type)) {
                    // we got to the end. finish up the expression and return it as the rvalue;
                    return rvalue;
                }
            } 

            switch (Type) {
                case TokenType.OpenBrace:
                    depth++;
                    break;
                case TokenType.CloseBrace:
                    depth--;
                    break;
            }

            rvalue.Template.Add(Token);
            return ExpectingForeachObjectExpression(context, terminators, rvalue,depth);
        }

        /// <exception cref="ParseException">Unexpected end of input.</exception>
        private TokenType NextAfter(TokenTypes tokenTypes, bool throwOnEnd = true) {
            return tokenTypes.Contains(Next.Type) ? NextAfter(tokenTypes, throwOnEnd) : ThrowIfEof(Type, throwOnEnd);
        }

        private TokenType NextAfter(TokenType tokenType, bool throwOnEnd = true) {
            return tokenType == NextType ? NextAfter(tokenType, throwOnEnd) : ThrowIfEof(Type, throwOnEnd);
        }

        /// <exception cref="ParseException">Unexpected end of input.</exception>
        internal TokenType ThrowIfEof(TokenType type, bool yes) {
            if (yes && type == TokenType.Eof) {
                throw Fail(ErrorCode.UnexpectedEnd, "Unexpected end of input.");
            }
            return type;
        }
    }

    // ReSharper restore PossibleMultipleEnumeration
}