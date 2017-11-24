using System;

namespace testtt {
    public struct MyStruct  {
        public bool Equals(MyStruct other) {
            return this.srcip == other.srcip && this.dstip == other.dstip && this.srcport == other.srcport && this.dstport == other.dstport && this.protocol == other.protocol && this.counter == other.counter;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is MyStruct && Equals((MyStruct) obj);
        }

        public override int GetHashCode() {
            unchecked {
                int hashCode = (int) this.srcip;
                hashCode = (hashCode * 397) ^ (int) this.dstip;
                hashCode = (hashCode * 397) ^ (int) this.srcport;
                hashCode = (hashCode * 397) ^ (int) this.dstport;
                hashCode = (hashCode * 397) ^ (int) this.protocol;
                hashCode = (hashCode * 397) ^ (int) this.counter;
                return hashCode;
            }
        }

        public uint srcip;
        public uint dstip;
        public uint srcport;
        public uint dstport;
        public uint protocol;
        public uint counter;

        public static bool operator ==(MyStruct l, MyStruct r) { return l.Equals(r); }
        public static bool operator !=(MyStruct l, MyStruct r) { return !(l == r); }

        System.Collections.Generic.

    }
}