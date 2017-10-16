#ifndef __MY_NETLINK_H
#define __MY_NETLINK_H
#include <stdio.h>
#include <stdlib.h>
#include <sys/socket.h>
#include <string.h>
#include <linux/netlink.h>
#include <unistd.h>
#include <errno.h>
#include "md5.h"
#include "sketch.h"
#include <memory>

#define NETLINK_USER 22
#define USER_MSG (NETLINK_USER + 1)
#define MSG_LEN 4096

#define MAX_PLOAD 100

#define NLREQUEST_REPORT 1
#define NLREQUEST_FETCH 2
#define PORT_NAME_LEN 40

#define ETH_ALEN 6

struct sw_flow_id {
    uint32 ufid_len;

    union {
        uint32 ufid[16 / 4];
        struct sw_flow_key* unmasked_key;
    };

    std::string to_string();
};

std::string ip_to_str(uint32 ip);

struct ipv4_key {
    struct {
        __be32 src; /* IP source address. */
        __be32 dst; /* IP destination address. */
    } addr;
    struct {
        uint8_t sha[ETH_ALEN]; /* ARP source hardware address. */
        uint8_t tha[ETH_ALEN]; /* ARP target hardware address. */
    } arp;
};

struct port_key {
    __be16 src; /* TCP/UDP/SCTP source port. */
    __be16 dst; /* TCP/UDP/SCTP destination port. */
    __be16 flags; /* TCP flags. */
};

struct my_flow_key {
    struct ipv4_key ip;
    struct port_key port;

    std::string to_string();
};


//struct defused_packet {
//    int port_name_len;
//    int flow_id_size;
//    uint packet_size;
//    char* port_name;
//    void* flow_id;
//    friend struct stat_packet;
//
//    ~defused_packet();
//};

struct stat_packet {
    int request_type;
    int port_name_len;
    void* flow_pointer;
    struct my_flow_key key;
    uint packet_size;
    uint64 flow_traffic;
    char port_name[PORT_NAME_LEN];
};

struct reply_packet {
    uint64 countmin_traffic;
    void* flow;
    struct my_flow_key key;
};


struct my_msg {
    nlmsghdr header;
    stat_packet data;
};


int opensocket();
sockaddr_nl bind_local(int skfd);
int nl_send(int skfd, const void* data, size_t data_len, unsigned int pid, sockaddr_nl dest_addr);
#endif
