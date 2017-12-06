#ifndef COUNTMAX_H
#define COUNTMAX_H
#include <stdint.h>
#include <ctype.h>
#include <locale.h>
#include <wchar.h>
#include <stddef.h>
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
static void countmax_line_update(struct countmax_line* this, struct flow_key* key, elemtype value);
static elemtype countmax_line_query(struct countmax_line* this, struct flow_key* key);

struct countmax_sketch {
    size_t w;
    size_t d;
    struct countmax_line** lines;
};

static struct countmax_sketch* new_countmax_sketch(int w, int d);
static void countmax_sketch_update(struct countmax_sketch* this, struct flow_key* key, elemtype value);
static elemtype countmax_sketch_query(struct countmax_sketch* this, struct flow_key* key);
#endif