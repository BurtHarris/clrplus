using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClrPlus.Powershell.Core.Service {
    using System.Net;
    using ClrPlus.Core.Utility;
    using ServiceStack.Common.Web;
    using ServiceStack.ServiceClient.Web;
    using ServiceStack.Text;

    internal class CustomJsonServiceClient : JsonServiceClient {
        private dynamic @private;

        internal CustomJsonServiceClient(string baseUri) : base(baseUri) {
            @private = new AccessPrivateWrapper(this);
        }

        private WebRequest _sendRequest(string httpMethod, string requestUri, object request) {
            return @private.SendRequest(HttpMethods.Post, requestUri, request);
        }

        private bool _handleResponseException<TResponse>(Exception ex, object request, string requestUri, Func<WebRequest> createWebRequest, Func<WebRequest, WebResponse> getResponse, out TResponse response) {
            return @private.HandleResponseException(ex, request, requestUri, createWebRequest, getResponse, out response);
        }

        public override TResponse Send<TResponse>(object request) {
            var requestUri = SyncReplyBaseUri.WithTrailingSlash() + request.GetType().Name;
            var client = @private.SendRequest(requestUri, request);

            try {
                var webResponse = client.GetResponse();
                return @private.HandleResponse<TResponse>(webResponse);
            }
            catch (Exception ex) {
                // if it's not found, try again with just the uri... perhaps they have a custom uri.
                TResponse response;

                if (!_handleResponseException(ex,
                    request,
                    requestUri,
                    () => _sendRequest(HttpMethods.Post, requestUri, request),
                    c => c.GetResponse(),
                    out response)) {
                    throw;
                }

                return response;
            }
        }
    }
}
