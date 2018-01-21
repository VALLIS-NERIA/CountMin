#ifndef COUNTMAX_H
#define COUNTMAX_H
#include "flow_key.h"



struct countmax_line {
    size_t w;
    uint32_t mask;
    struct flow_key** keys;
    elemtype* counters;
};


struct countmax_sketch {
    size_t w;
    size_t d;
    struct countmax_line** lines;
};



struct countmax_manager {
    size_t w;
    size_t d;
    size_t sw_count;
    struct countmax_sketch** sketches;
};



struct countmax_line* new_countmax_line(int w) {
    struct countmax_line* line = new(struct countmax_line);
    line->counters = newarr(elemtype, w);
    line->keys = newarr(struct flow_key*, w);
    line->w = w;
    uint32_t rand = rand_uint32();
    //get_random_bytes(&rand, sizeof(uint32_t));
    line->mask = rand;
    return line;
}

void delete_countmax_line(struct countmax_line* this) {
    kfree(this->counters);
    kfree(this->keys);
    kfree(this);
}

inline void countmax_line_update(struct countmax_line* this, struct flow_key* key,
                          elemtype value) {
    size_t index = (uint32_t)flow_key_hash(key, 16) % this->w;
    //struct flow_key* current_key = &(this->keys[index]);
    //if (flow_key_equal_val(*key, this->keys[index])) {
    if (key==this->keys[index]) {
        this->counters[index] += value;
    }
    else {
        elemtype now = this->counters[index];
        if (value > now) {
            this->counters[index] = value - now;
            this->keys[index] = key;
        }
        else {
            this->counters[index] -= value;
        }
    }
}

inline elemtype countmax_line_query(struct countmax_line* this, struct flow_key* key) {
    size_t index = (uint32_t)flow_key_hash(key,16) % this->w;
    struct flow_key* current_key = &(this->keys[index]);
    if (flow_key_equal(key, current_key)) {
        return this->counters[index];
    }
    else {
        return 0;
    }
}

/*              */

struct countmax_sketch* new_countmax_sketch(int w, int d) {
    struct countmax_sketch* sketch = new(struct countmax_sketch);
    sketch->w = w;
    sketch->d = d;
    sketch->lines = newarr(struct countmax_line*, d);
    int i = 0;
    for (i = 0; i < d; i++) {
        sketch->lines[i] = new_countmax_line(w);
    }
    return sketch;
}

void delete_countmax_sketch(struct countmax_sketch* this) {
    int i = 0;
    for (i = 0; i < this->d; i++) {
        delete_countmax_line(this->lines[i]);
    }
    kfree(this->lines);
    kfree(this);
}

void countmax_sketch_update(struct countmax_sketch* this, struct flow_key* key,
                            elemtype value) {
    int i = 0;
    for (i = 0; i < this->d; i++) {
        countmax_line_update(this->lines[i], key, value);
    }
}

elemtype countmax_sketch_query(struct countmax_sketch* this,
                               struct flow_key* key) {
    elemtype max = 0;
    int i = 0;
    for (i = 0; i < this->d; i++) {
        elemtype q = countmax_line_query(this->lines[i], key);
        if (q > max) {
            max = q;
        }
    }
    return max;
}


struct countmax_manager* new_countmax_manager(int w, int d, int sw_count) {
    struct countmax_manager* manager = new(struct countmax_manager);
    manager->w = w;
    manager->d = d;
    manager->sw_count = sw_count;
    manager->sketches = newarr(struct countmax_sketch*, sw_count);
    int i = 0;
    for (i = 0; i < sw_count; i++) {
        manager->sketches[i] = new_countmax_sketch(w, d);
    }
    return manager;
}

static void delete_countmax_manager(struct countmax_manager* this) {
    int i = 0;
    for (i = 0; i < this->sw_count; i++) {
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
    int i = 0;
    for (i = 0; i < this->sw_count; i++) {
        elemtype q = countmax_sketch_query(this->sketches[i], key);
        if (q > max) {
            max = q;
        }
    }
    return max;
}



#ifndef NULL
static struct countmax_line* new_countmax_line(int w);
static void countmax_line_update(struct countmax_line* this, struct flow_key* key, elemtype value);
static elemtype countmax_line_query(struct countmax_line* this, struct flow_key* key);
static void delete_countmax_line(struct countmax_line* this);
static struct countmax_sketch* new_countmax_sketch(int w, int d);
static void countmax_sketch_update(struct countmax_sketch* this, struct flow_key* key, elemtype value);
static elemtype countmax_sketch_query(struct countmax_sketch* this, struct flow_key* key);
static void delete_countmax_sketch(struct countmax_sketch* this);
static struct countmax_manager* new_countmax_manager(int w, int d, int sw_count);
static void countmax_manager_update(struct countmax_manager* this, int sw_id, struct flow_key* key, elemtype value);
static elemtype countmax_manager_query(struct countmax_manager* this, struct flow_key* key);
static void delete_countmax_manager(struct countmax_manager* this);
#endif

#endif
