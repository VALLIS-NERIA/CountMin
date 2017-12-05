#include <stdint.h>
#include "countmax.h"

static int get_hash_code(struct flow_key* key) {
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

struct countmax_line* new_countmax_line(int w) {
    struct countmax_line* line=kzalloc()
}

void countmax_update(struct countmax_line* this, struct flow_key* key, elemtype value) {
    size_t index = get_hash_code(key) % this->w;
    struct flow_key* current_key = &(this->keys[index]);
    if (flow_key_equal(key,current_key)) {
        this->counters[index] += value;
    }
    else {
        elemtype now = this->counters[index];
        if (value > now) {
            this->counters[index] = value - now;
            this->keys[index] = *key;
        }
        else {
            this->counters[index] -= value;
        }
    }
}
