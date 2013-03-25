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

namespace ClrPlus.Powershell.Provider.Filesystem {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Provider;
    using Base;
    using Core.Exceptions;
    using Core.Extensions;
    using Platform;
    using Path = Utility.Path;

    public class FilesystemLocationProvider : ILocationResolver {
        private ProviderInfo _providerInfo;

        public FilesystemLocationProvider(ProviderInfo providerInfo) {
            _providerInfo = providerInfo;
        }

        public ILocation GetLocation(string path) {
            return new FilesystemLocation(Path.ParsePath(path));
        }
    }

    public class FilesystemLocation : ILocation {
        private Path _path;
        private Stream _stream;

        public FilesystemLocation(Path path) {
            _path = path;
        }

        public void Dispose() {
            Close();
        }

        public IContentReader GetContentReader() {
            throw new NotImplementedException();
        }

        public IContentWriter GetContentWriter() {
            throw new NotImplementedException();
        }

        public void ClearContent() {
            throw new NotImplementedException();
        }

        public void Delete(bool recurse) {
            throw new NotImplementedException();
        }

        public ILocation NewItem(string type, object newItemValue) {
            throw new NotImplementedException();
        }

        public ILocation Rename(string newName) {
            throw new NotImplementedException();
        }

        public ILocation Move(ILocation newLocation) {
            throw new NotImplementedException();
        }

        public IEnumerable<ILocation> Copy(ILocation newLocation, bool recurse) {
            throw new NotImplementedException();
        }

        public IEnumerable<ILocation> Copy(string newPath, bool recurse) {
            throw new NotImplementedException();
        }


        public void Close() {
            if (_stream != null) {
                _stream.Close();
                _stream = null;
            }
        }

        public string Name {
            get {
                return _path.Name;
            }
        }

        public string AbsolutePath {
            get {
                return _path.FilePath;
            }
        }

        public string Url {
            get {
                return _path.OriginalPath;
            }
        }

        public string Type {
            get {
                return IsFile ? "file" : IsFileContainer ? "directory" : "unknown";
            }
        }

        public long Length {
            get {
                return IsFile ? new FileInfo(_path.FilePath).Length : -1;
            }
        }

        public DateTime TimeStamp {
            get {
                return IsFile ? new FileInfo(_path.FilePath).LastWriteTime : DateTime.MinValue;
            }
        }

        public bool Exists {
            get {
                return File.Exists(_path.FilePath) || Directory.Exists(_path.FilePath);
            }
        }

        public bool IsFile {
            get {
                return File.Exists(_path.FilePath);
            }
        }

        public bool IsFileContainer {
            get {
                return Directory.Exists(_path.FilePath);
            }
        }

        public bool IsItemContainer {
            get {
                return Directory.Exists(_path.FilePath);
            }
        }

        public IEnumerable<ILocation> GetDirectories(bool recurse) {
            if (!IsFileContainer) {
                return Enumerable.Empty<ILocation>();
            }

            return _path.FilePath.DirectoryEnumerateFilesSmarter(recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Where(Directory.Exists).Select(each => new FilesystemLocation(Path.ParsePath(each)));
        }

        public IEnumerable<ILocation> GetFiles(bool recurse) {
            if (!IsFileContainer) {
                return Enumerable.Empty<ILocation>();
            }
            return _path.FilePath.DirectoryEnumerateFilesSmarter(recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Where(File.Exists).Select(each => new FilesystemLocation(Path.ParsePath(each)));
        }

        public Stream Open(FileMode mode) {
            switch (mode) {
                case FileMode.Create:
                case FileMode.CreateNew:
                case FileMode.Truncate:
                    return _stream = File.Open(_path.FilePath, FileMode.Create, FileAccess.Write, FileShare.Read);

                case FileMode.Open:
                    if (!IsFile) {
                        throw new ClrPlusException("Selector not found '{0}'".format(AbsolutePath));
                    }
                    return _stream = File.Open(_path.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            throw new ClrPlusException("Unsupported File Mode.");
        }

        public ILocation GetChildLocation(string relativePath) {
            return new FilesystemLocation(Path.ParsePath(AbsolutePath + "\\" + relativePath));
        }
    }
}