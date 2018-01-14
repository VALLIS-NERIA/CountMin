#include <linux/random.h>
#include "countmax.h"
// #include <stdint.h>
// #include <stdlib.h>
#include "linux/slab.h"

struct countmax_line* new_countmax_line(int w) {
    struct countmax_line* line = (struct countmax_line*)kzalloc(
        sizeof(struct countmax_line), GFP_ATOMIC);
    line->counters = (elemtype*)kzalloc(w * sizeof(elemtype), GFP_ATOMIC);
    line->keys =
        (struct flow_key*)kzalloc(w * sizeof(struct flow_key), GFP_ATOMIC);
    line->w = w;
    uint32_t rand = 0;
    get_random_bytes(&rand, sizeof(uint32_t));
    line->mask = rand;
    return line;
}

void delete_countmax_line(struct countmax_line* this) {
    kfree(this->counters);
    kfree(this->keys);
    kfree(this);
}

void countmax_line_update(struct countmax_line* this, struct flow_key* key,
                          elemtype value) {
    size_t index = (uint32_t)flow_key_hash(key) % this->w;
    struct flow_key* current_key = &(this->keys[index]);
    if (flow_key_equal(key, current_key)) {
        this->counters[index] += value;
    } else {
        elemtype now = this->counters[index];
        if (value > now) {
            this->counters[index] = value - now;
            this->keys[index] = *key;
        } else {
            this->counters[index] -= value;
        }
    }
}

elemtype countmax_line_query(struct countmax_line* this, struct flow_key* key) {
    size_t index = (uint32_t)flow_key_hash(key) % this->w;
    struct flow_key* current_key = &(this->keys[index]);
    if (flow_key_equal(key, current_key)) {
        return this->counters[index];
    } else {
        return 0;
    }
}

/*              */

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

void delete_countmax_sketch(struct countmax_sketch* this) {
    for (int i = 0; i < this->d;i++){
        delete_countmax_line(this->lines[i]);
    }
    kfree(this->lines);
}

void countmax_sketch_update(struct countmax_sketch* this, struct flow_key* key,
                            elemtype value) {
    for (int i = 0; i < this->d; i++) {
        countmax_line_update(this->lines[i], key, value);
    }
}

elemtype countmax_sketch_query(struct countmax_sketch* this,
                               struct flow_key* key) {
    elemtype max = 0;
    for (int i = 0; i < this->d; i++) {
        elemtype q = countmax_line_query(this->lines[i], key);
        if (q > max) {
            max = q;
        }
    }
    return max;
}


struct countmax_manager* new_countmax_manager(int w, int d, int sw_count) {
    struct countmax_manager* manager =
        kzalloc(sizeof(struct countmax_manager), GFP_ATOMIC);
    manager->w = w;
    manager->d = d;
    manager->sw_count = sw_count;
    manager->sketches =
        kzalloc(sw_count * sizeof(struct countmax_sketch*), GFP_ATOMIC);
    for (int i = 0; i < sw_count; i++) {
        manager->sketches[i] = new_countmax_sketch(w, d);
    }
    return manager;
}

static void delete_countmax_manager(struct countmax_manager* this){
    for (int i = 0; i < this->sw_count;i++){
        delete_countmax_sketch(this->sketches[i]);
    }
    kfree(this->sketches);
}

void countmax_manager_update(struct countmax_manager* this, int sw_id,
                             struct flow_key* key, elemtype value) {
    if (sw_id < 0 || sw_id >= this->sw_count) {
        return;
    }
    countmax_sketch_update(this->sketches[sw_id], key, value);
}

elemtype countmax_manager_query(struct countmax_manager* this,
                                struct flow_key* key) {
    elemtype max = 0;
    for (int i = 0; i < this->sw_count; i++) {
        elemtype q = countmax_sketch_query(this->sketches[i], key);
        if (q > max) {
            max = q;
        }
    }
    return max;
}


