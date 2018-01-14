#ifndef FLOW_KEY_H
#define FLOW_KEY_H 1
// #include <stdint.h>
// #include <stdbool.h>
// #include <stddef.h>
#include <linux/types.h>
struct flow_key {
    __u32 srcip;
    __u32 dstip;
    __u16 srcport;
    __u16 dstport;
    __u16 protocol;
};
static int flow_key_hash(struct flow_key* key);
static int flow_key_equal(struct flow_key* lhs, struct flow_key* rhs);

#endif