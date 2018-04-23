#include <linux/init.h>
#include <linux/kernel.h>
#include <linux/module.h>
#include<linux/in.h>
#include<linux/inet.h>
#include<linux/socket.h>
#include<net/sock.h>
MODULE_LICENSE("MIT");
#define MAX_BATCH 1024
typedef uint64_t elemtype;
unsigned short listen_port = 0x8888;
unsigned short response_port = 0x9999;

struct flow_key {
    uint32_t srcip;
    uint32_t dstip;
    uint16_t srcport;
    uint16_t dstport;
    uint16_t protocol;
};


int get_stat_and_send_back(struct socket* client_sock, char* buf, int len) {
    if (len % sizeof(struct flow_key)) {
        printk("unexpected len: %d\n", len);
        return -1;
    }
    printk("querying...\n");
    int count = len / sizeof(struct flow_key);
    elemtype* sendbuf = kzalloc(count * sizeof(elemtype), GFP_KERNEL);
    struct flow_key* src = (struct flow_key*)buf;
    int i = 0;
    for (i = 0; i < count; ++i) {
        //TODO: query here 
        elemtype val = i;
        sendbuf[i] = val;
    }
    printk("query finished, sending\n");
    struct kvec vec;
    struct msghdr msg;
    memset(&vec, 0, sizeof(vec));
    memset(&msg, 0, sizeof(msg));
    vec.iov_base = sendbuf;
    vec.iov_len = 1;
    kernel_sendmsg(client_sock, &msg, &vec, 1, count * sizeof(elemtype));

    return 0;
}

int listen(void) {
    const unsigned buf_size = MAX_BATCH * sizeof(struct flow_key);
    struct socket *sock, *client_sock;
    struct sockaddr_in s_addr;
    int ret = 0;

    memset(&s_addr, 0, sizeof(s_addr));
    s_addr.sin_family = AF_INET;
    s_addr.sin_port = htons(listen_port);
    s_addr.sin_addr.s_addr = htonl(INADDR_ANY);


    //sock = (struct socket *)kmalloc(sizeof(struct socket), GFP_KERNEL);
    //client_sock = (struct socket *)kmalloc(sizeof(struct socket), GFP_KERNEL);

    /*create a socket*/
    ret = sock_create_kern(&init_net, AF_INET, SOCK_STREAM, 0, &sock);
    if (ret) {
        printk("server:socket_create error!\n");
    }
    printk("server:socket_create ok!\n");

    /*bind the socket*/
    ret = kernel_bind(sock, (struct sockaddr *)&s_addr, sizeof(struct sockaddr_in));
    if (ret < 0) {
        printk("server: bind error\n");
        return ret;
    }
    printk("server:bind ok!\n");

    /*listen*/
    ret = kernel_listen(sock, 10);
    if (ret < 0) {
        printk("server: listen error\n");
        return ret;
    }
    printk("server:listen ok!\n");

    ret = kernel_accept(sock, &client_sock, 10);
    if (ret < 0) {
        printk("server:accept error!\n");
        return ret;
    }
    printk("server: accept ok, Connection Established\n");

    /*kmalloc a receive buffer*/
    char* recvbuf = NULL;
    recvbuf = kzalloc(buf_size, GFP_KERNEL);
    if (recvbuf == NULL) {
        printk("server: recvbuf kmalloc error!\n");
        return -1;
    }
    memset(recvbuf, 0, sizeof(recvbuf));

    /*receive message from client*/
    struct kvec vec;
    struct msghdr msg;
    memset(&vec, 0, sizeof(vec));
    memset(&msg, 0, sizeof(msg));
    vec.iov_base = recvbuf;
    vec.iov_len = 1;
    ret = kernel_recvmsg(client_sock, &msg, &vec, 1, buf_size, 0); /*receive message*/
    printk("receive message, length: %d\n", ret);
    get_stat_and_send_back(client_sock, recvbuf, ret);
    /*release socket*/
    sock_release(sock);
    sock_release(client_sock);
    return ret;
    return 0;
}


int init(void) {

    return 0;
}

module_init(init);
module_exit(clean);
