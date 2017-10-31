using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    public class Switch {
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
        }

        private static Random rnd = new Random();

        public string Name { get; set; }

        private LinkDict linkLoad;

        public LinkDict LinkLoad {
            get => linkLoad;
            set => linkLoad = value;
        }

        public List<Switch> LinkedSwitches { get; private set; }

        public bool IsEdge { get; set; }

        public override string ToString() { return Name; }

        public Switch(string name = "unnamed switch", bool isEdge = false) {
            this.Name = name;
            this.IsEdge = isEdge;
            this.LinkedSwitches = new List<Switch>();
            this.LinkLoad = new LinkDict() {sw = this};
        }

        public void Link(Switch sw) {
            if (!this.LinkedSwitches.Contains(sw))
                this.LinkedSwitches.Add(sw);
            if (!sw.LinkedSwitches.Contains(this))
                sw.LinkedSwitches.Add(this);
        }


        public Switch RandomLinkedSwitch() { return LinkedSwitches[rnd.Next(LinkedSwitches.Count)]; }

        public void AssignTraffic(Switch nearby, double traffic) { LinkLoad[nearby] += traffic; }
    }
}