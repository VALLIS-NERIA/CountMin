#ifndef MY_HASH_TABLE
#define MY_HASH_TABLE

#include "flow_key.h"

typedef uint32_t ht_value;
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
    size_t slot_count;
    int bits;
    struct htlist_head** data;
};




struct hash_table* new_hash_table(size_t size) {
    struct hash_table* table = new(struct hash_table);
    table->slot_count = size;
    table->bits = (uint32_t)log2f(size);
    table->data = newarr(struct htlist_head*, table->slot_count);
    for (size_t i = 0; i < table->slot_count; i++) {
        table->data[i] = new(struct htlist_head);
        table->data[i]->first = NULL;
    }
    return table;
}

void htlist_add(struct htlist_head* head, struct flow_key key, ht_value value) {
    if (head->first == NULL) {
        head->first = new(struct htlist_node);
        head->first->next = NULL;
        head->first->key = key;
        head->first->value = value;
    }
    else {
        struct htlist_node* p = head->first;
        while (p->next != NULL) {
            p = p->next;
        }
        p->next = new(struct htlist_node);
        p = p->next;
        p->next = NULL;
        p->key = key;
        p->value = value;
    }
}

void hash_table_add(struct hash_table* htable, struct flow_key key, ht_value value) {
    uint32_t hash = flow_key_hash(key, htable->bits);
    htlist_add(htable->data[hash], key, value);
}
int ht_count = 0;
int htlist_get(struct htlist_head* head, struct flow_key key, ht_value* value) {
    struct htlist_node* p = head->first;
    while (p != NULL) {
        if (flow_key_equal((p->key), key)) {
            *value = p->value;
            return SUCCESS;
        }
        else {
            p = p->next;
            ++ht_count;
        }
    }
    return HT_ERR_KEY_NOT_FOUND;
}

int hash_table_get(struct hash_table* htable, struct flow_key key, ht_value* value) {
    uint32_t hash = flow_key_hash(key, htable->bits);
    return htlist_get(htable->data[hash], key, value);
}

int htlist_set(struct htlist_head* head, struct flow_key key, ht_value value) {
    struct htlist_node* p = head->first;
    while (p != NULL) {
        if (flow_key_equal((p->key), key)) {
            p->value = value;
            return SUCCESS;
        }
        else {
            p = p->next;
        }
    }
    return HT_ERR_KEY_NOT_FOUND;
}

int hash_table_set(struct hash_table* htable, struct flow_key key, ht_value value) {
    uint32_t hash = flow_key_hash(key, htable->bits);
    return htlist_set(htable->data[hash], key, value);
}

int htlist_inc(struct htlist_head* head, struct flow_key key, ht_value value) {
    struct htlist_node* p = head->first;
    while (p != NULL) {
        if (flow_key_equal((p->key), key)) {
            p->value += value;
            return SUCCESS;
        }
        else {
            p = p->next;
        }
    }
    return HT_ERR_KEY_NOT_FOUND;
}

int hash_table_inc(struct hash_table* htable, struct flow_key key, ht_value value) {
    uint32_t hash = flow_key_hash(key, htable->bits);
    return htlist_inc(htable->data[hash], key, value);
}

int htlist_remove(struct htlist_head* head, struct flow_key key) {
    struct htlist_node* p = head->first;
    if (flow_key_equal((p->key), key)) {
        head->first = p->next;
        kfree(p);
    }
    while (p != NULL) {
        struct htlist_node* q = p->next;
        if (q == NULL) {
            return HT_ERR_KEY_NOT_FOUND;
        }

        if (flow_key_equal((q->key), key)) {
            p->next = q->next;
            kfree(q);
            return SUCCESS;
        }
        else {
            p = p->next;
        }
    }
    return HT_ERR_KEY_NOT_FOUND;
}

int hash_table_remove(struct hash_table* htable, struct flow_key key) {
    uint32_t hash = flow_key_hash(key, htable->bits);
    return htlist_remove(htable->data[hash], key);
}

void delete_htlist(struct htlist_head* head) {
    struct htlist_node* p = head->first;
    while (p != NULL) {
        struct htlist_node* q = p->next;
        kfree(p);
        p = q;
    }
    kfree(head);
}

void delete_hash_table(struct hash_table* htable) {
    for (size_t i = 0; i < htable->slot_count; i++) {
        delete_htlist(htable->data[i]);
    }
    kfree(htable->data);
    kfree(htable);
}

#endif
