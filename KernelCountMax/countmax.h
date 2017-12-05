#ifndef COUNTMAX_H
#define COUNTMAX_H
#include <stdint.h>
typedef int64_t elemtype;
struct flow_key {
    uint32_t srcip;
    uint32_t dstip;
    uint16_t srcport;
    uint16_t dstport;
    uint16_t protocol;
};
static int get_hash_code(struct flow_key* key);
static int flow_key_equal(struct flow_key* lhs, struct flow_key* rhs);

struct countmax_line {
    size_t w;
    struct flow_key* keys;
    elemtype* counters;
};
static struct countmax_line* new_countmax_line(int w);
static void update(struct countmax_line* this, struct flow_key* key, elemtype value);
#endif