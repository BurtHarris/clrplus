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

namespace ClrPlus.Scripting.Languages.PropertySheet {
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using Utility;

    public enum TokenizerVersion {
        V2, 
        V3,
    }

    public class PropertySheetTokenizer : Tokenizer {
        /// <summary>
        ///     The list of keywords for Cascading Property Sheets
        /// </summary>
        private static readonly HashSet<string> CpsKeywords = new HashSet<string>();

        private TokenizerVersion _version = TokenizerVersion.V2;

        /// <summary>
        ///     Protected Constructor for tokenizing CPS code. Public access via the static Tokenize methods.
        /// </summary>
        /// <param name="text">the array of characters to tokenize.</param>
        /// <param name="version">Which version of the parser to conform to.</param>
        protected PropertySheetTokenizer(char[] text, TokenizerVersion version)
            : base(text) {
            _version = version;
            Keywords = CpsKeywords;
        }

        protected override bool IsCurrentCharacterIdentifierPartCharacter {
            get {
                if (CurrentCharacter == '-') {
                    return true;
                }

                return base.IsCurrentCharacterIdentifierPartCharacter;
            }
        }

        /// <summary>
        ///     Tokenizes the source code and returns a list of tokens
        /// </summary>
        /// <param name="text">The CPS source code to tokenize (as a string)</param>
        /// <returns>A List of tokens</returns>
        public new static List<Token> Tokenize(string text) {
            return Tokenize(string.IsNullOrEmpty(text) ? new char[0] : text.ToCharArray(), TokenizerVersion.V2);
        }

        public static List<Token> Tokenize(string text, TokenizerVersion version) {
            return Tokenize(string.IsNullOrEmpty(text) ? new char[0] : text.ToCharArray(), version);
        }

        public new static List<Token> Tokenize(char[] text) {
            return Tokenize(text, TokenizerVersion.V2);
        }

        /// <summary>
        ///     Tokenizes the source code and returns a list of tokens
        /// </summary>
        /// <param name="text">The CPS source code to tokenize (as an array of characters)</param>
        /// <param name="version">Version of the tokenizer to use.</param>
        /// <returns>A List of tokens</returns>
        public static List<Token> Tokenize(char[] text, TokenizerVersion version) {

            var tokenizer = new PropertySheetTokenizer(text, version);
            tokenizer.Tokenize();
            return tokenizer.Tokens;
        }

        protected override void ParsePound() {
            AddToken(Pound);
        }

        protected override void ParseDollar() {
            if (_version < TokenizerVersion.V3) {
                base.ParseDollar();
                return;
            }


            switch (NextCharacter) {
                case '{' :
                    // looks like the start of a new macro expression
                    // we're going to consume the whole macro 
                    var matchStack = 0;
                    var start = Index;
                    

                    while (CharsLeft > 0) {
                        AdvanceAndRecognize();

                        if(CurrentCharacter == '$' && NextCharacter == '{' ) {
                            matchStack++;
                        }

                        if(CurrentCharacter == '}' && matchStack == 0) {
                            break;
                        }

                        if(CurrentCharacter == '}' ) {
                            matchStack--;
                        }
                    }

                    // we've got a macro/expression token
                    AddToken(new Token{
                        Type = TokenType.MacroExpression,
                        Data = new string(Text, start, (Index - start)+1)
                    });
                    
                    return;
            }

            base.ParseDollar();
        }

       
        protected override bool PoachParse() {
            if (CurrentCharacter == '-') { // wtf?
            }

            var selectorParameter = PoachParseMatch('[', ']');
            if (selectorParameter != null) {
                AddToken( new Token {
                    Type = TokenType.SelectorParameter,
                    Data = selectorParameter
                });
                return true;
            }

            if (_version >= TokenizerVersion.V3) {
                if (CurrentCharacter == ':' && NextCharacter == '=') {
                    AdvanceAndRecognize();
                    AddToken(new Token {
                        Type = TokenType.ColonEquals,
                        Data = ":="
                    });
                    return true;
                }
            }

            if (_version >= TokenizerVersion.V3) {
                var instruction = PoachParseMatchWithAnchor('<', '>');
                if (instruction != null) {
                    AddToken(new Token {
                        Type = TokenType.EmbeddedInstruction,
                        Data = instruction
                    });
                    return true;
                }
            }
#if FALSE
            var squareStack = 0;

            if (CurrentCharacter == '[') {
                int start = Index + 1;
                while (CharsLeft > 0) {
                    AdvanceAndRecognize();

                    if (CurrentCharacter == '[') {
                        squareStack++;
                    }

                    if (CurrentCharacter == ']' && squareStack == 0) {
                        break;
                    }

                    if (CurrentCharacter == ']') {
                        squareStack--;
                    }
                }

                string selectorParameter = new string(Text, start, (Index - start)).Trim();
                AddToken(new Token {
                    Type = TokenType.SelectorParameter,
                    Data = selectorParameter
                });

                return true;
            }
#endif
            return false;
        }

        private static bool IsCharacterOkForAnchor(char ch) {
            switch (CharUnicodeInfo.GetUnicodeCategory(ch)) {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.Format:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.SpaceSeparator:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                case UnicodeCategory.Control:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.OpenPunctuation:
                case UnicodeCategory.ClosePunctuation:
                case UnicodeCategory.InitialQuotePunctuation:
                case UnicodeCategory.FinalQuotePunctuation:
                case UnicodeCategory.OtherNotAssigned:
                    return false;

                case UnicodeCategory.OtherSymbol:
                case UnicodeCategory.ModifierSymbol:
                case UnicodeCategory.MathSymbol:
                case UnicodeCategory.CurrencySymbol:
                case UnicodeCategory.EnclosingMark:
                case UnicodeCategory.OtherNumber:
                case UnicodeCategory.Surrogate:
                case UnicodeCategory.DashPunctuation:
                    return true;

                case UnicodeCategory.OtherPunctuation:
                    // manually blacklist some
                    switch (ch) {
                        case '!':
                        case '"':
                        case '\'':
                            return false;

                        default:
                            return true;
                    }
            }
            return false;
        }

        protected virtual string PoachParseMatch(char open, char close) {
            var matchStack = 0;

            if(CurrentCharacter == open) {
                int start = Index + 1;
                while(CharsLeft > 0) {
                    AdvanceAndRecognize();



                    if(CurrentCharacter == open) {
                        matchStack++;
                    }

                    if(CurrentCharacter == close && matchStack == 0) {
                        break;
                    }

                    if(CurrentCharacter == close) {
                        matchStack--;
                    }
                }
                return new string(Text, start, (Index - start)).Trim();
            }
            return null;
        }

        protected virtual string PoachParseMatchWithAnchor(char open, char close) {
            var matchStack = 0;
            List<char> anchor = null;
            if(CurrentCharacter == open) {
                int start = Index;

                // first, grab all the potential anchor characters
                while (CharsLeft > 0 & IsCharacterOkForAnchor(NextCharacter)) {
                    AdvanceAndRecognize();
                    anchor = anchor ?? new List<char>();
                    anchor.Add(CurrentCharacter);
                }

                if (null != anchor) {
                    // in anchor mode, we don't bother counting open and close characters
                    // we just look for the anchor sequence followed by the close character
                    anchor.Add(close);

                    var index = 0;

                    while(CharsLeft > 0 && index < anchor.Count) {
                        AdvanceAndRecognize();
                        index = CurrentCharacter == anchor[index] ? index + 1 : 0;
                    }
                    return new string(Text, (start + anchor.Count), ((Index - start) - (2*anchor.Count))).Trim();
                }

                start++;
                // no anchor, just stack match
                while(CharsLeft > 0) {
                    AdvanceAndRecognize();

                    if(CurrentCharacter == open) {
                        matchStack++;
                    }

                    if(CurrentCharacter == close && matchStack == 0) {
                        break;
                    }

                    if(CurrentCharacter == close) {
                        matchStack--;
                    }
                } 
                
                return new string(Text, start, (Index - start)).Trim();
               
            }
            return null;
        }

        /// <summary>
        ///     Handles the '@' case
        /// </summary>
        protected override void ParseOther() {
            var start = Index;
            if (CurrentCharacter == '@') {
                if (NextCharacter == '"') {
                    ParseAtStringLiteral();
                    return;
                }

                if (CharsLeft == 0) {
                    AddToken(new Token {
                        Type = TokenType.Unknown,
                        Data = "@"
                    });
                    return;
                }

                AdvanceAndRecognize();

                if (!IsCurrentCharacterIdentifierStartCharacter) {
                    AddToken(new Token {
                        Type = TokenType.Unknown,
                        Data = "@"
                    });
                    Index--; // rewind back to last character.
                    return;
                }
            }

            ParseIdentifier(start);
        }

        /// <summary>
        ///     Parses source code for a string starting with an at symbol ( @ )
        /// </summary>
        protected virtual void ParseAtStringLiteral() {
            // @"..."
            Index += 2;
            // var start = Index;

            RecognizeNextCharacter();
            var sb = new StringBuilder();

            while ((CurrentCharacter == '"' && NextCharacter == '"') || CurrentCharacter != '"') {
                if (CurrentCharacter == '"' && NextCharacter == '"') {
                    Index++;
                }

                sb.Append(CurrentCharacter);
                AdvanceAndRecognize();
            };

            AddToken(new Token {
                Type = TokenType.StringLiteral,
                Data = sb.ToString(),
                RawData = "@Literal"
            });
        }
    }
}