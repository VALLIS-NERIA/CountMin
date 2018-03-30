#include "util.h"



static void inline u32_swap(void* a, void* b, int size) {
    uint32_t t = *(uint32_t *)a;
    *(uint32_t *)a = *(uint32_t *)b;
    *(uint32_t *)b = t;
}

static inline void generic_swap(void* _a, void* _b, int size) {
    char t;
    char* a = (char*)_a;
    char* b = (char*)_b;
    do {
        t = *(char *)a;
        *(char *)a++ = *(char *)b;
        *(char *)b++ = t;
    }
    while (--size > 0);
}

void sort(void* _base, size_t num, size_t size,
          int (*cmp_func)(const void*, const void*),
          void (*swap_func)(void*, void*, int)) {
    char* base = (char*)_base;
    /* pre-scale counters for performance */
    int i = (num / 2 - 1) * size, n = num * size, c, r;

    if (!swap_func)
        swap_func = (size == 4 ? u32_swap : generic_swap);

    /* heapify */
    for (; i >= 0; i -= size) {
        for (r = i; r * 2 + size < n; r = c) {
            c = r * 2 + size;
            if (c < n - size &&
                cmp_func(base + c, base + c + size) < 0)
                c += size;
            if (cmp_func(base + r, base + c) >= 0)
                break;
            swap_func(base + r, base + c, size);
        }
    }

    /* sort */
    for (i = n - size; i > 0; i -= size) {
        swap_func(base, base + i, size);
        for (r = 0; r * 2 + size < i; r = c) {
            c = r * 2 + size;
            if (c < i - size &&
                cmp_func(base + c, base + c + size) < 0)
                c += size;
            if (cmp_func(base + r, base + c) >= 0)
                break;
            swap_func(base + r, base + c, size);
        }
    }
}
