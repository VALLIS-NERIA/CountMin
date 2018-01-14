#include <linux/init.h>
#include <linux/kernel.h>
#include <linux/module.h>
#include "countmax.c"
#include "flow_key.c"

MODULE_LICENSE("MIT");
static struct countmax_sketch* cm;
static int __init init(void) {
    cm = new_countmax_sketch(100, 4);
    struct flow_key key;
    elemtype value = 10;
    countmax_sketch_update(cm, &key, 10);
    printk("%d\n", countmax_sketch_query(cm, &key));
    return 0;
}

static void clean(void) { delete_countmax_sketch(cm); }

module_init(init);
module_exit(clean);