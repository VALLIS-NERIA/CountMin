using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Simulation;
using Tup = System.ValueTuple<double, double>;
using ElemType = System.Int64;
using Newtonsoft.Json;
using anal = System.Console;
using System.Net.Sockets;


namespace testtt {
    internal class Program {
        private static void Main() {
            var uc = new UdpClient(1024);
            var ep = new IPEndPoint(IPAddress.Any, 0);
            var data = uc.Receive(ref ep);
            var s = Encoding.ASCII.GetString(data);
            Console.WriteLine(s);
        }
    }
}