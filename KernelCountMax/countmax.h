#ifndef COUNTMAX_H
#define COUNTMAX_H
// #include <stdint.h>
#include "flow_key.h"
//#include <wchar.h>

typedef int64_t elemtype;
struct countmax_line {
    size_t w;
    uint32_t mask;
    struct flow_key* keys;
    elemtype* counters;
};
static struct countmax_line* new_countmax_line(int w);
static void countmax_line_update(struct countmax_line* this, struct flow_key* key, elemtype value);
static elemtype countmax_line_query(struct countmax_line* this, struct flow_key* key);
static void delete_countmax_line(struct countmax_line* this);

struct countmax_sketch {
    size_t w;
    size_t d;
    struct countmax_line** lines;
};

static struct countmax_sketch* new_countmax_sketch(int w, int d);
static void countmax_sketch_update(struct countmax_sketch* this, struct flow_key* key, elemtype value);
static elemtype countmax_sketch_query(struct countmax_sketch* this, struct flow_key* key);
static void delete_countmax_sketch(struct countmax_sketch* this);

struct countmax_manager {
    size_t w;
    size_t d;
    size_t sw_count;
    struct countmax_sketch** sketches;
};
static struct countmax_manager* new_countmax_manager(int w, int d, int sw_count);
static void countmax_manager_update(struct countmax_manager* this, int sw_id, struct flow_key* key, elemtype value);
static elemtype countmax_manager_query(struct countmax_manager* this, struct flow_key* key);
static void delete_countmax_manager(struct countmax_manager* this);
#endif