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
#include<iomanip>  
#include<iostream>  
  
// The packet length  
#define PCKT_LEN 100  
  
//UDP的伪头部  
struct UDP_PSD_Header  
{  
    u_int32_t src;  
    u_int32_t des;  
    u_int8_t  mbz;  
    u_int8_t ptcl;  
    u_int16_t len;  
};  
//计算校验和  
unsigned short csum(unsigned short *buf, int nwords)  
{   
    unsigned long sum;  
    for (sum = 0; nwords > 0; nwords--)  
    {  
        sum += *buf++;  
    }  
    sum = (sum >> 16) + (sum & 0xffff);  
    sum += (sum >> 16);  
    return (unsigned short)(~sum);  
}  
  
  
// Source IP, source port, target IP, target port from the command line arguments  
int main(int argc, char *argv[])  
{  
    int sd;  
    char buffer[PCKT_LEN] ;  
    //查询www.chongfer.cn的DNS报文  
    unsigned char DNS[] = { 0xd8, 0xcb , 0x01, 0x00, 0x00, 0x01, 0x00 ,0x00,  
        0x00, 0x00, 0x00, 0x00, 0x03, 0x77, 0x77, 0x77,  
        0x08, 0x63, 0x68, 0x6f, 0x6e, 0x67, 0x66, 0x65,  
        0x72, 0x02, 0x63, 0x6e, 0x00, 0x00, 0x01, 0x00,  
        0x01 };  
    struct iphdr *ip = (struct iphdr *) buffer;  
    struct udphdr *udp = (struct udphdr *) (buffer + sizeof(struct iphdr));  
    // Source and destination addresses: IP and port  
    //struct sockaddr_in sin, din;  
    int  one = 1;  
    const int *val = &one;  
    //缓存清零  
    memset(buffer, 0, PCKT_LEN);  
  
    if (argc != 5)  
    {  
        printf("- Invalid parameters!!!\n");  
        printf("- Usage %s <source hostname/IP> <source port> <target hostname/IP> <target port>\n", argv[0]);  
        exit(-1);  
    }  
  
    // Create a raw socket with UDP protocol  
    sd = socket(AF_INET, SOCK_RAW, IPPROTO_UDP);  
    if (sd < 0)  
    {  
        perror("socket() error");  
        // If something wrong just exit  
        exit(-1);  
    }  
    else  
        printf("socket() - Using SOCK_RAW socket and UDP protocol is OK.\n");  
    //IPPROTO_TP说明用户自己填写IP报文  
    //IP_HDRINCL表示由内核来计算IP报文的头部校验和，和填充那个IP的id   
    if (setsockopt(sd, IPPROTO_IP, IP_HDRINCL, val, sizeof(int)))  
    {  
        perror("setsockopt() error");  
        exit(-1);  
    }  
    else  
        printf("setsockopt() is OK.\n");  
  
    // The source is redundant, may be used later if needed  
    // The address family  
    //sin.sin_family = AF_INET;  
    //din.sin_family = AF_INET;  
    // Port numbers  
    //sin.sin_port = htons(atoi(argv[2]));  
    //din.sin_port = htons(atoi(argv[4]));  
    // IP addresses  
    //sin.sin_addr.s_addr = inet_addr(argv[1]);  
    //din.sin_addr.s_addr = inet_addr(argv[3]);  
  
    // Fabricate the IP header or we can use the  
    // standard header structures but assign our own values.  
    ip->ihl = 5;  
    ip->version = 4;//报头长度，4*32=128bit=16B  
    ip->tos = 0; // 服务类型  
    ip->tot_len = ((sizeof(struct iphdr) + sizeof(struct udphdr)+sizeof(DNS)));  
    //ip->id = htons(54321);//可以不写  
    ip->ttl = 64; // hops生存周期  
    ip->protocol = 17; // UDP  
    ip->check = 0;  
    // Source IP address, can use spoofed address here!!!  
    ip->saddr = inet_addr(argv[1]);  
    // The destination IP address  
    ip->daddr = inet_addr(argv[3]);  
  
    // Fabricate the UDP header. Source port number, redundant  
    udp->source = htons(atoi(argv[2]));//源端口  
    // Destination port number  
    udp->dest = htons(atoi(argv[4]));//目的端口  
    udp->len = htons(sizeof(struct udphdr)+sizeof(DNS));//长度  
    //forUDPCheckSum用来计算UDP报文的校验和用  
    //UDP校验和需要计算 伪头部、UDP头部和数据部分  
    char * forUDPCheckSum = new char[sizeof(UDP_PSD_Header) + sizeof(udphdr)+sizeof(DNS)+1];  
    memset(forUDPCheckSum, 0, sizeof(UDP_PSD_Header) + sizeof(udphdr) + sizeof(DNS) + 1);  
    UDP_PSD_Header * udp_psd_Header = (UDP_PSD_Header *)forUDPCheckSum;  
    udp_psd_Header->src = inet_addr(argv[1]);  
    udp_psd_Header->des = inet_addr(argv[3]);  
    udp_psd_Header->mbz = 0;  
    udp_psd_Header->ptcl = 17;  
    udp_psd_Header->len = htons(sizeof(udphdr)+sizeof(DNS));  
    memcpy(forUDPCheckSum + sizeof(UDP_PSD_Header), udp, sizeof(udphdr));  
    memcpy(forUDPCheckSum + sizeof(UDP_PSD_Header) + sizeof(udphdr), DNS, sizeof(DNS));  
  
    //ip->check = csum((unsigned short *)ip, sizeof(iphdr)/2);//可以不用算  
    //计算UDP的校验和，因为报文长度可能为单数，所以计算的时候要补0  
    udp->check = csum((unsigned short *)forUDPCheckSum,(sizeof(udphdr)+sizeof(UDP_PSD_Header)+sizeof(DNS)+1)/2);  
  
    setuid(getpid());//如果不是root用户，需要获取权限      
  
    // Send loop, send for every 2 second for 2000000 count  
    printf("Trying...\n");  
    printf("Using raw socket and UDP protocol\n");  
    printf("Using Source IP: %s port: %u, Target IP: %s port: %u.\n", argv[1], atoi(argv[2]), argv[3], atoi(argv[4]));  
    std::cout << "Ip length:" << ip->tot_len << std::endl;  
    int count;  
    //将DNS报文拷贝进缓存区  
    memcpy(buffer + sizeof(iphdr) + sizeof(udphdr), DNS, sizeof(DNS));  
      
    for (count = 1; count <= 2000000; count++)  
    {  
          
        if (sendto(sd, buffer, ip->tot_len, 0, (struct sockaddr *)&din, sizeof(din)) < 0)  
            // Verify  
        {  
            perror("sendto() error");  
            exit(-1);  
        }  
        else  
        {  
            printf("Count #%u - sendto() is OK.\n", count);  
            sleep(2);  
        }  
    }  
    close(sd);  
    return 0;  
}  