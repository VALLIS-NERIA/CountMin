#ifndef MY_HASH_HEAP
#define MY_HASH_HEAP
#include <linux/slab.h>
#include <linux/types.h>
#include "hashtable.c"

#ifndef SUCCESS
#define SUCCESS 0
#endif

#define HEAP_EXCCED -1
#define HEAP_UNINTAILIZED -2

#define LCHILD(x) 2 * x + 1
#define RCHILD(x) 2 * x + 2
#define PARENT(x) (x - 1) / 2

typedef int64_t heap_data;

struct node {
    struct flow_key* key;
    heap_data data;
};

struct hash_heap {
    struct flow_key* keys;
    struct hash_table* indexes;
    int size;
    int max_size;
    struct node* elem;
};


/*
Function to initialize the min heap with size = 0
*/
struct hash_heap* new_hash_heap(int max_size) {
    struct hash_heap *hp = new(struct hash_heap);
    hp->keys = newarr(struct flow_key, max_size);
    hp->indexes = new_hash_table(__ilog2_u32(max_size) + 1);
    hp->size = 0;
    hp->max_size = max_size;
    hp->elem = newarr(struct node*, hp->size);
    return hp;
}


/*
Function to hash_heap_node_swap data within two nodes of the min heap using pointers
*/
void hash_heap_swap(struct hash_heap* hh,int index1, int index2) {
    struct node tmp = hh->elem[index1];
    hh->elem[index1] = hh->elem[index2];
    hh->elem[index2] = tmp;
    //ht_value tmpv;
    hash_table_set(hh->indexes, hh->elem[index1].key, index1);
    hash_table_set(hh->indexes, hh->elem[index2].key, index2);
}


/*
Heapify function is used to make sure that the heap property is never violated
In case of deletion of a struct node, or creating a min heap from an array, heap property
may be violated. In such cases, hash_heap_heapify function can be called to make sure that
heap property is never violated
*/
void hash_heap_heapify(struct hash_heap* hp, int i) {
    int smallest = (LCHILD(i) < hp->size && hp->elem[LCHILD(i)].data < hp->elem[i].data) ? LCHILD(i) : i;
    if (RCHILD(i) < hp->size && hp->elem[RCHILD(i)].data < hp->elem[smallest].data) {
        smallest = RCHILD(i);
    }
    if (smallest != i) {
        hash_heap_swap(hp, i, smallest);
        hash_heap_heapify(hp, smallest);
    }
}

/*
Function to insert a struct node into the min heap, by allocating space for that struct node in the
heap and also making sure that the heap property and shape propety are never violated.
*/
int hash_heap_insert(struct hash_heap* hp,struct flow_key* key, int data) {

    if (!hp->elem)return HEAP_UNINTAILIZED;
    if (hp->size == hp->max_size)return HEAP_EXCCED;

    // copy the key
    struct flow_key* t_key = new(struct flow_key);
    *t_key = *key;
    
    struct node nd;
    nd.key = t_key;
    nd.data = data;

    int i = (hp->size)++;
    while (i && nd.data < hp->elem[PARENT(i)].data) {
        hp->elem[i] = hp->elem[PARENT(i)];
        i = PARENT(i);
    }
    hp->elem[i] = nd;

    // insert to hashtable
    hash_table_add(hp->indexes, t_key, i);
    return SUCCESS;
}

heap_data inline hash_heap_peek(struct hash_heap* hp) {
    if (hp->size) {
        int last = (hp->size) - 1;
        return hp->elem[last].data;
    }
    return 0;
}

/*
Function to delete a struct node from the min heap
It shall remove the root struct node, and place the last struct node in its place
and then call hash_heap_heapify function to make sure that the heap property
is never violated
*/
void hash_heap_extract(struct hash_heap* hp) {
    if (hp->size) {
        //printf("Deleting struct node %d\n\n", hp->elem[0].data);
        int last = --(hp->size);
        hp->elem[0] = hp->elem[last];
        hash_table_remove(hp->indexes, hp->elem[last].key);
        hash_heap_heapify(hp, 0);
    }
}

int hash_heap_update_or_insert(struct hash_heap* hp,struct flow_key* key, heap_data value) {
    int index;
    int ret = hash_table_get(hp->indexes, key, &index);
    if (ret == SUCCESS) {
        hp->elem[index].data = value;
        hash_heap_heapify(hp, 0);
        return SUCCESS;
    }
    else if(ret==HT_ERR_KEY_NOT_FOUND) {
        return hash_heap_insert(hp, key, value);
    }
    return -255;
}

int hash_heap_inc(struct hash_heap* hp, struct flow_key* key, heap_data value) {
    int index;
    int ret = hash_table_get(hp->indexes, key, &index);
    if (ret == SUCCESS) {
        hp->elem[index].data += value;
        hash_heap_heapify(hp, 0);
        return SUCCESS;
    }
    else if (ret == HT_ERR_KEY_NOT_FOUND) {
        return hash_heap_insert(hp, key, value);
    }
    return -255;
}


/*
Function to clear the memory allocated for the min heap
*/
void delete_hash_heap(struct hash_heap* hp) {
    delete_hash_table(hp->indexes);
    kfree(hp->elem);
    kfree(hp);
}

#endif
