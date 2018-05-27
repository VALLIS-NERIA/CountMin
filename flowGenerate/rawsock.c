#include <unistd.h>  
#include <stdio.h>  
#include <sys/socket.h>  
#include <netinet/ip.h>  
#include <netinet/udp.h>  
#include<memory.h>  
#include<stdlib.h>  
#include <linux/if_ether.h>  
#include <linux/if_packet.h> // sockaddr_ll  
#include<arpa/inet.h>  
#include<netinet/if_ether.h>  
  
// The packet length  
#define PCKT_LEN 100  

  
unsigned short udp_check_sum(uint32_t srcip, uint32_t dstip, uint16_t* packet, size_t byte_len){
    struct udphdr* hdr = (struct udphdr*)packet;
    uint32_t sum = 0;
    // pseudo header
    printf("len = %d\n", byte_len);
    sum += (srcip >> 16);
    sum += (srcip & 0xffff);
    sum += (dstip >> 16);
    sum += (dstip & 0xffff);
    sum += 17<<8;
    sum += hdr->len;
    printf("%d\n", sum);
    // header and data
    int i = 0;
    for(i = 0; i < byte_len / 2; ++i){
        sum += *packet;
        ++packet;
    }
    // pad    
    if (byte_len % 2){
        // they equal
        // sum += htons(*packet << 8);        
        sum += *packet;        
    }
    printf("sum = %d\n", sum);
    
    sum = (sum >> 16) + (sum & 0xffff);  
    sum += (sum >> 16);  
    return (unsigned short)(~sum);  
}

int send_test_packet(int sock_fd, uint32_t srcip, uint32_t dstip) {

}

// ALL parameters should be BIG endian
int send_packet(int sock_fd, uint32_t srcip, uint32_t dstip, uint16_t srcport, uint16_t dstport, char* load, size_t load_len){
    char buffer[PCKT_LEN];
    memset(buffer, 0, PCKT_LEN);  
    struct iphdr *ip = (struct iphdr *) buffer;  
    struct udphdr *udp = (struct udphdr *) (buffer + sizeof(struct iphdr));  
    unsigned char *data = (unsigned char *) (buffer + sizeof(struct iphdr) + sizeof(struct udphdr));
    memcpy(data,load,load_len);

    struct sockaddr_in sin, dst_addr;  
    int one = 1;

    // The address family  
    sin.sin_family = AF_INET;  
    dst_addr.sin_family = AF_INET;  
    // Port numbers  
    sin.sin_port = srcport;  
    dst_addr.sin_port = dstport;  
    // IP addresses  
    sin.sin_addr.s_addr = srcip;  
    dst_addr.sin_addr.s_addr = dstip;  
  
    // Fabricate the IP header or we can use the  
    // standard header structures but assign our own values.  
    ip->ihl = 5;  
    ip->version = 4;//报头长度，4*32=128bit=16B  
    ip->tos = 0; // 服务类型  
    ip->tot_len = ((sizeof(struct iphdr) + sizeof(struct udphdr) + load_len));  
    //ip->id = htons(54321);//可以不写  
    ip->ttl = 64; // hops生存周期  
    ip->protocol = 17; // UDP  
    ip->check = 0;  
    // Source IP address, can use spoofed address here!!!  
    ip->saddr = srcip;  
    // The destination IP address  
    ip->daddr = dstip;  
  
    // Fabricate the UDP header. Source port number, redundant  
    udp->source = srcport;//源端口  
    // Destination port number  
    udp->dest = dstport;//目的端口  
    udp->len = htons(sizeof(struct udphdr)+load_len);//长度  
    udp->check = udp_check_sum(ip->saddr, ip->daddr, udp, sizeof(struct udphdr) + load_len);
    setuid(getpid());//如果不是root用户，需要获取权限    
    // printf("Using Source IP: %s port: %u, Target IP: %s port: %u.\n", argv[1], atoi(argv[2]), argv[3], atoi(argv[4]));
    if (sendto(sock_fd, buffer, ip->tot_len, 0, (struct sockaddr *)&dst_addr, sizeof(dst_addr)) < 0) {  
        perror("sendto() error");  
        exit(-1);  
    }    
}

  
// Source IP, source port, target IP, target port from the command line arguments  
int main(int argc, char *argv[])  
{  
    int sd;  
    uint32_t srcip, dstip;
    uint16_t srcport, dstport;
    if (argc != 5) {  
        printf("- Invalid parameters!!!\n");  
        printf("- Usage %s <source hostname/IP> <source port> <target hostname/IP> <target port>\n", argv[0]);  
        exit(-1);  
    }
    srcport = htons(atoi(argv[2]));
    dstport = htons(atoi(argv[4]));
    srcip = inet_addr(argv[1]);
    dstip = inet_addr(argv[3]);
    // Create a raw socket with UDP protocol  
    sd = socket(AF_INET, SOCK_RAW, IPPROTO_UDP);  
    if (sd < 0) {  
        perror("socket() error");  
        // If something wrong just exit  
        exit(-1);  
    }  
    else {
        printf("socket() - Using SOCK_RAW socket and UDP protocol is OK.\n");  
    }
    //IPPROTO_TP说明用户自己填写IP报文  
    //IP_HDRINCL表示由内核来计算IP报文的头部校验和，和填充那个IP的id   
    int dum;
    if (setsockopt(sd, IPPROTO_IP, IP_HDRINCL, &dum, sizeof(int))) {
        perror("setsockopt() error");  
        exit(-1);  
    }  
    else {
        printf("setsockopt() is OK.\n");
    }
    char* str = "hello";
    send_packet(sd,srcip,dstip,srcport,dstport,str,strlen(str));
    close(sd);  
    return 0;  
}  