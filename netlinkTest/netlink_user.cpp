/*******************************
file:           u_netlink.c
description:    netlink demo
author:         arvik
email:          1216601195@qq.com
blog:           http://blog.csdn.net/u012819339
*******************************/
#include <stdio.h>
#include <stdlib.h>
#include <sys/socket.h>
#include <string.h>
#include <net/sock.h>
//#include <linux/netlink.h>
#include <unistd.h>
#include <errno.h>
#include "md5.h"
#include <map>
typedef int64_t int64;
typedef uint32_t uint32;
#define NETLINK_USER 22
#define USER_MSG (NETLINK_USER + 1)
#define MSG_LEN 4096

#define MAX_PLOAD 100

struct defused_packet {
	void* port_name;
	void* flow_id;
	void* dp_addr;
	void* flow_id_addr;
	int port_name_len;
	int flow_id_size;
	uint packet_size;

	~defused_packet() {
		free(port_name);
		free(flow_id);
	}
};

//void* my__buffer[] = {0,0,0,0,0,0,0,0,0,0,0,0};

struct stat_packet {
	int port_name_len;
	int flow_id_size;
	void* dp_addr;
	void* flow_id_addr;
	__int8_t pad[MSG_LEN];

	defused_packet defuse() {
		defused_packet dpkt;
		dpkt.port_name = malloc(port_name_len);
		dpkt.flow_id = malloc(flow_id_size);
		memcpy(dpkt.port_name, this->pad, port_name_len);
		memcpy(dpkt.flow_id, this->pad + port_name_len, flow_id_size);
		dpkt.port_name_len = this->port_name_len;
		dpkt.flow_id_size = this->flow_id_size;
		dpkt.dp_addr = this->dp_addr;
		dpkt.flow_id_addr = this->flow_id_addr;
		dpkt.packet_size = *(uint*)(this->pad + port_name_len + flow_id_size);
		return dpkt;
	}
};

struct _my_msg {
	nlmsghdr header;
	stat_packet data;
};

int opensocket() {
	int skfd;
	skfd = socket(AF_NETLINK, SOCK_RAW1, USER_MSG);
	if (skfd == -1) {
		printf("create socket error...%s\n", strerror(errno));
		return -1;
	}
	int x;
	printf("%d", x);
	return skfd;
}

sockaddr_nl bind_local(int skfd) {
	sockaddr_nl local;
	memset(&local, 0, sizeof(local));
	local.nl_family = AF_NETLINK;
	local.nl_pid = 50;
	local.nl_groups = 0;
	if (bind(skfd, (sockaddr *)&local, sizeof(local)) != 0) {
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


int hash(void* key, int key_length) {
	unsigned char buf[16];
	MD5_CTX md5;
	MD5Init(&md5);
	MD5Update(&md5, (unsigned char*)key, key_length);
	MD5Final(&md5, buf);
	uint32* ibuf = (uint32*)buf;
	int h = ibuf[0] + ibuf[1] + ibuf[2] + ibuf[3];
	//h = h / w;
	//free(tmp);
	return h;
}

int __hash(void* key, int key_length) {
	// in bytes.
	int length = key_length;
	// pad
	if (key_length % 4)length = key_length + (key_length % 4);
	uint32* padded_key = new uint32[length / 4];
	memset(padded_key, 0, length);
	memcpy(padded_key, key, key_length);
	uint32 sum = 0;
	for (int i = 0; i < length / 4; i++) {
		sum += padded_key[i];
	}
	//sum = sum ^ hash_seed;
	int h = sum;
	return h;
}
netlink_kernel_cfg cfg;
int main(int argc, char** argv) {
	std::map<void*, int> map;
	map[*argv] = 1;
	int ret;
	int skfd = opensocket();
	sockaddr_nl local = bind_local(skfd);

	sockaddr_nl dest_addr;
	memset(&dest_addr, 0, sizeof(dest_addr));
	dest_addr.nl_family = AF_NETLINK;
	dest_addr.nl_pid = 0; // to kernel
	dest_addr.nl_groups = 0;

	//char *data = "hello kernel";
	//ret = nl_send(skfd, data, strlen(data), local.nl_pid, dest_addr);
	//if (!ret) {
	//    perror("sendto error1\n");
	//    close(skfd);
	//    exit(-1);
	//}
	printf("wait kernel msg!\n");

	_my_msg info;
	unsigned int dest_size = sizeof(dest_addr);
	while (1) {
		memset(&info, 0, sizeof(info));
		ret = recvfrom(skfd, &info, sizeof(_my_msg), 0, (sockaddr *)&dest_addr, &dest_size);
		if (!ret) {
			perror("recv form kernel error\n");
			close(skfd);
			exit(-1);
		}
		auto dpkt = info.data.defuse();
		printf("msg receive from kernel:%d %d %p %u %u %d\n", dpkt.port_name_len, dpkt.flow_id_size, dpkt.dp_addr,
		       hash(dpkt.port_name, dpkt.port_name_len), hash(dpkt.flow_id, dpkt.flow_id_size), dpkt.packet_size);
	}
	close(skfd);
	return 0;
}
