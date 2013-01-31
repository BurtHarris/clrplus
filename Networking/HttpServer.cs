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

namespace ClrPlus.Networking {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Core.Collections;
    using Core.Extensions;

    public class HttpServer {
        private readonly string _host;
        private readonly int _port;
        private readonly HttpListener _listener = new HttpListener();
        private readonly IDictionary<string, string> _virtualDirs = new XDictionary<string, string>();

        public HttpServer(string host = "*", int port = 80) {
            _host = host.ToLower();
            _port = port;
        }

        public void AddVirtualDir(string prefix, string localPath) {
            if (string.IsNullOrEmpty(prefix)) {
                prefix = string.Empty;
            }

            prefix = ("/" + prefix + "/");
            while (prefix.IndexOf("//") > -1) {
                prefix = prefix.Replace("//", "/").ToLower();
            }
            _virtualDirs.Add(prefix, localPath);

            var listenerPrefix = string.Format("http://{0}:{1}{2}", _host, _port, prefix);
            _listener.Prefixes.Add(listenerPrefix);
        }

        public string GetLocalPath(Uri requestUri) {
            var lp = requestUri.LocalPath.ToLower();
            return (from vdPrefix in (from k in _virtualDirs.Keys orderby k.Length descending select k)
                let index = lp.IndexOf(vdPrefix)
                where index == 0
                let localPath = lp.Substring(vdPrefix.Length)
                select Path.Combine(_virtualDirs[vdPrefix], localPath)).FirstOrDefault();
        }

        public string GetDirectoryListing(string directory) {
            if (Directory.Exists(directory)) {
                var b = new StringBuilder();
                var di = new DirectoryInfo(directory);
                b.Append("Directory Listing:<br/></hr><table><tr style='font-weight:bold;'><td>Name</td><td style='text-align: center'>Date</td><td style='text-align: center'>Size</td></tr>");
                foreach (DirectoryInfo d in di.GetDirectories()) {
                    b.Append(string.Format("<tr><td>[<a href='{0}'>{0}</a>]</td><td style='text-align: right'>{1}</td><td style='text-align: right'></td></tr>\r\n", d.Name, d.LastWriteTime.ToString()));
                }
                foreach (FileInfo f in di.GetFiles()) {
                    b.Append(string.Format("<tr><td><a href='{0}'>{0}</a></td><td style='text-align: right'>{1}</td><td style='text-align: right'>{2}</td></tr>\r\n", f.Name, f.LastWriteTime.ToString(), f.Length));
                }
                b.Append("</table>");

                return b.ToString();
            }
            return null;
        }

        public DateTime GetLocationLastModified(string location) {
            return Directory.Exists(location) ? Directory.GetLastWriteTimeUtc(location)
                : File.Exists(location) ? File.GetLastWriteTimeUtc(location) : DateTime.Now;
        }

        private bool Exists(string location) {
            if (string.IsNullOrEmpty(location)) {
                return false;
            }
            return Directory.Exists(location) || File.Exists(location);
        }

        private long GetContentLength(string location) {
            if (Directory.Exists(location)) {
                return GetDirectoryListing(location).Length;
            }
            var fi = new FileInfo(location);
            return fi.Length;
        }

        /// <summary>
        /// Starts handling requests
        /// 
        /// This only handles one request at a time (since no threads are involved here)
        /// but handles it efficently.
        /// </summary>
        public async void Start() {
            _listener.Start();

            while (true) {
                try {
                    var context = await _listener.GetContextAsync();

                    var request = context.Request;
                    var response = context.Response;
                    var lp = GetLocalPath(request.Url);

                    switch (request.HttpMethod) {
                        case "HEAD":
                            if (Exists(lp)) {
                                response.AddHeader("Last-Modified", GetLocationLastModified(lp).ToString("r"));
                                response.ContentLength64 = GetContentLength(lp);
                            } else {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                            }
                            response.Close();

                            break;
                        case "GET":
                            if (!Exists(lp)) {
                                response.StatusCode = (int)HttpStatusCode.NotFound;
                                response.Close();
                                break;
                            }
                            response.AddHeader("Last-Modified", GetLocationLastModified(lp).ToString("r"));
                            response.ContentLength64 = GetContentLength(lp);
                            if (Directory.Exists(lp)) {
                                response.ContentType = "text/html";
                                var buf = GetDirectoryListing(lp).ToByteArray();
                                response.OutputStream.Write(buf, 0, buf.Length);
                                response.OutputStream.Flush();
                                response.Close();
                                break;
                            }

                            var data = File.ReadAllBytes(lp);
                            response.OutputStream.Write(data, 0, data.Length);
                            response.Close();
                            break;
                        case "POST":

                            break;

                        default:
                            Console.WriteLine("Unknown HTTP VERB : {0}", request.HttpMethod);
                            break;
                    }
                } catch (HttpListenerException) {
                    return;
                }
                catch(InvalidOperationException) {
                    return;
                }
                catch (Exception e) {
                    Console.WriteLine("HTTP Server Error: {0}--{1}", e.GetType().Name,e.Message);
                }
            }
        }

        public void Stop() {
            // _listener.Abort();
            _listener.Stop();
        }
    }
}