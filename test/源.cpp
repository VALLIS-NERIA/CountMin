#include <time.h>
#include <iostream>
static uint64_t a[1000] = {0};
struct test {
    int get_hash_code() const {
        int hashCode = (int)srcip;
        hashCode = (hashCode * 397) ^ (int)dstip;
        hashCode = (hashCode * 397) ^ (int)srcport;
        hashCode = (hashCode * 397) ^ (int)dstport;
        hashCode = (hashCode * 397) ^ (int)protocol;
        return hashCode;
    }

     unsigned int srcip;
     unsigned int dstip;
     unsigned int srcport;
     unsigned int dstport;
     unsigned int protocol;
     unsigned int counter;
};
int main() {
    test a;
     a.get_hash_code();
//    int cycles = 0;
//    std::cout << a[0] << '\n';
//    char c;
//    time_t t0 = time(nullptr);
//loop:
//    for (int i = 0; i < 1000; i++) {
//        a[i]+=100;
//    }
//    if (!a[0]) {
//        ++cycles;
//    }
//    goto loop;
//    std::cout << cycles << " " << a[0] / 512 << '\n';
//    system("pause");
}
