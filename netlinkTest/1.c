#include <linux/netlink.h>
#include <errno.h>
#include <stdio.h>
#include <stdlib.h>
#include <sys/socket.h>

#define NETLINK_USER 22
#define USER_MSG (NETLINK_USER + 1)
#define MSG_LEN 4096
#define MAX_PLOAD 100
struct flow_key {
    uint32_t srcip;
    uint32_t dstip;
    uint16_t srcport;
    uint16_t dstport;
    uint16_t protocol;
};
struct _my_msg {
    struct nlmsghdr header;
    struct flow_key data;
};
int opensocket() {
    int skfd;
    skfd = socket(AF_NETLINK, SOCK_RAW, USER_MSG);
    if (skfd == -1) {
        printf("create socket error...%s\n", strerror(errno));
        return -1;
    }
    int x;
    printf("%d", x);
    return skfd;
}

struct sockaddr_nl bind_local(int skfd) {
    struct sockaddr_nl local;
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

int nl_send(int skfd, const void* data, size_t data_len, unsigned int pid, struct sockaddr_nl dest_addr) {
    struct nlmsghdr* nlh = NULL;
    nlh = (struct nlmsghdr *)malloc(NLMSG_SPACE(MAX_PLOAD));
    memset(nlh, 0, sizeof(struct nlmsghdr));
    nlh->nlmsg_len = NLMSG_SPACE(MAX_PLOAD);
    nlh->nlmsg_flags = 0;
    nlh->nlmsg_type = 0;
    nlh->nlmsg_seq = 0;
    nlh->nlmsg_pid = pid; //self port

    memcpy(NLMSG_DATA(nlh), data, data_len);

    int ret = sendto(skfd, nlh, nlh->nlmsg_len, 0, (struct sockaddr *)&dest_addr, sizeof(struct sockaddr_nl));
    free((void *)nlh);
    return ret;
}


int main(int argc, char** argv) {
    int ret;
    int skfd = opensocket();
    struct sockaddr_nl local = bind_local(skfd);

    struct sockaddr_nl dest_addr;
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

    struct _my_msg info;
    unsigned int dest_size = sizeof(dest_addr);
    while (1) {
        memset(&info, 0, sizeof(info));
        ret = recvfrom(skfd, &info, sizeof(struct _my_msg), 0, (struct sockaddr *)&dest_addr, &dest_size);
        if (!ret) {
            perror("recv form kernel error\n");
            close(skfd);
            exit(-1);
        }
    }
    close(skfd);
    return 0;
}

