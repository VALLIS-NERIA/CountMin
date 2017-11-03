using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;

namespace Simulation {
    using ElemType = System.UInt64;

    class SketchVisor :ISketch<Flow,ElemType> {
        protected struct Entry {
            public Flow f;
            public ElemType e;
            public ElemType r;
            public ElemType d;
        }

        protected ElemType ComputeThresh(IEnumerable<ElemType> list) {
            var l1 = list.OrderByDescending(a => a);
            var a1 = l1.First();
            var a2 = l1.ElementAt(1);
            var ak = l1.Last();
            var b = (double) (a1 - 1) / (a2 - 1);
            var theta = Math.Log(0.5, b);

        }

        public void Update(Flow key, ElemType value) { throw new NotImplementedException(); }
        public ElemType Query(Flow key) { throw new NotImplementedException(); }

        public ElemType this[Flow key] => Query(key);
    }
}
