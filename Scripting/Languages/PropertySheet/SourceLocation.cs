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
    using Core.Extensions;
    using Utility;

    public class SourceLocation {
        public static SourceLocation Unknown = new SourceLocation {
            Row = 0,
            Column = 0,
            SourceFile = null
        };

        public static SourceLocation[] Unknowns = new [] { Unknown};

        public string SourceFile {get; set;}

        private Token _sourceToken;

        public SourceLocation() {
        }

        public SourceLocation(Token t, string sourceFile = null) {
            _sourceToken = t;
            SourceFile = sourceFile;
        }

        public int Row {
            get {
                return _sourceToken.Row;
            }
            set {
                _sourceToken.Row = value;
            }
        }

        public int Column {
            get {
                return _sourceToken.Column;
            }
            set {
                _sourceToken.Column = value;
            }
        }

        public override string ToString() {
            return "{0}({1},{2})".format(SourceFile, Row, Column);
        }
    }
}