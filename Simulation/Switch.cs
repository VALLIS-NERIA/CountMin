using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    public class Switch {
        [Serializable]
        public class LinkDict : Dictionary<Switch, double> {
            public Switch sw;

            new public double this[Switch key] {
                
                get {
                    if (base.ContainsKey(key)) {
                        return base[key];
                    }
                    else {
                        if (sw.LinkedSwitches.Contains(key)) {
                            base.Add(key, 0);
                            return base[key];
                        }
                        else {
                            // Throws exception
                            return base[key];
                        }
                    }
                }

                set {
                    if (base.ContainsKey(key)) {
                        base[key] = value;
                    }
                    else {
                        if (sw.LinkedSwitches.Contains(key)) {
                            base.Add(key, value);
                        }
                        else {
                            // Throws exception
                            var foo = base[key];
                        }
                    }
                }
            }

            public override string ToString() {
                StringBuilder sb=new StringBuilder();
                foreach (var sw1 in sw.LinkedSwitches) {
                    sb.Append(sw.Name).Append(" -> ").Append(sw1.Name).Append(" : ").Append(this[sw1]).Append(Environment.NewLine);
                }
                return sb.ToString();
            }

        }

        private static Random rnd = new Random();

        public string Name { get; set; }

        public LinkDict LinkLoad { get; set; }

        public List<Switch> LinkedSwitches { get; private set; }

        public HashSet<Flow> PassingFlows { get; set; }

        public bool IsEdge { get; set; }

        public override string ToString() { return Name; }

        public Switch(string name = "unnamed switch", bool isEdge = false) {
            this.Name = name;
            this.IsEdge = isEdge;
            this.LinkedSwitches = new List<Switch>();
            this.LinkLoad = new LinkDict() {sw = this};
            this.PassingFlows = new HashSet<Flow>();
        }

        public void Link(Switch sw) {
            if (!this.LinkedSwitches.Contains(sw))
                this.LinkedSwitches.Add(sw);
            if (!sw.LinkedSwitches.Contains(this))
                sw.LinkedSwitches.Add(this);
        }

        public void RemoveFlow(Flow f) {
            if (PassingFlows.Contains(f)) {
                PassingFlows.Remove(f);
                if (f.OutgressSwitch == this)
                    return;
                var next = f.Nodes[f.Nodes.IndexOf(this) + 1];
                this.LinkLoad[next] -= f.Traffic;
            }
            else {
                throw new ArgumentException("This flow does not go through this switch");
            }
        }

        public void ClearFlow() { this.LinkLoad = new LinkDict() {sw = this}; }
        public void AssignFlow(Flow f) {
            if (PassingFlows.Contains(f)) {
                throw new ArgumentException();
            }
            PassingFlows.Add(f);
            if (f.OutgressSwitch == this)
                return;
            var next = f.Nodes[f.Nodes.IndexOf(this) + 1];
            this.LinkLoad[next] += f.Traffic;
        }

        public Switch RandomLinkedSwitch() { return LinkedSwitches[rnd.Next(LinkedSwitches.Count)]; }

        public void AssignTraffic(Switch nearby, double traffic) { LinkLoad[nearby] += traffic; }
    }
}