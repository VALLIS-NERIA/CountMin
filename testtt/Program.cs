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
            int count = 0;
            //unsigned int 
            System.UInt32 x = 0;
            while (true) {
                var data = uc.Receive(ref ep);
                ++count;
                //Console.Write($"\r{++count}");
                //var s = Encoding.ASCII.GetString(data);
                //Console.WriteLine(ep.Address.ToString() + s);
            }
        }
    }
}