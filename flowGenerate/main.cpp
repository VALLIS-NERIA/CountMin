#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <iostream>
#include <cstring>
// 48KB
static const int huge_unit = 49152;
// 1KB
static const int unit = 1024;

int main(int argc, char** argv) {
    if (argc < 3) {
        std::cout << "Usage: [traffic (in bytes)] [destip] \n";
        return 0;
    }
    int amount;
    sscanf(argv[1], "%d", &amount);
    auto dest_ip = argv[2];
    int socket_descriptor; //套接口描述字  
    char buf[unit];
    memset(buf, '6', unit);
    struct sockaddr_in address;//处lr/理网络通信的地址  
    address.sin_family = AF_INET;
    auto addr = inet_addr(dest_ip);
    if (addr == INADDR_NONE) {
        std::cerr << "Invalid host: " << dest_ip << std::endl;
        return -1;
    }
    address.sin_addr.s_addr = addr; //这里不一样  
    address.sin_port = htons(23333);

    //创建一个 UDP socket  

    socket_descriptor = socket(AF_INET, SOCK_DGRAM, 0);//IPV4  SOCK_DGRAM 数据报套接字（UDP协议）  

    int dim;
    int total = 0;
    while (amount > 0) {
        if(amount>huge_unit) {
            dim = huge_unit;
        }
        else {
            dim = amount < unit ? amount : unit;
        }
        auto ret = sendto(socket_descriptor, buf, dim, 0, (struct sockaddr *)&address, sizeof(address));
        if (!ret) {
            std::cerr << "Send failed.\n";
            break;
        }
        amount -= dim;
        total += dim;
    }

    std::cout << total << " bytes has been sent to " << dest_ip << ".\n";

}
