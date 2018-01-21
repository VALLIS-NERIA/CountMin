#ifndef FSS
#define FSS

#include "minheap.h"


struct fss_sketch {
    size_t w;
    uint32_t mask;
    //struct flow_key* keys;
    elemtype* counters;
    int* hash_counters;
    struct hash_heap* heap;
};

struct fss_manager {
    size_t w;
    size_t d;
    size_t sw_count;
    struct fss_sketch** sketches;
};


/*              */

struct fss_sketch* new_fss_sketch(int w) {
    struct fss_sketch* sketch = new(struct fss_sketch);
    sketch->w = w;
    sketch->heap = new_hash_heap(w);
    sketch->counters = newarr(elemtype, w);
    sketch->hash_counters = newarr(int, w);
    for (int i = 0; i < w; i++) {
        sketch->counters[i] = 0;
        sketch->hash_counters[i] = 0;
    }
    uint32_t ran = rand_uint32();
    sketch->mask = ran;
    return sketch;
}

void delete_fss_sketch(struct fss_sketch* this) {
    delete_hash_heap(this->heap);
    kfree(this->counters);
    kfree(this->hash_counters);
    kfree(this);
}




elemtype fss_sketch_query(struct fss_sketch* this, struct flow_key* key) {
    ht_value tvalue;
    if (hash_table_get(this->heap->indexes, key, &tvalue) == HT_ERR_KEY_NOT_FOUND) {
        return 0;
    }
}

int heap_count_1 = 0;
int heap_count_2 = 0;

void fss_sketch_update(struct fss_sketch* this, struct flow_key* key, elemtype value) {
    elemtype min = hash_heap_peek(this->heap);
    elemtype u = this->heap->size < this->heap->max_size ? 0:min;
    size_t index = ((uint32_t)flow_key_hash_old(key) ^ this->mask) % this->w;
    if (this->hash_counters[index] != 0) {
        ht_value v_;
        int ret = hash_table_get(this->heap->indexes, key, &v_);
        // exist
        if (ret == SUCCESS) {
            ++heap_count_1;
            hash_heap_inc(this->heap, key, value);
            return;
        }
    }
    this->hash_counters[index] += value;
    elemtype v = this->hash_counters[index];
    if (this->hash_counters[index] > u) {
        ++heap_count_2;
        if (this->heap->size < this->w) {
            hash_heap_insert(this->heap, key, v);
        }
        else {
            hash_heap_extract(this->heap);
            struct flow_key* kmin = hash_heap_peek_key(this->heap);
            size_t index_m = ((uint32_t)flow_key_hash_old(kmin) ^ this->mask) % this->w;
            this->hash_counters[index_m] -= 1;
            this->counters[index] = min;

            hash_heap_insert(this->heap, key, v);

        }
        this->hash_counters[index] += 1;
    }    
}

/*              */

struct fss_manager* new_fss_manager(int w, int d, int sw_count) {
    struct fss_manager* manager = new(struct fss_manager);
    manager->w = w;
    manager->d = d;
    manager->sw_count = sw_count;
    manager->sketches = newarr(struct fss_sketch, sw_count);
    //kzalloc(sw_count * sizeof(struct fss_sketch*), GFP_ATOMIC);
    for (int i = 0; i < sw_count; i++) {
        manager->sketches[i] = new_fss_sketch(w);
    }
    return manager;
}

static void delete_fss_manager(struct fss_manager* this) {
    for (int i = 0; i < this->sw_count; i++) {
        delete_fss_sketch(this->sketches[i]);
    }
    kfree(this->sketches);
}

void fss_manager_update(struct fss_manager* this, int sw_id, struct flow_key* key, elemtype value) {
    if (sw_id < 0 || sw_id >= this->sw_count) {
        return;
    }
    fss_sketch_update(this->sketches[sw_id], key, value);
}

elemtype fss_manager_query(struct fss_manager* this, struct flow_key* key) {
    elemtype max = 0;
    for (int i = 0; i < this->sw_count; i++) {
        elemtype q = fss_sketch_query(this->sketches[i], key);
        if (q > max) {
            max = q;
        }
    }
    return max;
}


#ifndef NULL
static struct fss_line* new_fss_line(int w);
static void fss_line_update(struct fss_line* this, struct flow_key* key, elemtype value);
static elemtype fss_line_query(struct fss_line* this, struct flow_key* key);
static void delete_fss_line(struct fss_line* this);

static struct fss_sketch* new_fss_sketch(int w, int d);
static void fss_sketch_update(struct fss_sketch* this, struct flow_key* key, elemtype value);
static elemtype fss_sketch_query(struct fss_sketch* this, struct flow_key* key);
static void delete_fss_sketch(struct fss_sketch* this);

static struct fss_manager* new_fss_manager(int w, int d, int sw_count);
static void fss_manager_update(struct fss_manager* this, int sw_id, struct flow_key* key,
    elemtype value);
static elemtype fss_manager_query(struct fss_manager* this, struct flow_key* key);
static void delete_fss_manager(struct fss_manager* this);
#endif

#endif
