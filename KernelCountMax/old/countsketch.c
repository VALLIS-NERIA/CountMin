#include "minheap.c"
#include <linux/random.h>
#include <linux/sort.h>

typedef int64_t elemtype;
void inline my_sort(void *base, size_t num, size_t size,
    int(*cmp)(const void *, const void *)) {
    sort(base, num, size, cmp, NULL);
}

struct countsketch_line {
    size_t w;
    uint32_t mask;
    //struct flow_key* keys;
    elemtype* counters;
};

struct countsketch_sketch {
    size_t w;
    size_t d;
    struct countsketch_line** lines;
    struct hash_heap* heap;
};

struct countsketch_manager {
    size_t w;
    size_t d;
    size_t sw_count;
    struct countsketch_sketch** sketches;
};


struct countsketch_line* new_countsketch_line(int w) {
    struct countsketch_line* line = new(struct countsketch_line);
    line->counters = newarr(elemtype, w);
    //line->keys = (struct flow_key*)kzalloc(w * sizeof(struct flow_key), GFP_ATOMIC);
    line->w = w;
    uint32_t rand = 0;
    get_random_bytes(&rand, sizeof(uint32_t));
    line->mask = rand;
    return line;
}

void delete_countsketch_line(struct countsketch_line* this) {
    kfree(this->counters);
    //kfree(this->keys);
    kfree(this);
}

void countsketch_line_update(struct countsketch_line* this, struct flow_key* key, elemtype value) {
    size_t index = (uint32_t)flow_key_hash_old(key) ^ this->mask % this->w;
    //struct flow_key* current_key = &(this->keys[index]);
    int sign = flow_key_hash(key, 1) == 0 ? 1 : -1;
    this->counters[index] += sign * value;
}

elemtype countsketch_line_query(struct countsketch_line* this, struct flow_key* key) {
    size_t index = (uint32_t)flow_key_hash_old(key) ^ this->mask % this->w;
    int sign = flow_key_hash(key, 1) == 0 ? 1 : -1;
    return this->counters[index] * sign;
}

/*              */

struct countsketch_sketch* new_countsketch_sketch(int w, int d) {
    struct countsketch_sketch* sketch = new(struct countsketch_sketch);
    sketch->w = w;
    sketch->d = d;
    sketch->lines = newarr(struct countsketch_line, d);
    sketch->heap = new_hash_heap(w);
    for (int i = 0; i < d; i++) {
        sketch->lines[i] = new_countsketch_line(w);
    }
    return sketch;
}

void delete_countsketch_sketch(struct countsketch_sketch* this) {
    for (int i = 0; i < this->d; i++) {
        delete_countsketch_line(this->lines[i]);
    }
    kfree(this->lines);
}

int cmpelem(const void *a, const void *b)
{
    return *(elemtype *)a - *(elemtype *)b;
}

elemtype countsketch_sketch_forcequery(struct countsketch_sketch* this, struct flow_key* key) {
    elemtype* results = newarr(elemtype, this->d);
    for (int i = 0; i < this->d; i++) {
        elemtype q = countsketch_line_query(this->lines[i], key);
        results[i] = q;
    }
    my_sort(results, this->d, sizeof(elemtype), cmpelem);
    if (this->d % 2 == 0) {
        return (results[this->d / 2] + results[this->d / 2 - 1]) / 2;
    }
    else {
        return results[(this->d - 1) / 2];
    }
}

elemtype countsketch_sketch_query(struct countsketch_sketch* this, struct flow_key* key) {
    elemtype tvalue;
    if (hash_table_get(this->heap->indexes, key, &tvalue) == HT_ERR_KEY_NOT_FOUND) {
        return 0;
    }
    return countsketch_sketch_forcequery(this, key);
}

void countsketch_sketch_update(struct countsketch_sketch* this, struct flow_key* key, elemtype value) {
    for (int i = 0; i < this->d; i++) {
        countsketch_line_update(this->lines[i], key, value);
    }
    elemtype v;
    int ret = hash_table_get(this->heap->indexes, key, &v);
    // exist
    if(ret==SUCCESS) {
        hash_heap_inc(this->heap, key, value);
    }
    else if(ret==HT_ERR_KEY_NOT_FOUND) {
        // TODO
        elemtype v = countsketch_sketch_forcequery(this, key);
        if(this->heap->size<this->w) {
            hash_heap_insert(this->heap, key, v);
        }
        else {
            elemtype min = hash_heap_peek(this->heap);
            if(min<v) {
                hash_heap_extract(this->heap);
                hash_heap_insert(this->heap, key, v);
            }
        }
    }
}

/*              */

struct countsketch_manager* new_countsketch_manager(int w, int d, int sw_count) {
    struct countsketch_manager* manager = new(struct countsketch_manager);
    manager->w = w;
    manager->d = d;
    manager->sw_count = sw_count;
    manager->sketches = newarr(struct countsketch_sketch, sw_count);
        //kzalloc(sw_count * sizeof(struct countsketch_sketch*), GFP_ATOMIC);
    for (int i = 0; i < sw_count; i++) {
        manager->sketches[i] = new_countsketch_sketch(w, d);
    }
    return manager;
}

static void delete_countsketch_manager(struct countsketch_manager* this) {
    for (int i = 0; i < this->sw_count; i++) {
        delete_countsketch_sketch(this->sketches[i]);
    }
    kfree(this->sketches);
}

void countsketch_manager_update(struct countsketch_manager* this, int sw_id, struct flow_key* key, elemtype value) {
    if (sw_id < 0 || sw_id >= this->sw_count) {
        return;
    }
    countsketch_sketch_update(this->sketches[sw_id], key, value);
}

elemtype countsketch_manager_query(struct countsketch_manager* this, struct flow_key* key) {
    elemtype max = 0;
    for (int i = 0; i < this->sw_count; i++) {
        elemtype q = countsketch_sketch_query(this->sketches[i], key);
        if (q > max) {
            max = q;
        }
    }
    return max;
}


#ifndef NULL
static struct countsketch_line* new_countsketch_line(int w);
static void countsketch_line_update(struct countsketch_line* this, struct flow_key* key, elemtype value);
static elemtype countsketch_line_query(struct countsketch_line* this, struct flow_key* key);
static void delete_countsketch_line(struct countsketch_line* this);

static struct countsketch_sketch* new_countsketch_sketch(int w, int d);
static void countsketch_sketch_update(struct countsketch_sketch* this, struct flow_key* key, elemtype value);
static elemtype countsketch_sketch_query(struct countsketch_sketch* this, struct flow_key* key);
static void delete_countsketch_sketch(struct countsketch_sketch* this);

static struct countsketch_manager* new_countsketch_manager(int w, int d, int sw_count);
static void countsketch_manager_update(struct countsketch_manager* this, int sw_id, struct flow_key* key,
    elemtype value);
static elemtype countsketch_manager_query(struct countsketch_manager* this, struct flow_key* key);
static void delete_countsketch_manager(struct countsketch_manager* this);
#endif