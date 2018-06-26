#ifndef FLOW_KEY_H
#define FLOW_KEY_H 1

#include "util.h"
#include <stdbool.h>
#include <stddef.h>
#include <math.h>



struct flow_key {
    uint32_t srcip;
    uint32_t dstip;
    union {
        struct {
            uint16_t srcport;
            uint16_t dstport;
        };
        uint32_t port;
    };
    uint16_t protocol;
};
inline uint32_t flow_key_hash_old(struct flow_key key) {
    int hashCode = (int)key.srcip;
    hashCode = (hashCode * 397) ^ (int)key.dstip;
    hashCode = (hashCode * 397) ^ (int)key.srcport;
    hashCode = (hashCode * 397) ^ (int)key.dstport;
    hashCode = (hashCode * 397) ^ (int)key.protocol;
    return (uint32_t)hashCode;
}

inline uint32_t flow_key_hash(struct flow_key key, uint32_t bits) {
    uint32_t hash = hash_32(key.srcip, bits);
    hash ^= hash_32(key.dstip, bits);
    hash ^= hash_32(key.port, bits);
    hash ^= hash_32(key.protocol, bits);
    //hash ^= hash_32(key->dstport, bits);
    //hash ^= hash_32(key->protocol, bits);
    return hash;
}

inline uint32_t flow_key_hash_2(struct flow_key key, uint32_t mod) {
    int bits = (uint32_t)log2f(mod);;
    uint32_t hash = hash_32(key.srcip, bits);
    hash ^= hash_32(key.dstip, bits);
    hash ^= hash_32(*((uint32_t*)&(key.srcport)), bits);
    //hash ^= hash_32(key->dstport, bits);
    //hash ^= hash_32(key->protocol, bits);
    return hash>>(32-bits);
}

inline int flow_key_equal_ref(struct flow_key* lhs, struct flow_key* rhs) {
    return lhs->srcip == rhs->srcip && lhs->dstip == rhs->dstip && lhs->srcport == rhs->srcport &&
        lhs->dstport == rhs->dstport && lhs->protocol == rhs->protocol;
}

inline int flow_key_equal(struct flow_key lhs, struct flow_key rhs) {
    return memcmp(&lhs, &rhs, sizeof(struct flow_key));
    return lhs.srcip == rhs.srcip && lhs.dstip == rhs.dstip && lhs.srcport == rhs.srcport &&
        lhs.dstport == rhs.dstport && lhs.protocol == rhs.protocol;
}

#endif