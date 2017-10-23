#include <stdio.h>
#include <stdlib.h>
#include <sys/socket.h>
#include <string.h>
#include <linux/netlink.h>
#include <sys/epoll.h>
#include <unistd.h>
#include <errno.h>
#include "md5.h"
#include "sketch.h"
#include "netlink.h"
typedef __int64_t int64;
typedef __uint32_t uint32;

std::string chop(char* s) {
    char* s2 = (char*)malloc(strlen(s));
    for (int i = 0; i < strlen(s); i++) {
        if (s[i] == '-') {
            s2[i] = '\0';
            break;
        }
        else {
            s2[i] = s[i];
        }
    }
    auto string = std::string(s2);
    free(s2);
    return string;
}

std::map<std::string, std::map<my_flow_key, uint64>> stats;
std::map<my_flow_key, int> flows;
uint64 get_stats(my_flow_key key) {
    uint64 ret = 0;
    for(auto m : stats) {
        if(!m.second.count(key)) {
            continue;
        }
        else if(m.second[key]>ret) {
            ret = m.second[key];
        }
    }
    return ret;
}

int main(int argc, char** argv) {
    int ret;
    int skfd = opensocket();
    sockaddr_nl local = bind_local(skfd);

    // parameter is ignored.
    //int epoll_fd = epoll_create(1);

    sockaddr_nl dest_addr;
    memset(&dest_addr, 0, sizeof(dest_addr));
    dest_addr.nl_family = AF_NETLINK;
    dest_addr.nl_pid = 0; // to kernel
    dest_addr.nl_groups = 0;
    unsigned int dest_size = sizeof(dest_addr);
    my_msg info;


    count_min<my_flow_key, uint64, count_line_ex<my_flow_key, uint64>> cm(5, 2);


    while (true) {
        memset(&info, 0, sizeof(info));
        ret = recvfrom(skfd, &info, sizeof(my_msg), 0, (sockaddr *)&dest_addr, &dest_size);
        if (!ret) {
            perror("recv form kernel error\n");
            close(skfd);
            exit(-1);
        }

        auto spkt = info.data;

        uint64 traffic;
        size_t length;

        auto washed_key = spkt.key.wash();
        switch (spkt.request_type) {
        case NLREQUEST_FETCH:
            traffic = cm.query(washed_key);
            reply_packet reply;
            reply.countmin_traffic = traffic;
            reply.flow = spkt.flow_pointer;
            reply.key = spkt.key;
            length = sizeof(reply);
            printf("flow : %s   cm : %llu   actual : %llu\n", washed_key.to_string().c_str(), traffic, get_stats(washed_key));
            ret = nl_send(skfd, &reply, length, local.nl_pid, dest_addr);
            //cm.print();
            break;
        case NLREQUEST_REPORT:
            if (spkt.packet_size) {
                std::string sw_name = chop(spkt.port_name);
                cm.add(sw_name, washed_key, spkt.packet_size);
                if (!flows.count(washed_key)) {
                    std::cout << "new flow: " << washed_key.to_string() << " with ovs stat " << spkt.flow_traffic << std::endl;
                }
                stats[sw_name][washed_key] += spkt.packet_size;
                flows[washed_key] = 1;
                traffic = cm.query(washed_key);
                //printf("%s: %s %d %lld %lld\n", sw_name.c_str(), key.to_string().c_str(), spkt.packet_size, traffic, spkt.flow_traffic);
            }
            else {
                printf(".");
            }
            //cm.print();
            break;
        default:
            break;
        }
    }
    close(skfd);
    return 0;
}
