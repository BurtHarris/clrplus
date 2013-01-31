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

namespace ClrPlus.Powershell.Provider.Commands {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using Base;
    using Core.Exceptions;
    using Core.Extensions;
    using Filesystem;
    using Microsoft.PowerShell.Commands;
    using Utility;

    [Cmdlet("Copy", "ItemEx", DefaultParameterSetName = "Selector", SupportsShouldProcess = true, SupportsTransactions = false)]
    public class CopyItemExCmdlet : CopyItemCommand {
        public static ILocationResolver GetLocationResolver(ProviderInfo providerInfo) {
            var result = providerInfo as ILocationResolver;
            if (result == null) {
                if (providerInfo.Name == "FileSystem") {
                    return new FilesystemLocationProvider(providerInfo);
                }
            }
            if (result == null) {
                throw new ClrPlusException("Unable to create location resolver for {0}".format(providerInfo.Name));
            }
            return result;
        }

        protected override void BeginProcessing() {
            Console.WriteLine("===BeginProcessing()===");
            base.BeginProcessing();
        }

        protected override void EndProcessing() {
            Console.WriteLine("===EndProcessing()===");
            base.EndProcessing();
        }

        protected virtual void Process(ProviderInfo sourceProvider, IEnumerable<string> sourcePaths, ProviderInfo destinationProvider, string destinationPath) {
        }

        protected override void ProcessRecord() {
            ProviderInfo destinationProviderInfo;


            // must figure out if the destination is on the same provider, even if the destination doesn't exist.

            var destinationPath = SessionState.Path.GetResolvedProviderPathFromPSPath(Destination, out destinationProviderInfo);

            var sources = Path.Select(each => {
                ProviderInfo spi;
                var sourceFiles = SessionState.Path.GetResolvedProviderPathFromPSPath(each, out spi);
                return new SourceSet {
                    ProviderInfo = spi,
                    SourcePaths = sourceFiles.ToArray(),
                };
            }).ToArray();

            var providerInfos = sources.Select(each => each.ProviderInfo).Distinct().ToArray();
            if (providerInfos.Length > 1 && providerInfos[0] == destinationProviderInfo) {
                Console.WriteLine("Using regular copy-item");
                base.ProcessRecord();
                return;
            }

            bool force = Force;

            // resolve where files are going to go.
            var destinationLocation = GetLocationResolver(destinationProviderInfo).GetLocation(destinationPath[0]);

            var copyOperations = ResolveSourceLocations(sources, destinationLocation).ToArray();

            if (copyOperations.Length > 1 && destinationLocation.IsFile) {
                // source can only be a single file.
                throw new ClrPlusException("Destination file exists--multiple source files specified.");
            }

            foreach (var operation in copyOperations) {
                Console.WriteLine("COPY '{0}' to '{1}'", operation.Source.AbsolutePath, operation.Destination.AbsolutePath);
                if (!force) {
                    if (operation.Destination.Exists) {
                        throw new ClrPlusException("Destination file '{0}' exists. Must use -force to override".format(operation.Destination.AbsolutePath));
                    }
                }

                using (var inputStream = new ProgressStream(operation.Source.Open(FileMode.Open))) {
                    using (var outputStream = new ProgressStream(operation.Destination.Open(FileMode.Create))) {
                        inputStream.BytesRead += (sender, args) => {};
                        outputStream.BytesWritten += (sender, args) => {};

                        inputStream.CopyTo(outputStream);
                    }
                }
            }

            Console.WriteLine("Done.");
        }

        internal virtual IEnumerable<CopyOperation> ResolveSourceLocations(SourceSet[] sourceSet, ILocation destinationLocation) {
            bool copyContainer = this.Container;

            foreach (var src in sourceSet) {
                var resolver = GetLocationResolver(src.ProviderInfo);
                foreach (var path in src.SourcePaths) {
                    var location = resolver.GetLocation(path);
                    var absolutePath = location.AbsolutePath;

                    if (!location.IsFile) {
                        // if this is not a file, then it should be a container.
                        if (!location.IsItemContainer) {
                            throw new ClrPlusException("Unable to resolve path '{0}' to a file or folder.".format(path));
                        }

                        // if it's a container, get all the files in the container
                        var files = location.GetFiles(Recurse);
                        foreach (var f in files) {
                            var relativePath = (copyContainer ? location.Name + @"\\" : "") + absolutePath.GetRelativePath(f.AbsolutePath);
                            yield return new CopyOperation {
                                Destination = destinationLocation.GetChildLocation(relativePath),
                                Source = f
                            };
                        }
                        continue;
                    }

                    yield return new CopyOperation {
                        Destination = destinationLocation.GetChildLocation(location.Name),
                        Source = location
                    };
                }
            }
        }

        protected override void StopProcessing() {
            Console.WriteLine("===StopProcessing()===");
            base.StopProcessing();
        }
    }
}