#include <linux/types.h>
#include <linux/hashtable.h>
#include <linux/slab.h>
#include "flow_key.h"

struct htlist_node {
    struct flow_key key;
    int value;
    struct htlist_node* next;
};

struct htlist_head {
    struct htlist_node* first;
};

struct hash_table {
    size_t size;
    struct llist_head* data;
};

//#define llist_foreach(head,obj,)

#define new(name) kzalloc(sizeof(struct name), GFP_KERNEL)
#define newarr(name, size) kzalloc(size * sizeof(struct name), GFP_KERNEL)

struct hash_table init_hash_table(size_t bits) {
    struct hash_table table;
    table.size = 1<<bits;
    table.data = newarr(htlist_head, table.size);
    for (int i = 0; i < table.size; i++) {
        table.data[i].first = NULL;
    }
}

void htlist_add(struct htlist_head head, struct flow_key* key, int value) {
    if (head.first == NULL) {
        head.first = new(htlist_node);
        head.first->key = *key;
        head.first->value = value;
    }
    else {
        struct htlist_node* p = head.first;
        while (p->next != NULL) {
            p = p->next;
        }
        p->next = new(htlist_node);
        p = p->next;
        p->key = *key;
        p->value = value;
    }
}

void hash_table_add(struct hash_table htable, struct flow_key* key, int value) { }
