#include <stdint.h>
#include "countmax.h"
#include "linux/slab.h"

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
    struct countmax_line* line = (struct countmax_line*)kzalloc(sizeof(struct countmax_line), GFP_ATOMIC);
    line->counters = (elemtype*)kzalloc(w * sizeof(elemtype), GFP_ATOMIC);
    line->keys = (struct flow_key*)kzalloc(w * sizeof(struct flow_key), GFP_ATOMIC);
    line->w = w;
    return line;
}

void countmax_line_update(struct countmax_line* this, struct flow_key* key, elemtype value) {
    size_t index = get_hash_code(key) % this->w;
    struct flow_key* current_key = &(this->keys[index]);
    if (flow_key_equal(key, current_key)) {
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

elemtype countmax_line_query(struct countmax_line* this, struct flow_key* key) {
    size_t index = get_hash_code(key) % this->w;
    struct flow_key* current_key = &(this->keys[index]);
    if (flow_key_equal(key, current_key)) {
        return this->counters[index];
    }
    else {
        return 0;
    }
}

struct countmax_sketch* new_countmax_sketch(int w, int d) {
    struct countmax_sketch* sketch = kzalloc(sizeof(struct countmax_sketch), GFP_ATOMIC);
    sketch->w = w;
    sketch->d = d;
    sketch->lines = kzalloc(d * sizeof(struct countmax_line*), GFP_ATOMIC);
    for (int i = 0; i < d; i++) {
        sketch->lines[i] = new_countmax_line(w);
    }
    return sketch;
}

void countmax_sketch_update(struct countmax_sketch* this, struct flow_key* key, elemtype value) {
    for (int i = 0; i < this->d; i++) {
        countmax_line_update(this->lines[i], key, value);
    }
}

elemtype countmax_sketch_query(struct countmax_sketch* this, struct flow_key* key) {
    elemtype max = 0;
    for (int i = 0; i < this->d; i++) {
        elemtype q = countmax_line_query(this->lines[i], key);
        if (q > max) {
            max = q;
        }
    }
    return max;
}
