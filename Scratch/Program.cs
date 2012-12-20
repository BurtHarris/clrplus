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

    class Program {
        public delegate int Foo(string text);

        static void Main(string[] args) {
            using( var powershell = Runspace.DefaultRunspace.Dynamic() ) {

                var dirResults = powershell.dir(@"c:\root", recurse: false);
                Console.WriteLine("wait till that's done...");
                dirResults.Wait();
                Console.WriteLine("done...");

                var items = powershell.dir(@"c:\root\bin", recurse:false);
                foreach (var i in items) {
                    Console.WriteLine(i);
                }
            }

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        /*
        static async void Main2() {
            HttpServer server = new HttpServer();
            server.AddVirtualDir("foo",@"c:\root");
            server.Start();
            
            
            Console.WriteLine("Press enter to stop.");
            Console.ReadLine();
            server.Stop();

            Console.WriteLine("Press enter to exit");
            Console.ReadLine();
        }

        static async Task Task1() {
            XTask.Run(() => {
            
            CurrentTask.Events += new Foo((t) => {
                Console.WriteLine("Task 1 test message was: {0}", t);
                return 0;
            });

            Test();
            });
        }


        static async Task Task2() {
             
            var xyz =XTask.Run<int>(async () => {
                CurrentTask.Events += new Foo((t) => {
                    Console.WriteLine("Task 2 test message was {0}", t);
                    return 0;
                });
                await Test();
                return 0;
            });


            Console.WriteLine("before await");

            var zzz = await xyz;

            Console.WriteLine("after await");

            Thread.Sleep(5000);
        }

        static async Task Test() {
            Event<Foo>.RaiseFirst("(1) Early.");

            var x = await XTask.Run(() => {
                Event<Foo>.RaiseFirst("(2) In First Task");

                var y =  XTask.Run(() => {
                    Thread.Sleep(4000);
                    Event<Foo>.RaiseFirst("(3) After delay, In second Task");

                    return 5;
                });

                Event<Foo>.RaiseFirst("(4) In first task, after we made the second task");
                return 5;
            });

            Console.WriteLine("there {0}", x);
        }

        static Task Test2() {
            var x = Task.Factory.StartNew(() => {

                var y = Task.Factory.StartNew(() => {
                    Thread.Sleep(2000);
                    Event<Foo>.RaiseFirst("gws");
                    return 5;
                });

                Event<Foo>.RaiseFirst("garrett");
                
                return 5;
            });

            Console.WriteLine("there {0}", x.Result);
            return x;
        }
         * */
    }
}
