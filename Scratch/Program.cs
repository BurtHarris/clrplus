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

namespace Scratch {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using ClrPlus.Core.DynamicXml;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Core.Tasks;
    using ClrPlus.Powershell.Core;
    using ClrPlus.Scripting.Languages.PropertySheet;
    using ClrPlus.Scripting.Languages.PropertySheetV3;
    using ClrPlus.Scripting.MsBuild.Building;
    using ClrPlus.Scripting.MsBuild.Packaging;

    internal class Program {
        protected LocalEventSource LocalEventSource {
            get {
                var local = CurrentTask.Local;

                local.Events += new Error((code, message, objects) => {
                    Console.WriteLine("{0}:Error {1}".format(code, message.format(objects)));
                    return true;
                });

                local.Events += new Warning((code, message, objects) => {
                    Console.WriteLine("{0}:{1}".format(code, message.format(objects)));
                    return false;
                });

                local.Events += new Debug((code, message, objects) => {
                    Console.WriteLine("{0}: {1}".format(code, message.format(objects)));
                    return false;
                });

                local.Events += new Verbose((code, message, objects) => {
                    Console.WriteLine("{0} {1}".format(code, message.format(objects)));
                    return false;
                });

                local.Events += new Progress((code, progress, message, objects) => {
                    Console.WriteLine(new ProgressRecord(0, code, message.format(objects)) {
                        PercentComplete = progress
                    });
                    return false;
                });

                local.Events += new Message((code, message, objects) => {
                    Console.WriteLine("{0}:{1}".format(code, message.format(objects)));
                    return false;
                });
                return local;
            }
        }

        public object SomeLookup(string param) {
            return null;
        }

        private static void Main(string[] args) {
            new Program().Start(args);
        }

        private void foo() {
            Event<Warning>.Raise("123", "some Warning");
        }

        private void InlineTask(Action a) {
            Task.Factory.StartNew(a).Wait();
        }

        private void xStart(string[] args) {
            using (var local = LocalEventSource) {
                foo();
                local.Dispose();
            }
            foo();
        }

        private void zStart(string[] args) {
            var path = @"C:\Program Files (x86)\MSBuild\Microsoft.Cpp\v4.0\V110\1033\lib.xml";
            var doc = XDocument.Load(path);

            dynamic xml = new DynamicNode(doc);

            foreach (var property in xml) {
                string subType = property.Attributes.Has("Subtype") ? property.Attributes.Subtype : "";

                switch ((string)property.LocalName) {
                    case "BoolProperty":
                        Console.WriteLine(@"""{0}"".MapBoolean(),", property.Attributes.Name);
                        break;
                    case "StringListProperty":
                        switch (subType) {
                            case "folder":
                                Console.WriteLine(@"""{0}"".MapFolderList(),", property.Attributes.Name);
                                break;
                            case "file":
                                Console.WriteLine(@"""{0}"".MapFileList(),", property.Attributes.Name);
                                break;
                            case "":
                                Console.WriteLine(@"""{0}"".MapStringList(),", property.Attributes.Name);
                                break;
                            default:
                                throw new Exception("Unknown subtype:{0}".format(subType));
                        }
                        break;
                    case "IntProperty":
                        Console.WriteLine(@"""{0}"".MapInt(),", property.Attributes.Name);
                        break;
                    case "StringProperty":
                        switch (subType) {
                            case "folder":
                                Console.WriteLine(@"""{0}"".MapFolder(),", property.Attributes.Name);
                                break;
                            case "file":
                                Console.WriteLine(@"""{0}"".MapFile(),", property.Attributes.Name);
                                break;
                            case "":
                                Console.WriteLine(@"""{0}"".MapString(),", property.Attributes.Name);
                                break;
                            default:
                                throw new Exception("Unknown subtype:{0}".format(subType));
                        }
                        break;
                    case "EnumProperty":
                        var values = new List<string>();

                        foreach (var enumvalue in property) {
                            if (enumvalue.LocalName == "EnumProperty.Arguments") {
                                continue;
                            }
                            values.Add(enumvalue.Attributes.Name);
                        }
                        Console.WriteLine(@"""{0}"".MapEnum({1}),", property.Attributes.Name, values.Select(each => @"""" + each + @"""").Aggregate((current, each) => current + ",  " + each));
                        break;

                    case "Rule.Categories":
                    case "Rule.DataSource":
                        break;

                    default:
                        Console.WriteLine("==============================UNKNOWN TYPE: {0}", property.LocalName);
                        break;
                }
            }
        }

        private void Start(string[] args) {
            CurrentTask.Events += new SourceError((code, location, message, objects) => {
                location = location ?? SourceLocation.Unknowns;
                Console.WriteLine("{0}:Error {1}:{2}", location.FirstOrDefault(), code, message.format(objects));
                return true;
            });

            CurrentTask.Events += new SourceWarning((code, location, message, objects) => {
                location = location ?? SourceLocation.Unknowns;
                Console.WriteLine("{0}:Warning {1}:{2}", location.FirstOrDefault(), message.format(objects));
                return false;
            });

            CurrentTask.Events += new SourceDebug((code, location, message, objects) => {
                location = location ?? SourceLocation.Unknowns;
                Console.WriteLine("{0}:DebugMessage {1}:{2}", location.FirstOrDefault(), code, message.format(objects));
                return false;
            });

            CurrentTask.Events += new Error((code, message, objects) => {
                Console.WriteLine("{0}:Error {1}", code, message.format(objects));
                return true;
            });

            CurrentTask.Events += new Warning((code, message, objects) => {
                Console.WriteLine("{0}:Warning {1}", code, message.format(objects));
                return false;
            });

            CurrentTask.Events += new Debug((code, message, objects) => {
                Console.WriteLine("{0}:DebugMessage {1}", code, message.format(objects));
                return false;
            });

            CurrentTask.Events += new Verbose((code, message, objects) => {
                Console.WriteLine("{0}:Verbose {1}", code, message.format(objects));  
                return false;
            });
            CurrentTask.Events += new Message((code, message, objects) => {
                Console.WriteLine("{0}:Message {1}", code, message.format(objects));
                return false;
            });

#if AUTOPKG

            try {
                Environment.CurrentDirectory = @"C:\project";
                Console.WriteLine("Package script");
                using (var script = new PackageScript("CppRestSDK.autopkg")) {
                    script.Save(PackageTypes.NuGet, false);
                }
            } catch (Exception e) {
                Console.WriteLine("{0} =>\r\n\r\nat {1}", e.Message, e.StackTrace.Replace("at ClrPlus.Scripting.Languages.PropertySheetV3.PropertySheetParser", "PropertySheetParser"));
            }
#else 
            try {
                // Environment.CurrentDirectory = @"C:\project";
                Console.WriteLine("Build script");
                using(var script = new BuildScript("test.buildinfo")) {
                    script.Execute();
                }
            }
            catch(Exception e) {
                Console.WriteLine("{0} =>\r\n\r\nat {1}", e.Message, e.StackTrace.Replace("at ClrPlus.Scripting.Languages.PropertySheetV3.PropertySheetParser", "PropertySheetParser"));
            }

#endif
            return;
            //
        }
    }

    [Cmdlet(AllVerbs.Add, "Nothing")]
    public class AddNothingCmdlet : PSCmdlet {
        protected override void ProcessRecord() {
            using (var ps = Runspace.DefaultRunspace.Dynamic()) {
                var results = ps.GetItemss("c:\\");
                foreach (var item in results) {
                    Console.WriteLine(item);
                }
            }
        }
    }
}