#include <sys/socket.h>
#include <netinet/in.h>  
#include <arpa/inet.h>  
#include <iostream>
#include <cstring>

static const int unit = 1024;
int main(int argc, char** argv) {
    if(argc<3) {
        std::cout << "Usage: [traffic (in bytes)] [destip] \n";
        return 0;
    }
    int amount;
    sscanf(argv[1], "%d", &amount);
    auto dest_ip = argv[2];
    int socket_descriptor; //�׽ӿ�������  
    char buf[unit];
    memset(buf, '6', unit);
    struct sockaddr_in address;//��������ͨ�ŵĵ�ַ  
    address.sin_family = AF_INET;
    address.sin_addr.s_addr = inet_addr(dest_ip);//���ﲻһ��  
    address.sin_port = htons(23333);

    //����һ�� UDP socket  

    socket_descriptor = socket(AF_INET, SOCK_DGRAM, 0);//IPV4  SOCK_DGRAM ���ݱ��׽��֣�UDPЭ�飩  

    int dim;
    while(amount>0) {
        dim = amount < unit ? amount : unit;
        sendto(socket_descriptor, buf, dim, 0, (struct sockaddr *)&address, sizeof(address));
        amount -= dim;
    }

}