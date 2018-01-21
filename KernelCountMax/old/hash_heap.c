#include "hashtable.c"
#include "minheap.c"
#include "linux/log2.h"
//struct hash_heap {
//    struct hash_table* ht;
//    struct min_heap* heap;
//    int size;
//    int max_size;
//};
//
//struct hash_heap new_hash_heap(size_t size) {
//    struct hash_heap hh;
//    hh.ht = new_hash_table(__ilog2_u32(size) + 1);
//    hh.heap = new_min_heap(size);
//    hh.size = 0;
//    hh.max_size = size;
//    return hh;
//}
//
//int hash_heap_add(struct hash_heap* hh, struct flow_key* key, heap_data value) {
//    hash_table_add()
//}