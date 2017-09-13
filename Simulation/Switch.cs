using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Simulation {
    public class Switch {
        private static Random rnd = new Random();

        public string Name { get; }

        public List<Switch> LinkedSwitches { get; private set; }

        public override string ToString() { return Name; }

        public Switch(string name = "unnamed switch", int portCount = 0) {
            this.Name = name;
            this.LinkedSwitches = new List<Switch>();
        }

        public void Link(Switch sw) {
            if (!this.LinkedSwitches.Contains(sw))
                this.LinkedSwitches.Add(sw);
            if (!sw.LinkedSwitches.Contains(this))
                sw.LinkedSwitches.Add(this);
        }

        public Switch RandomLinkedSwitch() { return LinkedSwitches[rnd.Next(LinkedSwitches.Count)]; }

    }
}