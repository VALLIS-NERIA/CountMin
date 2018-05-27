#include <unistd.h>
#include <stdio.h>
#include <sys/socket.h>
#include <netinet/ip.h>
#include <netinet/udp.h>
#include <memory.h>
#include <stdlib.h>
#include <linux/if_ether.h>
#include <linux/if_packet.h> // sockaddr_ll  
#include <arpa/inet.h>
#include <netinet/if_ether.h>
#include <time.h>
#include <pthread.h>
// The packet length  
#define PCKT_LEN 1500
//#define MAGIC_SRC_PORT 0xdead
//#define MAGIC_DST_PORT 0xbeef
#define MAGIC_SRC_ADDR 0xdeadbeef //239.190.173.222
#define MAGIC_SRC_PORT 0xcdfa //64205
#define MAGIC_DST_ADDR 0x15175731
#define MAGIC_DST_PORT 0x2857 //22312


extern unsigned short udp_check_sum(uint32_t srcip, uint32_t dstip, uint16_t* packet, size_t byte_len);
extern int send_packet(int sock_fd, uint32_t srcip, uint32_t dstip, uint16_t srcport, uint16_t dstport, char* load,
                       size_t load_len);
extern int full_main(int argc, char* argv[]);
static char global_buf[PCKT_LEN];
static uint32_t base_check_sum = 0;

int init_test_packet(uint16_t srcport, uint16_t dstport, char* load, size_t load_len) {
    int sd, dummy;
    // bind socket
    //setuid(getpid()); //如果不是root用户，需要获取权限    
    sd = socket(AF_INET, SOCK_RAW, IPPROTO_UDP);
    if (sd < 0) {
        perror("socket() error");
        exit(-1);
    }
    if (setsockopt(sd, IPPROTO_IP, IP_HDRINCL, &dummy, sizeof(int))) {
        perror("setsockopt() error");
        exit(-1);
    }

    // initialize buffer
    memset(global_buf, 0, PCKT_LEN);
    struct iphdr* ip = (struct iphdr *)global_buf;
    struct udphdr* udp = (struct udphdr *)(global_buf + sizeof(struct iphdr));
    unsigned char* data = (unsigned char *)(global_buf + sizeof(struct iphdr) + sizeof(struct udphdr));
    memcpy(data, load, load_len);

    ip->ihl = 5;
    ip->version = 4; // ipv4
    ip->tos = 0; // type of service
    ip->tot_len = sizeof(struct iphdr) + sizeof(struct udphdr) + load_len;
    ip->ttl = 64; // TTL  
    ip->protocol = 17; // UDP  
    ip->check = 0;
    // initialized to zero
    ip->saddr = 0;
    ip->daddr = 0;

    udp->source = srcport;
    udp->dest = dstport;
    udp->len = htons(sizeof(struct udphdr) + load_len);

    uint16_t* packet = udp;
    uint32_t sum = 0;
    // pseudo header
    sum += 17 << 8;
    sum += udp->len;
    // header and data
    int i = 0;
    for (i = 0; i < sizeof(struct udphdr) + load_len / 2; ++i) {
        sum += *packet;
        ++packet;
    }
    // pad    
    if ((sizeof(struct udphdr) + load_len) % 2) {
        // they equal
        // sum += htons(*packet << 8);        
        sum += *packet;
    }
    base_check_sum = sum;
    printf("base_check_sum = %d\n", base_check_sum);

    return sd;
}

unsigned short test_udp_check_sum(uint32_t srcip, uint32_t dstip) {
    uint32_t sum = base_check_sum;
    sum += (srcip >> 16);
    sum += (srcip & 0xffff);
    sum += (dstip >> 16);
    sum += (dstip & 0xffff);
    //printf("%d\n", sum);
    sum = (sum >> 16) + (sum & 0xffff);
    sum += (sum >> 16);
    return (unsigned short)(~sum);
}


int send_test_packet(int sock_fd, uint32_t srcip, uint32_t dstip) {
    struct iphdr* ip = (struct iphdr *)global_buf;
    struct udphdr* udp = (struct udphdr *)(global_buf + sizeof(struct iphdr));
    unsigned char* data = (unsigned char *)(global_buf + sizeof(struct iphdr) + sizeof(struct udphdr));
    ip->saddr = srcip;
    ip->daddr = dstip;
    udp->check = test_udp_check_sum(srcip, dstip);

    struct sockaddr_in sin, dst_addr;

    // The address family  
    dst_addr.sin_family = AF_INET;
    // Port numbers  
    dst_addr.sin_port = udp->dest;
    // IP addresses  
    dst_addr.sin_addr.s_addr = dstip;
    if (sendto(sock_fd, global_buf, ip->tot_len, 0, (struct sockaddr *)&dst_addr, sizeof(dst_addr)) < 0) {
        perror("sendto() error");
        return -1;
    }
    return 0;
}

int test_main() {
    //uint32_t srcip_le = ntohl(inet_addr("10.0.0.1"));
    uint32_t dstip = inet_addr("192.168.64.1");
    uint16_t srcport = htons(233);
    uint16_t dstport = htons(1024);
    char* load = "hello, world!";
    int sock = init_test_packet(srcport, dstport, load, strlen(load));
    //for (; srcip_le < (uint32_t)(-1); ++srcip_le) {
    uint32_t srcip = inet_addr("192.168.32.128");
    send_test_packet(sock, srcip, dstip);
    sleep(1);
    //printf("%d", srcip_le);
    //}
    close(sock);
}

struct ip_pair {
    uint32_t src;
    uint32_t dst;
};
struct ip_pair* data;
int packets(uint32_t* dstip) {
    uint16_t srcport = htons(233);
    uint16_t dstport = htons(1024);
    char* load = malloc(PCKT_LEN - 50);
    int sock = init_test_packet(srcport, dstport, load, PCKT_LEN - 50);
    if(dstip) printf("manually set dstip\n");
    uint32_t dddd = inet_addr("192.168.32.1");
    sleep(1);
    clock_t t1 = clock();
    int i = 0;
    for (i = 0; i < 1000000; ++i) {
        int ret = send_test_packet(sock, data[i].src, dddd);
        if (ret) {
            struct in_addr src, dst;
            src.s_addr = data[i].src;
            dst.s_addr = data[i].dst;
            printf("on #%d packet: from %s to %s\n", i, inet_ntoa(src), inet_ntoa(dst));
        }
    }
    clock_t t2 = clock();
    printf("%lf\n", (double)(t2 - t1) / CLOCKS_PER_SEC);
    return 0;
}

int multi(char* filename, int thread_count, char* dstips) {
    pthread_t* threads = malloc(sizeof(pthread_t) * thread_count);
    void* pdstip = NULL;
    if (dstips) { 
        uint32_t dstip = inet_addr(dstips);
        pdstip = &dstip;
    }
    int i;
    for (i = 0; i < thread_count; ++i) {
        pthread_create(&threads[i], NULL, packets, pdstip);
    }
    for (i = 0; i < thread_count; ++i) {
        int ret;
        int* pret = &ret;
        pthread_join(threads[i], &pret);
    }
    return 0;
}

int main(int argc, char* argv[]) {
    //setuid(getpid()); //如果不是root用户，需要获取权限    
    if (argc == 1) {
        test_main();
        return 0;
    }
    else if (argc > 4) {
        full_main(argc, argv);
    }
    else {
        uint32_t srcip = inet_addr("192.168.32.100");
        uint32_t dstip = inet_addr("192.168.32.1");
        char* counts = argc >= 3 ? argv[2] : "2";
        char* dstips = argc >= 4 ? argv[3] : NULL;
        data = malloc(sizeof(struct ip_pair) * 1000000);
        FILE* f = fopen(argv[1], "r");
        char buf[50];
        char src_buf[20];
        char dst_buf[20];
        int i = 0;
        for (i = 0; i < 1000000; ++i) {
            fgets(buf, 50, f);
            sscanf(buf, "%s\t%s", src_buf, dst_buf);
            data[i].src = inet_addr(src_buf);
            data[i].dst = inet_addr(dst_buf);
            if (i == 182)printf("%s %s", src_buf, dst_buf);
        }
        int count;
        sscanf(counts, "%d", &count);
        multi(argv[1], count, dstips);
        //packets(argv[1]);
    }
    return 0;
}
