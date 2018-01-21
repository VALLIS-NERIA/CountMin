#ifndef MY_UTIL
#define MY_UTIL

#ifndef __KERNEL__
#include <stdlib.h>
#include <stdint.h>
#define new(name) (name*)malloc(sizeof(name))
#define newarr(name, size) (name*)malloc(size * sizeof(name))
#define kfree free
#else

#include <linux/types.h>
#include <linux/slab.h>
#include <linux/random.h>
#include <linux/log2.h>
#include <linux/delay.h>
#include <linux/hash.h>
#define new(name) (name*)kzalloc(sizeof(name), GFP_KERNEL)
#define newarr(name, size) (name*)kzalloc(size * sizeof(name), GFP_KERNEL)
#define kfree kfree
#define log2(n) (uint32_t)__ilog2_u32(n)
#endif
typedef int64_t elemtype;

#define GOLDEN_RATIO_PRIME_32 0x9e370001UL

inline uint32_t sketch_hash_32(uint32_t val, unsigned int bits)
{
    /* On some cpus multiply is faster, on others gcc will do shifts */
    uint32_t hash = val * GOLDEN_RATIO_PRIME_32;

    /* High bits are more random, so use them. */
    return hash >> (32 - bits);
}
#ifdef _WIN32
inline int rand_byte() {
    return rand() & 0xff;
}
inline uint32_t rand_uint32() {
    uint32_t i = 0;
    i += rand_byte();
    i <<= 8;
    i += rand_byte();
    i <<= 8;
    i += rand_byte();
    i <<= 8;
    i += rand_byte();
    return i;
}

inline uint32_t rand_uint16() {
    uint16_t i = 0;
    i += rand_byte();
    i <<= 1;
    i += rand_byte();
    return i;
}
#else
inline int rand_byte(void) {
    char c;
    get_random_bytes(&c, 1);
    return c;
}

inline uint32_t rand_uint32(void) {
    uint32_t i = 0;
    get_random_bytes(&i, sizeof(uint32_t));
    return i;
}

inline uint16_t rand_uint16(void) {
    uint16_t i = 0;
    udelay(100);
    get_random_bytes(&i, sizeof(uint16_t));
    return i;
}
#endif



int cmpelem(const void* a, const void* b) {
    return *(elemtype *)a - *(elemtype *)b;
}

static void u32_swap(void *a, void *b, int size)
{
    uint32_t t = *(uint32_t *)a;
    *(uint32_t *)a = *(uint32_t *)b;
    *(uint32_t *)b = t;
}

static void generic_swap(void *_a, void *_b, int size)
{
    char t;
    char* a = (char*)_a;
    char* b = (char*)_b;
    do {
        t = *(char *)a;
        *(char *)a++ = *(char *)b;
        *(char *)b++ = t;
    } while (--size > 0);
}

void sort(void *_base, size_t num, size_t size,
    int(*cmp_func)(const void *, const void *),
    void(*swap_func)(void *, void *, int size))
{
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





void inline my_sort(void *base, size_t num, size_t size,
    int(*cmp)(const void *, const void *)) {
    sort(base, num, size, cmp, NULL);
}

#endif
