#include <time.h>
#include <iostream>
static uint64_t a[1000] = {0};

int main() {

    int cycles = 0;
    std::cout << a[0] << '\n';
    char c;
    getchar();
    time_t t0 = time(nullptr);
loop:
    for (int i = 0; i < 1000; i++) {
        a[i]+=100;
    }
    if (!a[0]) {
        ++cycles;
    }
    goto loop;
    std::cout << cycles << " " << a[0] / 512 << '\n';
    system("pause");
}
