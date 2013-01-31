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
//------------------------------------------------------------  -----------

namespace ClrPlus.Powershell.Core.Service {
    using System;
    using System.Collections.Generic;
    
    using ServiceStack.ServiceInterface;
    using ServiceStack.ServiceInterface.Auth;



    public class CustomBasicAuthProvider : BasicAuthProvider {

        public override bool TryAuthenticate(IServiceBase authService, string userName, string password) {

            return RestService.TryAuthenticate(authService, userName, password);

        }

        public override void OnAuthenticated(IServiceBase authService, IAuthSession session, IOAuthTokens tokens, Dictionary<string, string> authInfo) {
            RestService.OnAuthenticated(authService, session, tokens, authInfo, SessionExpiry);
        }
    }

    public class CustomCredentialsAuthProvider : CredentialsAuthProvider {
        public override bool TryAuthenticate(IServiceBase authService, string userName, string password) {
            //Add here your custom auth logic (database calls etc)
            //Return true if credentials are valid, otherwise false
            return true;
        }

        public override void OnAuthenticated(IServiceBase authService, IAuthSession session, IOAuthTokens tokens, Dictionary<string, string> authInfo) {
            //Fill the IAuthSession with data which you want to retrieve in the app eg:
            session.FirstName = "garrett";
            session.Roles = new List<string>();
            session.Roles.Add("users");
            session.Roles.Add("admins");
            
            //...

            //Important: You need to save the session!
            authService.SaveSession(session, SessionExpiry);
        }
    }
}