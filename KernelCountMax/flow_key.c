#include "flow_key.h"
static int flow_key_hash(struct flow_key* key) {
    int hashCode = (int)key->srcip;
    hashCode = (hashCode * 397) ^ (int)key->dstip;
    hashCode = (hashCode * 397) ^ (int)key->srcport;
    hashCode = (hashCode * 397) ^ (int)key->dstport;
    hashCode = (hashCode * 397) ^ (int)key->protocol;
    return hashCode;
}

static int flow_key_equal(struct flow_key* lhs, struct flow_key* rhs) {
    return lhs->srcip == rhs->srcip && lhs->dstip == rhs->dstip && lhs->srcport == rhs->srcport &&
        lhs->dstport == rhs->dstport && lhs->protocol == rhs->protocol;
}