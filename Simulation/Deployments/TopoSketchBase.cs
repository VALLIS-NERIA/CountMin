using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ElemType = System.Int64;

namespace Simulation.Deployments {
    public abstract class TopoSketchBase <T> : ITopoSketch<Flow, ElemType> where T : ISketch<object, ElemType> {
        public virtual int W { get; }
        public virtual string SketchClassName => typeof(T).DeclaringType.Name;

        public long MaxLoad => this.throughput.Values.Max();

        protected Topology topo;
        protected SketchFactory<T> factory;
        protected Dictionary<Switch, T> sketches;
        protected Dictionary<Switch, long> throughput;


        protected TopoSketchBase(SketchFactory<T> factory, Topology topo, int w) {
            this.factory = factory;
            this.topo = topo;
            this.W = w;
            this.sketches = this.DeploySketches();
            this.throughput = this.topo.Switches.ToDictionary(sw => sw, sw => 0L);
        }
        
        protected abstract Dictionary<Switch, T> DeploySketches();

        public virtual void Update(Flow key, ElemType value) {
            foreach (Switch sw in key.Switches) {
                if (!this.sketches.ContainsKey(sw)) continue;

                this.sketches[sw].Update(key, value);
                this.throughput[sw] += value;
            }
        }

        public virtual long Query(Flow key) {
            var res = new List<ElemType>();
            foreach (Switch sw in key.Switches) {
                if (!this.sketches.ContainsKey(sw)) continue;

                var q = this.sketches[sw].Query(key);
                if (q > 0) res.Add(q);
            }

            if (res.Count == 0) return 0;
            if (res.Count == 1) return res[0];
            return (ElemType) res.Max();
        }

        public virtual ElemType this[Flow key] => this.Query(key);
    }
}