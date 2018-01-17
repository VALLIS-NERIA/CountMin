#ifndef FLOW_KEY_H
#define FLOW_KEY_H 1
// #include <stdint.h>
// #include <stdbool.h>
// #include <stddef.h>
#include <linux/types.h>
#include <linux/hash.h>
struct flow_key {
    __u32 srcip;
    __u32 dstip;
    __u16 srcport;
    __u16 dstport;
    __u16 protocol;
};
static inline uint flow_key_hash_old(struct flow_key* key) {
    int hashCode = (int)key->srcip;
    hashCode = (hashCode * 397) ^ (int)key->dstip;
    hashCode = (hashCode * 397) ^ (int)key->srcport;
    hashCode = (hashCode * 397) ^ (int)key->dstport;
    hashCode = (hashCode * 397) ^ (int)key->protocol;
    return (uint)hashCode;
}

static inline uint flow_key_hash(struct flow_key* key, uint bits) {
    uint hash = hash_32(key->srcip, bits);
    hash ^= hash_32(key->dstip, bits);
    hash ^= hash_32(key->srcport, bits);
    hash ^= hash_32(key->dstport, bits);
    hash ^= hash_32(key->protocol, bits);
    return hash >> (32 - bits);
}

static inline int flow_key_equal(struct flow_key* lhs, struct flow_key* rhs) {
    return lhs->srcip == rhs->srcip && lhs->dstip == rhs->dstip && lhs->srcport == rhs->srcport &&
        lhs->dstport == rhs->dstport && lhs->protocol == rhs->protocol;
}

#endif