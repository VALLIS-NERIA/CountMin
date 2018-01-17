#include <linux/types.h>
#include <linux/hashtable.h>
#include <linux/slab.h>
#include "flow_key.h"

typedef u32 ht_value;
#define HT_ERR_KEY_NOT_FOUND -1
#define SUCCESS 0

struct htlist_node {
    struct flow_key key;
    ht_value value;
    struct htlist_node* next;
};

struct htlist_head {
    struct htlist_node* first;
};

struct hash_table {
    size_t size;
    int bits;
    struct htlist_head** data;
};

//#define llist_foreach(head,obj,)

#define new(name) kzalloc(sizeof(struct name), GFP_KERNEL)
#define newarr(name, size) kzalloc(size * sizeof(struct name), GFP_KERNEL)

struct hash_table* new_hash_table(size_t bits) {
    struct hash_table* table = new(hash_table);
    table->size = 1 << bits;
    table->bits = bits;
    table->data = newarr(htlist_head*, table->size);
    for (int i = 0; i < table->size; i++) {
        table->data[i]->first = NULL;
    }
    return table;
}

void htlist_add(struct htlist_head* head, struct flow_key* key, ht_value value) {
    if (head->first == NULL) {
        head->first = new(htlist_node);
        head->first->key = *key;
        head->first->value = value;
    }
    else {
        struct htlist_node* p = head->first;
        while (p->next != NULL) {
            p = p->next;
        }
        p->next = new(htlist_node);
        p = p->next;
        p->key = *key;
        p->value = value;
    }
}

void hash_table_add(struct hash_table* htable, struct flow_key* key, ht_value value) {
    uint hash = flow_key_hash(key, htable->bits);
    htlist_add(htable->data[hash], key, value);
}

int htlist_get(struct htlist_head* head, struct flow_key* key, ht_value* value) {
    struct htlist_node* p = head->first;
    while(p!=NULL) {
        if(flow_key_equal(&(p->key),key)) {
            *value = p->value;
            return SUCCESS;
        }
        else {
            p = p->next;
        }
    }
    return HT_ERR_KEY_NOT_FOUND;
}

int hash_table_get(struct hash_table* htable, struct flow_key* key, ht_value* value) {
    uint hash = flow_key_hash(key, htable->bits);
    return htlist_get(htable->data[hash], key, value);
}

int htlist_set(struct htlist_head* head, struct flow_key* key, ht_value value) {
    struct htlist_node* p = head->first;
    while (p != NULL) {
        if (flow_key_equal(&(p->key), key)) {
            p->value = value;
            return SUCCESS;
        }
        else {
            p = p->next;
        }
    }
    return HT_ERR_KEY_NOT_FOUND;
}

int hash_table_set(struct hash_table* htable, struct flow_key* key, ht_value value) {
    uint hash = flow_key_hash(key, htable->bits);
    return htlist_set(htable->data[hash], key, value);
}

int htlist_inc(struct htlist_head* head, struct flow_key* key, ht_value value) {
    struct htlist_node* p = head->first;
    while (p != NULL) {
        if (flow_key_equal(&(p->key), key)) {
            p->value += value;
            return SUCCESS;
        }
        else {
            p = p->next;
        }
    }
    return HT_ERR_KEY_NOT_FOUND;
}

int hash_table_inc(struct hash_table* htable, struct flow_key* key, ht_value value) {
    uint hash = flow_key_hash(key, htable->bits);
    return htlist_inc(htable->data[hash], key, value);
}

