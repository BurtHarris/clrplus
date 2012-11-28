﻿//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2011 Garrett Serack. All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License. 
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Networking.Extensions {
    using System;
    using System.Net;

    public static class WebRequestExtensions {
        public static WebResponse BetterEndGetResponse(this WebRequest request, IAsyncResult asyncResult) {
            try {
                return request.EndGetResponse(asyncResult);
            } catch (WebException wex) {
                if (wex.Response != null) {
                    return wex.Response;
                }
                throw;
            }
        }

        public static WebResponse BetterGetResponse(this WebRequest request) {
            try {
                return request.GetResponse();
            } catch (WebException wex) {
                if (wex.Response != null) {
                    return wex.Response;
                }
                throw;
            }
        }
    }
}