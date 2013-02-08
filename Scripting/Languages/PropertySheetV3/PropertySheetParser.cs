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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using Core.Collections;
    using Core.Extensions;
    using Platform;
    using Utility;
    using Tokens = System.Collections.Generic.IEnumerable<Utility.Token>;
    using TokenTypes = System.Collections.Generic.IEnumerable<Utility.TokenType>;


    using Tailcall = Continuation;
    public delegate Continuation Continuation();
    
    // ReSharper disable PossibleMultipleEnumeration
    public class PropertySheetParser {

        protected readonly Tailcall Done;
        protected readonly Tailcall Invalid;
        
        private readonly IModel _rootModel;
        private readonly IEnumerator<Token> _enumerator;

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
                return _token =  new Token {
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

        protected TokenType Type { get {
            return _token.Type;
        }}

        protected string Data { get {
            return _token.Data.ToString();
        }}

        protected Token Token { get {
            return _token;
        } }

        protected readonly string Filename;

        internal PropertySheetParser(IEnumerable<Token> tokens, IModel rootModel, string filename) {
            Filename = filename;
            _enumerator = tokens.GetEnumerator();
            
            _rootModel = rootModel;

            Done = () => null;
            Invalid = () => null;

        }

        internal void Parse() {
            Global();
        }
       
        public static readonly TokenTypes Semicolon = new[] { TokenType.Semicolon };
        public static readonly TokenTypes Comma = new[] { TokenType.Comma };
        public static readonly TokenTypes CommaOrCloseBrace = new[] { TokenType.Comma, TokenType.CloseBrace  };
        public static readonly TokenTypes CommaOrCloseParenthesis = new[] { TokenType.Comma, TokenType.CloseParenthesis }; 
        public static readonly TokenTypes SemicolonOrComma = new[] { TokenType.Semicolon , TokenType.Comma };
        public static readonly TokenTypes SemicolonCommaOrCloseBrace = new[] { TokenType.Semicolon, TokenType.Comma, TokenType.CloseBrace,  };
        public static readonly TokenTypes OpenBrace = new[] { TokenType.OpenBrace };
        public static readonly TokenTypes MemberTerminator = new[] { TokenType.OpenBrace, TokenType.Colon, TokenType.PlusEquals, TokenType.Equal };
        public static readonly TokenTypes Equal = new[] { TokenType.Equal };

        public static readonly TokenTypes Comments = new[] {TokenType.LineComment , TokenType.MultilineComment };
        public static readonly TokenTypes WhiteSpaceOrComments = Comments.UnionA(TokenType.WhiteSpace);
        public static readonly TokenTypes WhiteSpaceCommentsOrSemicolons = WhiteSpaceOrComments.UnionA(TokenType.Semicolon);

        protected void Rewind() {
            _rewind = true;
        }

        protected T Rewind<T>(T t) {
            Rewind();
            return t;
        }

        protected ParseException Fail(ErrorCode code, string format) {
            return new ParseException(Token, Filename, code, format, Data);
        }
        
        /// <exception cref="ParseException">Collections must be nested in an object -- expected one of '.' , '#', '@alias' or identifier</exception>
        Tailcall Global( Continuation onComplete = null ) {
            if ((onComplete ?? Continue)() == Done) {
                return Done;
            }

            switch(NextAfter(WhiteSpaceCommentsOrSemicolons,false)) {
                case TokenType.Eof:
                    return Done;

                case TokenType.Pound:
                    return Global(ParseMetadataItem(_rootModel));

                case TokenType.Identifier:
                case TokenType.Dot:

                    switch(Data) {
                        case "@import":
                            return Global(ParseImport());
                        case "@alias" :
                            return Global(ParseAlias(_rootModel));
                    }

                    Rewind();
                    return Global(ParseItemsInDictionary(_rootModel[ParseSelector(OpenBrace)]));

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
                    Console.WriteLine("found ;");
                    return Global(onComplete);
            }
            throw Fail(ErrorCode.TokenNotExpected, "Unexpected Token '{0}' -- expected one of '.' , '#', '@alias' or identifier");
        }

        /// <exception cref="ParseException">Invalid token in selector declaration after < >  or [ ] --found '{0}'</exception>
        /// <exception cref="ParseException">Duplicate < > instruction not permitted.</exception>
        /// <exception cref="ParseException">Duplicate [ ] parameter not permitted.</exception>
        /// <exception cref="ParseException">Reached terminator '{0}' -- expected selector declaration</exception>
        private Selector ParseSelector(TokenTypes terminators, string selectorName = null, string instruction = null, string parameter = null) {
            switch (NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Identifier:
                case TokenType.Dot:
                    if (instruction != null || parameter != null) {
                        throw Fail(ErrorCode.TokenNotExpected, "Invalid token in selector declaration after < >  or [ ] --found '{0}'");
                    }
                    return ParseSelector(terminators, (selectorName ?? "") + Token.Data);
#if false
                case TokenType.EmbeddedInstruction:
                    if (instruction != null) {
                        throw Fail(ErrorCode.TokenNotExpected, "Duplicate < > instruction not permitted.");
                    }
                    return ParseSelector(terminators, selectorName, Token.Data, parameter);
#endif 
                case TokenType.SelectorParameter:
                    if(parameter!= null) {
                        throw Fail(ErrorCode.TokenNotExpected, "Duplicate [ ] parameter not permitted.");
                    }
                    return ParseSelector(terminators, selectorName , instruction, Token.Data);

                default:
                    if(terminators.Contains(Type)) {
                        if(string.IsNullOrEmpty(selectorName)) {
                            throw Fail(ErrorCode.InvalidSelectorDeclaration, "Reached terminator '{0}' -- expected selector declaration");
                        }
                        return new Selector {
                            Name = selectorName,
                            Parameter = parameter,
                            //Instruction = instruction,
                        };
                    }
                    break; // fall thru to end fail.
            }
            throw Fail(ErrorCode.TokenNotExpected, "Invalid token in selector declaration--found '{0}'");
        }

        /// <exception cref="ParseException">@import filename must not be empty.</exception>
        Tailcall ParseImport(Tokens path = null) {
            switch( path == null ? NextAfter(WhiteSpaceOrComments) : NextType ) {

                case TokenType.Semicolon:
                    if(path.IsNullOrEmpty()) {
                        throw Fail(ErrorCode.InvalidImport, "@import filename must not be empty.");
                    }

                    return ImportFile(ParsingExtensions.ToString(path));

                case TokenType.StringLiteral:
                    if (path.IsNullOrEmpty()) {
                        var filename = Data;
                        if (TokenType.Semicolon != NextAfter(WhiteSpaceOrComments) ) {
                            throw Fail(ErrorCode.TokenNotExpected, "Unexpected token '{0}' before ';' in @import directive");
                        }
                        return ImportFile(filename);
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Found string literal, expected unquoted token in @import directive (don't embed quotes in the middle of the filename");

            }
            
            int n;
            if((n = Data.IndexOfAny(Path.GetInvalidPathChars())) > -1) {
                throw Fail(ErrorCode.InvalidImport, "invalid character '{0} in @import filename.".format(Data.Substring(n, 1)));
            }
            return ParseImport(path.ConcatHappily(Token));
        }

        Tailcall ParseMetadataItem(INode modelProperty, IHasMetadata metadataContainer = null) {
            metadataContainer = metadataContainer ?? modelProperty as IHasMetadata;

            switch(NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Identifier:
                    var identifier = Data;
                    switch(NextAfter(WhiteSpaceOrComments)) {
                        case TokenType.Equal:
                            metadataContainer.Metadata.AddOrSet(identifier, ParseRValue(modelProperty, Semicolon,null));
                            //modelProperty.AddMetadata(identifier, ParseRValue(modelProperty, Semicolon));
                            return Continue;

                        case TokenType.Colon:
                            // should we really support this?
                            metadataContainer.Metadata.AddOrSet(identifier, ParseRValue(modelProperty, Semicolon,null));
                            //modelProperty.AddMetadata(identifier, ParseRValue(modelProperty, Semicolon));
                            return Continue;

                        case TokenType.OpenBrace:
                            var metadata = ParseMetadataObject();

                            if (metadata != null && metadata.Count > 0) {
                                foreach(var key in metadata.Keys) {
                                    metadataContainer.Metadata.AddOrSet(identifier, metadata[key]);
                                }
                                //modelProperty.AddMetadata(identifier, metadata);
                            }
                            return Continue;
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Expected '=' in metadata declaration, found {0}");

            }
            throw Fail(ErrorCode.TokenNotExpected, "Expected identifier in metadata declaration, found {0}");
        }

        /// <exception cref="ParseException">Missing alias declaration in @import statement</exception>
        Tailcall ParseAlias(INode modelProperty) {
            switch (NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Identifier:
                    var identifier = Data;
                    switch (NextAfter(WhiteSpaceOrComments)) {
                        case TokenType.Equal:
                            modelProperty.AddAlias(identifier, ParseSelector(Semicolon));
                            return Continue;
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Expected '=' in alias declaration, found {0}");

            }
            throw Fail(ErrorCode.TokenNotExpected, "Expected identifier in alias declaration, found {0}");
        }

        Tailcall ImportFile(string path) {
            // imports a file.
            _rootModel.ImportFile(path);
            
            return Continue;
        }


        /// <exception cref="ParseException">Token '{0}' not expected in object declaration</exception>
        XDictionary<string, RValue> ParseMetadataObject(XDictionary<string, RValue> result = null ) {
            if ( TokenType.CloseBrace == NextAfter(WhiteSpaceOrComments)) {
                return result;
            }

            Rewind();

            var selector = ParseSelector(Equal);

            // should be at the terminator still!
            switch(Type) {
                case TokenType.Equal:
                    result = result ?? new XDictionary<string, RValue>();
                    result.Add(selector.Name , ParseRValue(null, SemicolonCommaOrCloseBrace,null));
                    return ParseMetadataObject(result);

                case TokenType.Colon:
                    result = result ?? new XDictionary<string, RValue>();
                    result.Add(selector.Name, ParseRValue(null, SemicolonCommaOrCloseBrace,null));
                    return ParseMetadataObject(result);
                
            }
            throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected in metadata kvpair declaration");
        }

        /// <exception cref="ParseException">Token '{0}' not expected in object declaration</exception>
        Tailcall ParseItemsInDictionary(INode modelProperty, Continuation onComplete = null) {
            switch (NextAfter(WhiteSpaceCommentsOrSemicolons, onComplete != null )) {
                case TokenType.Identifier:
                    if (Data == "@alias") {
                        ParseAlias(modelProperty);
                        return ParseItemsInDictionary(modelProperty, onComplete);
                    }
                    break;

                case TokenType.Pound:
                    // metadata entry
                    ParseMetadataItem(modelProperty);
                    return ParseItemsInDictionary(modelProperty, onComplete);

                case TokenType.CloseBrace:
                    return (onComplete ?? Continue)();

                case TokenType.Eof:
                    // we should only get here if we discovered the EOF outside of anything.
                    return onComplete;
            }
           
            Rewind();

            var selector = ParseSelector(MemberTerminator);
            
            // should be at the terminator still!

            switch (Type) {
                case TokenType.OpenBrace:
                    return ParseItemsInDictionary(modelProperty[selector],  () => ParseItemsInDictionary(modelProperty, onComplete) );
                    
                case TokenType.Colon :
                    modelProperty.Properties[selector].SetCollection(ParseRValue(modelProperty, SemicolonCommaOrCloseBrace, modelProperty.Properties[selector] as IHasMetadata));
                    return ParseItemsInDictionary(modelProperty, onComplete);

                case TokenType.PlusEquals:
                    modelProperty.Properties[selector].AddToCollection(ParseRValue(modelProperty, Semicolon, modelProperty.Properties[selector] as IHasMetadata));
                    return ParseItemsInDictionary(modelProperty, onComplete);

                case TokenType.Equal:
                    modelProperty.Properties[selector].SetValue(ParseRValue(modelProperty, SemicolonCommaOrCloseBrace, modelProperty.Properties[selector] as IHasMetadata));
                    return ParseItemsInDictionary(modelProperty, onComplete);
            }
            throw Fail(ErrorCode.TokenNotExpected, "Token '{0}' not expected in object declaration");
        }

        /// <exception cref="ParseException">Reached end-of-file inside a collection assignment declaration</exception>
        RValue ParseRValue(INode modelProperty, TokenTypes terminators, IHasMetadata metadataContainer) {
            switch (NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Colon:
                    // this had better be a global scope operator ::
                    if (NextType == TokenType.Colon) {
                        return ParseRValueLiterally(terminators, new Token {
                            Type = TokenType.Identifier,
                            Data = "::",
                            Column = Token.Column,
                            Row = Token.Row
                        }.SingleItemAsEnumerable());
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Leading colon in front of RValue is not permitted--did you mean '::' (global scope)");

                case TokenType.OpenParenthesis:
                    return ParseMatrixForEach(modelProperty, terminators);

                case TokenType.OpenBrace:
                    return ParseCollection(modelProperty, terminators, metadataContainer);

                case TokenType.EmbeddedInstruction:
                    var result = new RVInstruction(Data);

                    if (NextAfter(WhiteSpaceOrComments) == TokenType.Lambda) {
                        // aha. Found a lambda expression.
                        return ExpectingForeachExpression(terminators, new RVIterator(result));
                    }

                    if (terminators.Contains(Type)) {
                        return result;
                    }
                    throw Fail(ErrorCode.TokenNotExpected, "Expected terminator after instruction.");
            }
            Rewind();
            return ParseRValueLiterally(terminators);
        }

        /// <exception cref="ParseException">RValue missing, found '{0}' terminator.</exception>
        RValue ParseRValueLiterally(TokenTypes terminators, Tokens tokens = null) {
            if (NextType == TokenType.Lambda) {
                // aha. Found a lambda expression.
                return ExpectingForeachExpression(terminators, new RVIterator(new RVSingle(tokens)));
            }

            if (terminators.Contains(Type)) {
                if (tokens.IsNullOrEmpty()) {
                    throw Fail(ErrorCode.MissingRValue, "RValue missing, found '{0}' terminator.");
                }
                return new RVSingle(tokens);
            }
            return ParseRValueLiterally(terminators, tokens.ConcatHappily(Token));
        }
        
        /// <exception cref="ParseException"></exception>
        RValue ParseCollection(INode modelProperty, TokenTypes outerTerminators, IHasMetadata metadataContainer, RVCollection collection = null) {
            if (Type == TokenType.CloseBrace) {
                // the close brace indicates we're close to the end, but this could turn out to be an inline foreach 
                if (NextAfter(WhiteSpaceOrComments) == TokenType.Lambda) {
                    return ExpectingForeachExpression(outerTerminators, new RVIterator(collection));
                }
                
                // token now should be the outerTerminator.
                if (outerTerminators.Contains(Type)) {
                    Rewind();
                    return collection;
                }
                // not terminated, but something else after the close brace? bad.
                throw Fail(ErrorCode.TokenNotExpected, "Expected foreach ('=>') or expression a terminator , found '{0}'");
            }
            // check for empty collection first.
            if (NextAfter(WhiteSpaceOrComments) == TokenType.CloseBrace) {
                return collection ?? new RVCollection();
            }

            if(metadataContainer != null) {
                if (Type == TokenType.Pound) {
                    // metadata entry
                    ParseMetadataItem(modelProperty, metadataContainer);
                    return ParseCollection(modelProperty, outerTerminators, metadataContainer, collection);
                }
            }

            Rewind();
            return ParseCollection(modelProperty, outerTerminators,metadataContainer, (collection ?? new RVCollection()).Add(ParseRValue(modelProperty, CommaOrCloseBrace,metadataContainer)));
        }

        /// <exception cref="ParseException">Unrecognized token '{0}' in matrix foreach</exception>
        RValue ParseMatrixForEach(INode modelProperty, TokenTypes terminators, RVIterator rvalue = null) {
            rvalue = rvalue ?? new RVIterator();
            rvalue.Sources.Add(new RVIterator.RVIteratorValueParameter(ParseRValue(modelProperty, CommaOrCloseParenthesis,null)));
            
            switch (Type) {
                case TokenType.CloseParenthesis:
                    return ExpectingForEach(terminators, rvalue);
                
                case TokenType.Comma:
                    return ParseMatrixForEach(modelProperty, terminators, rvalue);
            }
            throw Fail(ErrorCode.TokenNotExpected, "Unrecognized token '{0}' in matrix foreach");
        }

        /// <exception cref="ParseException">Unrecognized token '{0}' in matrix foreach</exception>
        RValue ExpectingForEach(TokenTypes terminators, RVIterator expression) {
            switch(NextAfter(WhiteSpaceOrComments)) {
                case TokenType.Lambda:
                    return ExpectingForeachExpression(terminators, expression);
            }
            throw Fail(ErrorCode.TokenNotExpected, "Unrecognized token '{0}' in matrix foreach");
        }

        RValue ExpectingForeachExpression(TokenTypes terminators, RVIterator rvalue) {
            switch(rvalue.Template.Length == 0 ? NextAfter(WhiteSpaceOrComments) : NextAfter(Comments)) {
                case TokenType.Lambda:
                    // chained foreach.
                    return ExpectingForeachExpression(terminators, new RVIterator(rvalue));
            }
            if (terminators.Contains(Type)) {
                // we got to the end. finish up the expression and return it as the rvalue;
                return rvalue;
            }
            rvalue.Template.Append(Token);
            return ExpectingForeachExpression(terminators, rvalue);
        }

        /// <exception cref="ParseException">Unexpected end of input.</exception>
        TokenType NextAfter( TokenTypes tokenTypes, bool throwOnEnd = true) {
            return tokenTypes.Contains(Next.Type) ? NextAfter(tokenTypes, throwOnEnd) : ThrowIfEof(Type,throwOnEnd);
        } 

        TokenType NextAfter(TokenType tokenType, bool throwOnEnd = true) {
            return tokenType == NextType ? NextAfter(tokenType, throwOnEnd) : ThrowIfEof(Type, throwOnEnd);
        }

        /// <exception cref="ParseException">Unexpected end of input.</exception>
        internal TokenType ThrowIfEof(TokenType type, bool yes) {
            if(yes && type == TokenType.Eof) {
                throw Fail(ErrorCode.UnexpectedEnd, "Unexpected end of input.");
            }
            return type;
        }
    }

    internal static class ParsingExtensions {
        internal static string ToString(this Tokens path) {
            return path.Aggregate("", (current, each) => current + each.Data.ToString());
        }

        internal static string AppendHappily(this string text , string appendText) {
            return string.IsNullOrEmpty(text) ? appendText : text + appendText;
        }

        internal static bool IsNullOrEmpty(this Selector reference) {
            return reference == null || string.IsNullOrEmpty(reference.Name);
        }
    }

    // ReSharper restore PossibleMultipleEnumeration
}
