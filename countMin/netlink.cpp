#include"netlink.h"
#include <netinet/in.h>
#include <sstream>


//defused_packet stat_packet::defuse() {
//	defused_packet dpkt;
//	dpkt.port_name = (char*)malloc(port_name_len);
//	dpkt.flow_id = malloc(flow_id_size);
//	//memcpy(dpkt.port_name, this->pad, port_name_len);
//	//memcpy(dpkt.flow_id, this->pad + port_name_len, flow_id_size);
//	dpkt.port_name_len = this->port_name_len;
//	dpkt.flow_id_size = this->flow_id_size;
//	dpkt.packet_size = this->packet_size;
//	for (int i = 0; i < port_name_len; i++) {
//		if (dpkt.port_name[i] == '-' || i == port_name_len - 1) {
//			dpkt.port_name[i] = '\0';
//			break;
//		}
//
//	}
//	return dpkt;
//}

std::string sw_flow_id::to_string() {
    std::string s = "0x";
    //std::unique_ptr<char[]> buff(new char[100]);
    char buff[100];
    for (int i = 0; i < ufid_len / sizeof(uint32); i++) {
        sprintf(buff, "%x", ufid[i]);
        s += buff;
    }
    return s;
}

std::string ip_to_str(uint32 ip) {
    std::stringstream sstr;
    sstr << (ip) % 0x1000000 << '.' << (ip / 0x100) % 0x10000 << '.' << (ip / 0x10000) % 0x100 << '.' << ip / 0x1000000;
    return sstr.str();
}

int ipv4_key::operator==(ipv4_key another) {
    return this->addr.src == another.addr.src && this->addr.dst == another.addr.dst;
}

ipv4_key ipv4_key::wash() {
    ipv4_key another;
    another = *this;
    memset(another.arp.sha, 0, sizeof(another.arp.sha) * sizeof(decltype(another.arp.sha[0])));
    memset(another.arp.tha, 0, sizeof(another.arp.tha) * sizeof(decltype(another.arp.tha[0])));
    return another;
}

int port_key::operator==(port_key another) {
    return this->src == another.src && this->dst == another.dst;
}

port_key port_key::wash() {
    port_key another;
    another = *this;
    another.src = 0;
    another.flags = 0;
    return another;
}

std::string  my_flow_key::to_string() const {
    if(this->ip.addr.src==0&&this->ip.addr.dst==0&&this->port.src==0&&this->port.dst==0) {
        return "null";
    }
    std::stringstream sstr;
    sstr << ip_to_str(this->ip.addr.src) << ':' << this->port.src << " => " << ip_to_str(this->ip.addr.dst) << ':' << ntohs(this->port.dst);
    return sstr.str();
}


int operator==(my_flow_key left,my_flow_key another) {
    return (left.ip == another.ip) && (left.port == another.port);
}

my_flow_key my_flow_key::wash() {
    my_flow_key another;
    another.ip = this->ip.wash();
    another.port = this->port.wash();
    return another;
}

int operator<(my_flow_key left, my_flow_key right) {
    return left.port.dst < right.port.dst;
}

//defused_packet::~defused_packet() {
//	free(port_name);
//	free(flow_id);
//}


int opensocket() {
    int skfd;
    skfd = socket(AF_NETLINK, SOCK_RAW, USER_MSG);
    if (skfd == -1) {
        printf("create socket error...%s\n", strerror(errno));
        return -1;
    }
    int x;
    //printf("%d", x);
    return skfd;
}

sockaddr_nl bind_local(int skfd) {
    sockaddr_nl local;
    memset(&local, 0, sizeof(local));
    local.nl_family = AF_NETLINK;
    local.nl_pid = 50;
    local.nl_groups = 0;
    if (bind(skfd, (struct sockaddr *)&local, sizeof(local)) != 0) {
        printf("bind() error\n");
        close(skfd);
        exit(-1);
    }
    return local;
}

int nl_send(int skfd, const void* data, size_t data_len, unsigned int pid, sockaddr_nl dest_addr) {
    nlmsghdr* nlh = NULL;
    nlh = (struct nlmsghdr *)malloc(NLMSG_SPACE(MAX_PLOAD));
    memset(nlh, 0, sizeof(struct nlmsghdr));
    nlh->nlmsg_len = NLMSG_SPACE(MAX_PLOAD);
    nlh->nlmsg_flags = 0;
    nlh->nlmsg_type = 0;
    nlh->nlmsg_seq = 0;
    nlh->nlmsg_pid = pid; //self port

    memcpy(NLMSG_DATA(nlh), data, data_len);

    int ret = sendto(skfd, nlh, nlh->nlmsg_len, 0, (sockaddr *)&dest_addr, sizeof(sockaddr_nl));
    free((void *)nlh);
    return ret;
}
