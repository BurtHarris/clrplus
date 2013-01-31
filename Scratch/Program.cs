using System.Management.Automation;
using System.Management.Automation.Runspaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scratch {
    using System.Threading;
    using System.Threading.Tasks;
    using ClrPlus.Core.Tasks;
    using ClrPlus.Networking;
    using ClrPlus.Powershell.Core;
    using ClrPlus.Scripting.Languages.PropertySheetV3;

#if FALSE

    class MSBuildFile {
        
    }

    class Pivot {
        string Key;
        Dictionary<string, string[]> Choices;

    }

    class Package {
        Dictionary<string, Pivot> Pivots;
        MSBuildFile Operations;
    }

    internal class MSBuildPropertyModel : PropertyModel {
        public MSBuildPropertyModel(MSBuildFile buildFile) {
            
        }

        public override bool CanAddProperty(string p) {
            throw new NotImplementedException();
        }

        public override bool HasProperty(string p) {
            throw new NotImplementedException();
        }

        public override PropertyModel GetPropertyReference(Selector selector) {
            var result = base.GetPropertyReference(selector);
            if (result == null) {
                
            }
            return result;
        }

        public override object Value {get; set;}

        public virtual bool SetProperty(string p, object o) {
            throw new NotImplementedException();
        }

        public virtual bool ClearProperty(string p) {
            throw new NotImplementedException();
        }

        public override bool IsList {
            get {
                throw new NotImplementedException();
            }
        }

        public override bool IsArray { get {
            return false;
        } }

        public override bool Add(object value) {
            throw new NotImplementedException();
        }

        public override bool IsDictionary {
            get {
                throw new NotImplementedException();
            }
        }

        public override bool Set(object key, object value) {
            throw new NotImplementedException();
        }
    };


    internal class Program {
        private static void Main(string[] args) {
            var p = new Package();

            PropertySheet.Load("test.props", new DynamicPropertyModel(p) {
                Strict = false,
                Handlers = new Dictionary<Type, ModelFactory> {
                    {typeof (MSBuildFile), obj => new MSBuildPropertyModel(obj as MSBuildFile) }
                }
            });
        }
    }
#endif

    class ProjectModel : PropertyModel {
       
    }

    internal class Program {
        private static void Main(string[] args) {
            try {
                var tests = new[] {
                    @"tests\pass\Alias_decl.txt", @"tests\pass\Coll_ops.txt", @"tests\pass\Dict_ops.txt", @"tests\pass\test.txt"
                };

                foreach (var t in tests) {
                    var model = new PropertyModel();
                    model[new Selector { Name = "Project" }] = new ProjectModel();

                    Console.WriteLine("\r\n\r\n == TEST: {0} ==", t);
                    model.ParseFile(t);

                    // var ProjectNode = model["Project"];

                    // first pass, flatten 'whens'
                    //foreach (var item in ProjectNode ) {
                        
                    //}
                    
                    
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

