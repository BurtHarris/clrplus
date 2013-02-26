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
    using Core.Exceptions;
    using Core.Extensions;
    using Utility;

    public class ParseException : ClrPlusException {
        public Token Token;

        public ParseException(Token token, string filename, ErrorCode errorcode, string message, params object[] parameters)
            : base("{0}({1},{2}):PSP {3}:{4}".format(filename, token.Row, token.Column, (int)errorcode, message.format(parameters))) {
            Token = token;
        }
    }
}