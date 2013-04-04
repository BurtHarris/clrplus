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

namespace ClrPlus.Powershell.Provider.Utility {
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Core.Collections;
    using Core.Extensions;

    public class Path {
        private static Regex UriRx = new Regex(@"^([a-zA-Z]+):([\\|/]*)(\w*.*)");
        private const char Slash = '\\';

        private static readonly char[] SingleSlashes = new[] {
            Slash
        };

        private static readonly char[] Colon = new[] {
            ':'
        };

        private static readonly char[] Slashes = new[] {
            '\\', '/'
        };

        private string _hostName;

        public string HostAndPort
        {
            get
            {
                return string.IsNullOrEmpty(_hostName) ? string.Empty : (Port == null ? _hostName : _hostName + ":" + Port);
            }
            set
            {
                HostName = value;
            }
        }
        public string Container;
        public string[] Parts;
        public string SubPath;
        public string ParentPath;
        public string Name;
        public bool StartsWithSlash;
        public bool EndsWithSlash;
        public uint? Port;
        public string Scheme;
        public string OriginalPath;

        public string FilePath {
            get {
                if (Scheme != "file") {
                    return "";
                }
                if (IsUnc) {
                    return @"\\{0}\{1}\{2}".format(HostName, Share, SubPath);
                }
                return @"{0}\{1}".format(Drive, SubPath);
            }
        }

        public string HostName {
            get {
                return _hostName;
            }
            set {
                var parts = value.Split(Colon, StringSplitOptions.RemoveEmptyEntries);
                _hostName = parts.Length > 0 ? parts[0] : string.Empty;

                if (parts.Length > 1) {
                    uint p;
                    uint.TryParse(parts[1], out p);
                    if (p > 0) {
                        Port = p;
                    }
                }
            }
        }

        public string Share {
            get {
                return Container;
            }
            set {
                Container = value;
            }
        }

        public bool HasDrive {
            get {
                return HostAndPort.Length == 2 && HostAndPort[1] == ':';
            }
        }

        public bool IsUnc {get; set;}

        public string Drive {get; set;}

        private static XDictionary<string, Path> _parsedLocationCache = new XDictionary<string, Path>();

        public static Path ParseWithContainer(Uri url) {
            return ParseWithContainer(url.AbsoluteUri);
        }

        public static Path ParseWithContainer(string path) {
            if (_parsedLocationCache.ContainsKey(path)) {
                return _parsedLocationCache[path];
            }

            var endswithslash = path.LastIndexOfAny(Slashes) == path.Length;

            var pathToParse = (path ?? string.Empty).UrlDecode();

            var match = UriRx.Match(pathToParse);
            if (match.Success) {
                pathToParse = match.Groups[3].Value;
            }

            var segments = pathToParse.Split(Slashes, StringSplitOptions.RemoveEmptyEntries);
            return _parsedLocationCache.AddOrSet(path, new Path {
                HostAndPort = segments.Length > 0 ? segments[0] : string.Empty,
                Container = segments.Length > 1 ? segments[1] : string.Empty,
                Parts = segments.Length > 2 ? segments.Skip(2).ToArray() : new string[0],
                SubPath = segments.Length > 2 ? segments.Skip(2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                ParentPath = segments.Length > 3 ? segments.Skip(2).Take(segments.Length - 3).Aggregate((current, each) => current + Slash + each) : string.Empty,
                Name = segments.Length > 2 ? segments.Last() : string.Empty,
                StartsWithSlash = pathToParse.IndexOfAny(Slashes) == 0,
                EndsWithSlash = endswithslash, // pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                OriginalPath = path,
            });
        }

        public static Path ParseUrl(Uri url) {
            return ParseUrl(url.AbsoluteUri);
        }

        public static Path ParseUrl(string path) {
            if (_parsedLocationCache.ContainsKey(path)) {
                return _parsedLocationCache[path];
            }

            var endswithslash = path.LastIndexOfAny(Slashes) == path.Length;

            var pathToParse = (path ?? string.Empty).UrlDecode();

            var match = UriRx.Match(pathToParse);
            if (match.Success) {
                pathToParse = match.Groups[3].Value;
            }

            var segments = pathToParse.Split(Slashes, StringSplitOptions.RemoveEmptyEntries);
            return _parsedLocationCache.AddOrSet(path, new Path {
                HostName = segments.Length > 0 ? segments[0] : string.Empty,
                Container = string.Empty,
                Parts = segments.Length > 1 ? segments.Skip(1).ToArray() : new string[0],
                SubPath = segments.Length > 1 ? segments.Skip(1).Aggregate((current, each) => current + Slash + each) : string.Empty,
                ParentPath = segments.Length > 2 ? segments.Skip(1).Take(segments.Length - 2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                Name = segments.Length > 1 ? segments.Last() : string.Empty,
                StartsWithSlash = pathToParse.IndexOfAny(Slashes) == 0,
                EndsWithSlash = endswithslash, //pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                OriginalPath = path,
            });
        }

        public static Path ParsePath(string path) {
            if (_parsedLocationCache.ContainsKey(path)) {
                return _parsedLocationCache[path];
            }

            var endswithslash = path.LastIndexOfAny(Slashes) == path.Length;

            var uri = new Uri((path ?? string.Empty).UrlDecode(), UriKind.RelativeOrAbsolute);

            var pathToParse = uri.IsAbsoluteUri ? uri.AbsoluteUri.UrlDecode() : uri.OriginalString;

            var match = UriRx.Match(pathToParse);
            if (match.Success) {
                pathToParse = match.Groups[3].Value;
            }

            var segments = pathToParse.Split(Slashes, StringSplitOptions.RemoveEmptyEntries);

            return uri.IsAbsoluteUri 
                ? uri.IsUnc
                    ? _parsedLocationCache.AddOrSet(path, new Path {
                        HostName = segments.Length > 0 ? segments[0] : string.Empty,
                        Share = segments.Length > 1 ? segments[1] : string.Empty,
                        Parts = segments.Length > 2 ? segments.Skip(2).ToArray() : new string[0],
                        SubPath = segments.Length > 2 ? segments.Skip(2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                        ParentPath = segments.Length > 3 ? segments.Skip(2).Take(segments.Length - 2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                        Name = segments.Length > 2 ? segments.Last() : string.Empty,
                        StartsWithSlash = endswithslash, // pathToParse.IndexOfAny(Slashes) == 0,
                        EndsWithSlash = pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                        Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                        IsUnc = true,
                    })
                    : _parsedLocationCache.AddOrSet(path, new Path {
                        Drive = segments.Length > 0 ? segments[0] : string.Empty,
                        Share = string.Empty,
                        Parts = segments.Length > 1 ? segments.Skip(1).ToArray() : new string[0],
                        SubPath = segments.Length > 1 ? segments.Skip(1).Aggregate((current, each) => current + Slash + each) : string.Empty,
                        ParentPath = segments.Length > 2 ? segments.Skip(1).Take(segments.Length - 2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                        Name = segments.Length > 1 ? segments.Last() : string.Empty,
                        StartsWithSlash = pathToParse.IndexOfAny(Slashes) == 0,
                        EndsWithSlash = endswithslash, //  pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                        Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                        IsUnc = false,
                        OriginalPath = uri.AbsoluteUri.UrlDecode(),
                    })
                : _parsedLocationCache.AddOrSet(path, new Path {
                    Drive = string.Empty,
                    Share = string.Empty,
                    Parts = segments.Length > 0 ? segments.ToArray() : new string[0],
                    SubPath = segments.Length > 0 ? segments.Aggregate((current, each) => current + Slash + each) : string.Empty,
                    ParentPath = segments.Length > 1 ? segments.Take(segments.Length - 2).Aggregate((current, each) => current + Slash + each) : string.Empty,
                    Name = segments.Length > 0 ? segments.Last() : string.Empty,
                    StartsWithSlash = pathToParse.IndexOfAny(Slashes) == 0,
                    EndsWithSlash = endswithslash, //  pathToParse.LastIndexOfAny(Slashes) == pathToParse.Length,
                    Scheme = match.Success ? match.Groups[1].Value.ToLower() : string.Empty,
                    IsUnc = false,
                    OriginalPath = uri.OriginalString,
                });
        }

        public void Validate() {
            if (Parts == null) {
                // not set from original creation
                SubPath = SubPath ?? string.Empty;
                Parts = SubPath.Split(Slashes, StringSplitOptions.RemoveEmptyEntries);
                ParentPath = Parts.Length > 1 ? Parts.Take(Parts.Length - 1).Aggregate((current, each) => current + Slash + each) : string.Empty;
                Name = Parts.Length > 0 ? Parts.Last() : string.Empty;
            }
        }

        public bool IsSubpath(Path childPath) {
            if (Parts.Length >= childPath.Parts.Length) {
                return false;
            }

            return !Parts.Where((t, i) => t != childPath.Parts[i]).Any();
        }
    }
}