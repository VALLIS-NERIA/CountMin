#include <stddef.h>

struct llist_node {
    struct llist_node* next;
};

struct llist_head {
    struct llist_node* first;
};

struct hash_table {
    size_t size;
    struct llist_head* data;
};

//#define llist_foreach(head,obj,)

struct hash_table init_hash_table(size_t size) {
    
}