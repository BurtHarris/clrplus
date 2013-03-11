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
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using System.Management.Automation.Runspaces;
    using ClrPlus.Core.Collections;
    using ClrPlus.Powershell.Core;
    using ClrPlus.Scripting.Languages.PropertySheetV3;
    using ClrPlus.Scripting.Languages.PropertySheetV3.Mapping;
    using ClrPlus.Scripting.Languages.PropertySheetV3.RValue;

    class MSBuildProject {
        public int test;
        public string ouch;
    }

    public class nug {
        public string[] cars = new string[] { "ford", "honda", "gm" };
    }

    class Autopackage {
        public MSBuildProject Project = new MSBuildProject();
    }

    internal class Program {
        public object SomeLookup(string param) {
            return null;
        }

        private static void Main(string[] args) {
            new Program().Start(args);
        }

        public class Case {
            internal string Parameter;
            internal nug Project;

            private static XDictionary<string, Case> _cases = new XDictionary<string, Case>();

            public static IDictionary<string, Case> Create(nug parent) {
                
                return new DelegateDictionary<string, Case>(() => _cases.Keys , key =>
                    _cases.ContainsKey(key) ? _cases[key] : (_cases[key] = new Case {
                        Parameter = key,
                        Project = parent
                    }), (s,c) => _cases[s] = new Case{Parameter = s, Project = parent},_cases.Remove);
            }
        }

        private void Start(string[] args) {
            try {
                var n = new nug();

                var stuff = new Dictionary<string, string> {
                    {"abc", "item1"}, 
                    {"def", "item2"}
                };

                var tests = new[] {
                     @"tests\pass\test.txt" //, @"tests\pass\Alias_decl.txt"// @"tests\pass\Coll_ops.txt", @"tests\pass\Dict_ops.txt",
                };

                foreach (var t in tests) {
                    var autopkg = new Autopackage();

                    var ps = new PropertySheet(autopkg);
                    Console.WriteLine("\r\n\r\n=================[Parsing]==============");
                    ps.ParseFile(t);

                    Console.WriteLine("\r\n\r\n=================[Adding Routes]==============");
                   //  ps.Route( "Project.apple".MapTo( () => "applepropertyvalue", v => {} ));
                    ps.Route("nuget".MapTo(n,

                        "case".MapTo<nug,string,Case>(parent=> Case.Create(parent),  // <--  

 
                        // applies to every element of parent

                            "ItemDefinitionGroup".MapTo<Case>((Case => {                // when case is a parent, pass the case to this accessor.
                                // the key is the c.Parameter string.
                                // return (object)  "hello " + c.Parameter; // return the scoped item definition group 
                                return "hi";
                            }))
                            
                            ) 


                        ));
                   // ps.Route("dict".MapTo(stuff));
                    // ps.Route("outer.dic".MapTo(stuff));
                    var view = ps.View;

                    Console.WriteLine("\r\n\r\n=================[Using]==============");
                    // Console.WriteLine(view.Project);

                    
                    //Console.WriteLine("Sample? {0}",view.sample);
                    View v = view.nuget;
                    Console.WriteLine(v.Metadata.Keys.Aggregate((c, e) => c + ", " + e));
                    var n1 = view.nuget;
                    var n2 = n1.@case;
                    var n3 = n2["x86"];

                    
                    

                    var x86 = view.nuget.@case["x86"];
                    
                    Console.WriteLine(x86);
                    Console.WriteLine(x86.text);
                    Console.WriteLine(view.nuget.fudge.goo);


                    /*

                    Console.WriteLine("\r\n\r\n=================[0]==============");

                    Console.WriteLine(view.nuget);
                    view.CopyToModel();

                    
                    Console.WriteLine(view.nuget.sample);
                    Console.WriteLine("\r\n\r\n=================[1]==============");
                    Console.WriteLine(view.Project.ouch);
                    Console.WriteLine("\r\n\r\n=================[2]==============");
                    Console.WriteLine(view.Project.apple);
                    Console.WriteLine("\r\n\r\n=================[3]==============");
                    foreach(var i in view.nuget.cars) {
                        Console.WriteLine("Car: {0}", i);
                    }

                    Console.WriteLine("\r\n\r\n=================[4]==============");
                    Console.WriteLine(view.outer.dic["abc"]);
                    Console.WriteLine(view.dict["def"]);


                    var x86 = view.nuget.@case["x86"];
                    
                    Console.WriteLine(x86);
                    Console.WriteLine(x86.text);

                    Console.WriteLine("\r\n\r\n == TEST: {0} ==", t);

                    IDictionary<string, IValue> d = view.nuget.Metadata;
                    foreach (var k in d.Keys) {
                        Console.WriteLine("Metadata {0} => {1} ",k, d[k].Value);
                    }
*/
                }
            } catch (Exception e) {
                Console.WriteLine("{0} =>\r\n\r\nat {1}", e.Message, e.StackTrace.Replace("at ClrPlus.Scripting.Languages.PropertySheetV3.PropertySheetParser", "PropertySheetParser"));
            }
            return;
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
