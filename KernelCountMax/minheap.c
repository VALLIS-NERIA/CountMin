#include <linux/slab.h>

#ifndef SUCCESS
#define SUCCESS 0
#endif

#define HEAP_EXCCED -1
#define HEAP_UNINTAILIZED -2

#define LCHILD(x) 2 * x + 1
#define RCHILD(x) 2 * x + 2
#define PARENT(x) (x - 1) / 2

struct node {
    int data;
};

struct min_heap {
    int size;
    int max_size;
    struct node* elem;
};


/*
Function to initialize the min heap with size = 0
*/
struct min_heap new_min_heap(int max_size) {
    struct min_heap hp;
    hp.size = 0;
    hp.max_size = max_size;
    hp.elem = kzalloc(hp.max_size,GFP_ATOMIC);
    return hp;
}


/*
Function to min_heap_node_swap data within two nodes of the min heap using pointers
*/
void min_heap_node_swap(struct node* n1, struct node* n2) {
    struct node temp = *n1;
    *n1 = *n2;
    *n2 = temp;
}


/*
Heapify function is used to make sure that the heap property is never violated
In case of deletion of a struct node, or creating a min heap from an array, heap property
may be violated. In such cases, min_heap_heapify function can be called to make sure that
heap property is never violated
*/
void min_heap_heapify(struct min_heap* hp, int i) {
    int smallest = (LCHILD(i) < hp->size && hp->elem[LCHILD(i)].data < hp->elem[i].data) ? LCHILD(i) : i;
    if (RCHILD(i) < hp->size && hp->elem[RCHILD(i)].data < hp->elem[smallest].data) {
        smallest = RCHILD(i);
    }
    if (smallest != i) {
        min_heap_node_swap(&(hp->elem[i]), &(hp->elem[smallest]));
        min_heap_heapify(hp, smallest);
    }
}


/*
Build a Min Heap given an array of numbers
Instead of using min_heap_insert() function n times for total complexity of O(nlogn),
we can use the min_heap_build() function to build the heap in O(n) time
*/
int min_heap_build(struct min_heap* hp, int* arr, int size) {
    int i;
    if (size > hp->max_size) return HEAP_EXCCED;
    if (!hp->elem) return HEAP_UNINTAILIZED;
    // Insertion into the heap without violating the shape property
    for (i = 0; i < size; i++) {
        struct node nd;
        nd.data = arr[i];
        hp->elem[(hp->size)++] = nd;
    }

    // Making sure that heap property is also satisfied
    for (i = (hp->size - 1) / 2; i >= 0; i--) {
        min_heap_heapify(hp, i);
    }

    return SUCCESS;
}


/*
Function to insert a struct node into the min heap, by allocating space for that struct node in the
heap and also making sure that the heap property and shape propety are never violated.
*/
int min_heap_insert(struct min_heap* hp, int data) {
    //if (hp->size) {
    //    hp->elem = realloc(hp->elem, (hp->size + 1) * sizeof(struct node));
    //}
    //else {
    //    hp->elem = malloc(sizeof(struct node));
    //}
    if (!hp->elem)return HEAP_UNINTAILIZED;
    if (hp->size == hp->max_size)return HEAP_EXCCED;
    struct node nd;
    nd.data = data;

    int i = (hp->size)++;
    while (i && nd.data < hp->elem[PARENT(i)].data) {
        hp->elem[i] = hp->elem[PARENT(i)];
        i = PARENT(i);
    }
    hp->elem[i] = nd;
    return SUCCESS;
}


/*
Function to delete a struct node from the min heap
It shall remove the root struct node, and place the last struct node in its place
and then call min_heap_heapify function to make sure that the heap property
is never violated
*/
void min_heap_extract(struct min_heap* hp) {
    if (hp->size) {
        //printf("Deleting struct node %d\n\n", hp->elem[0].data);
        hp->elem[0] = hp->elem[--(hp->size)];
        hp->elem = realloc(hp->elem, hp->size * sizeof(struct node));
        min_heap_heapify(hp, 0);
    }
    else {
        //printf("\nMin Heap is empty!\n");
        free(hp->elem);
    }
}


/*
Function to get maximum struct node from a min heap
The maximum struct node shall always be one of the leaf nodes. So we shall recursively
move through both left and right child, until we find their maximum nodes, and
compare which is larger. It shall be done recursively until we get the maximum
struct node
*/
int getMaxNode(struct min_heap* hp, int i) {
    if (LCHILD(i) >= hp->size) {
        return hp->elem[i].data;
    }

    int l = getMaxNode(hp, LCHILD(i));
    int r = getMaxNode(hp, RCHILD(i));

    if (l >= r) {
        return l;
    }
    else {
        return r;
    }
}


/*
Function to clear the memory allocated for the min heap
*/
void delete_min_heap(struct min_heap* hp) {
    free(hp->elem);
}


///*
//Function to display all the nodes in the min heap by doing a inorder traversal
//*/
//void inorderTraversal(struct min_heap *hp, int i) {
//    if (LCHILD(i) < hp->size) {
//        inorderTraversal(hp, LCHILD(i));
//    }
//    printf("%d ", hp->elem[i].data);
//    if (RCHILD(i) < hp->size) {
//        inorderTraversal(hp, RCHILD(i));
//    }
//}
//
//
///*
//Function to display all the nodes in the min heap by doing a preorder traversal
//*/
//void preorderTraversal(struct min_heap *hp, int i) {
//    if (LCHILD(i) < hp->size) {
//        preorderTraversal(hp, LCHILD(i));
//    }
//    if (RCHILD(i) < hp->size) {
//        preorderTraversal(hp, RCHILD(i));
//    }
//    printf("%d ", hp->elem[i].data);
//}
//
//
///*
//Function to display all the nodes in the min heap by doing a post order traversal
//*/
//void postorderTraversal(struct min_heap *hp, int i) {
//    printf("%d ", hp->elem[i].data);
//    if (LCHILD(i) < hp->size) {
//        postorderTraversal(hp, LCHILD(i));
//    }
//    if (RCHILD(i) < hp->size) {
//        postorderTraversal(hp, RCHILD(i));
//    }
//}
//
//
///*
//Function to display all the nodes in the min heap by doing a level order traversal
//*/
//void levelorderTraversal(struct min_heap *hp) {
//    int i;
//    for (i = 0; i < hp->size; i++) {
//        printf("%d ", hp->elem[i].data);
//    }
//}
