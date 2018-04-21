//#include <stddef.h>
//#include <stdint.h>
//#include <stdlib.h>
//#include <time.h>
#include <windows.h>
#include <stdio.h>


#ifdef __cplusplus
#include <stddef.h>
#include <stdint.h>
#include <stdlib.h>
#include <time.h>
typedef uint32_t elemtype;

struct flow_key {
    uint32_t srcip;
    uint32_t dstip;
    uint16_t srcport;
    uint16_t dstport;
    uint16_t protocol;

    bool operator ==(const flow_key other) const {
        return srcip == other.srcip && dstip == other.dstip && srcport == other.srcport &&
            dstport == other.dstport && protocol == other.protocol;
    }
};

bool equal(const flow_key* lhs,const flow_key* rhs) {
    return lhs->srcip == rhs->srcip && lhs->dstip == rhs->dstip && lhs->srcport == rhs->srcport &&
        lhs->dstport == rhs->dstport && lhs->protocol == rhs->protocol;
}

int get_hash_code(const flow_key* key) {
    int hashCode = (int)key->srcip;
    hashCode = (hashCode * 397) ^ (int)key->dstip;
    hashCode = (hashCode * 397) ^ (int)key->srcport;
    hashCode = (hashCode * 397) ^ (int)key->dstport;
    hashCode = (hashCode * 397) ^ (int)key->protocol;
    return hashCode;
}

inline int rand_byte() {
    return rand() & 0xff;
}

inline uint32_t rand_uint32() {
    uint32_t i = 0;
    i += rand_byte();
    i <<= 1;
    i += rand_byte();
    i <<= 1;
    i += rand_byte();
    i <<= 1;
    i += rand_byte();
    return i;
}

inline uint32_t rand_uint16() {
    uint16_t i = 0;
    i += rand_byte();
    i <<= 1;
    i += rand_byte();
    return i;
}



class count_max {
    class line {
    public:
        size_t w;
        flow_key* keys;
        elemtype* counters;

        void update(const flow_key* key, const elemtype value) const {
            const size_t index = get_hash_code(key) % w;
            const flow_key& current_key = keys[index];
            if (current_key == *key) {
                counters[index] += value;
            }
            else {
                auto now = counters[index];
                if (value > now) {
                    counters[index] = value - now;
                    keys[index] = *key;
                }
                else {
                    counters[index] -= value;
                }
            }
        }

        line(size_t w) {
            this->w = w;
            this->keys = new flow_key[this->w];
            this->counters = new elemtype[this->w];
        }

        ~line() {
            delete[] keys;
            delete[] counters;
        }
    };

public:
    size_t w;
    size_t d;
    line** lines;

    void update(const flow_key* key, const elemtype value) const {
        for (int i = 0; i < d; ++i) {
            lines[i]->update(key, value);
        }
    }

    count_max(size_t w, size_t d) {
        this->w = w;
        this->d = d;
        this->lines = new line*[d];
        for (int i = 0; i < d; ++i) {
            lines[i] = new line(w);
        }
    }

    ~count_max() {
        delete[] lines;
    }
};

#endif

#ifndef __cplusplus
#include "countsketch.h"
#include "fss.c"
#include "countmax.h"
#endif
const long count = 100000;
const int work_round = 5;
const struct flow_key* flows;
const elemtype* traffics;

struct flow_key* flow_gen(size_t size) {
    struct flow_key* array = (struct flow_key*) malloc(sizeof(struct flow_key)* size);
    for (int i = 0; i < size; ++i) {
        array[i].srcip = rand_uint32();
        array[i].dstip = rand_uint32();
        array[i].srcport = rand_uint16();
        array[i].dstport = rand_uint16();
        array[i].protocol = rand_uint16();
    }
    return array;
}

elemtype* traffic_gen(size_t size) {
    elemtype* array = (elemtype*)malloc(sizeof(elemtype)*size);
    FILE* f = fopen("D:\\Code\\Repos\\countMin\\CountMaxLoadTest\\udp.txt", "r");
    char buf[30];
    for (int i = 0; i < size; ++i) {
        fgets(buf, 30, f);
        int traffic;
        sscanf(buf, "%d", &traffic);
        array[i] = traffic;
    }
    fclose(f);
    return array;
}



#ifndef __cplusplus
int fss_cpu = 0;
int cs_cpu = 0;
int cm_cpu = 0;
static void do_work_3(const int w) {
    heap_count_1 = 0;
    heap_count_2 = 0;
    ht_count = 0;
    int packet = 0;
    auto cs = new_countmax_sketch(w, 2);
    LARGE_INTEGER begin, end, frequency;
    //std::cout << "begin" << std::endl;
    QueryPerformanceFrequency(&frequency);
    QueryPerformanceCounter(&begin);
    for (int i = 0; i < count; ++i) {
        elemtype t0 = traffics[i];
        elemtype t = t0;
        while (t0) {
            t = t0 > 500 ? 500 : t0;
            t0 -= t;
            countmax_sketch_update(cs, &flows[i], t);
            ++packet;
        }
        //cm->update(&flows[i], traffics[i]);
    }
    QueryPerformanceCounter(&end);
    cm_cpu += (int)((double)(end.QuadPart - begin.QuadPart) * 1000 / packet);
    delete_countmax_sketch(cs);
    //printf("%d\t%lf\t%d\t%d\n", w, (double)(end.QuadPart - begin.QuadPart) * 1000 / packet, packet, ht_count);
    //printf("%lf\n", (double)(end.QuadPart - begin.QuadPart) * 1000 / packet);


    //printf("%d\t%d\n", heap_count_1,heap_count_2);
    //std::cout << d << '\t' << (double)(end.QuadPart - begin.QuadPart) * 1000 / count << std::endl;
}

void do_work_2(const int w) {
    heap_count_1 = 0;
    heap_count_2 = 0;
    ht_count = 0;
    int packet = 0;
    auto cs = new_countsketch_sketch(w, 2);
    LARGE_INTEGER begin, end, frequency;
    //std::cout << "begin" << std::endl;
    QueryPerformanceFrequency(&frequency);
    QueryPerformanceCounter(&begin);
    for (int i = 0; i < count; ++i) {
        elemtype t0 = traffics[i];
        elemtype t = t0;
        while (t0) {
            t = t0 > 500 ? 500 : t0;
            t0 -= t;
            countsketch_sketch_update(cs, flows[i], t);
            ++packet;
        }
        //cm->update(&flows[i], traffics[i]);
    }
    QueryPerformanceCounter(&end);
    cs_cpu += (int)((double)(end.QuadPart - begin.QuadPart) * 1000 / packet);
    delete_countsketch_sketch(cs);
    //printf("%d\t%lf\t%d\t%d\n", w, (double)(end.QuadPart - begin.QuadPart) * 1000 / packet, packet, ht_count);
    //printf("%lf\n", (double)(end.QuadPart - begin.QuadPart) * 1000 / packet);


    //printf("%d\t%d\n", heap_count_1,heap_count_2);
    //std::cout << d << '\t' << (double)(end.QuadPart - begin.QuadPart) * 1000 / count << std::endl;
}

void do_work_1(const int w) {
    heap_count_1 = 0;
    heap_count_2 = 0;
    int packet = 0;
    auto cs = new_fss_sketch(w);
    LARGE_INTEGER begin, end, frequency;
    //std::cout << "begin" << std::endl;
    QueryPerformanceFrequency(&frequency);
    QueryPerformanceCounter(&begin);
    for (int i = 0; i < count; ++i) {
        if (i == 71) {
            i = 71;
        }
        elemtype t0 = traffics[i];
        elemtype t = t0;
        while (t0) {
            t = t0 > 500 ? 500 : t0;
            t0 -= t;
            fss_sketch_update(cs, flows[i], t);
            ++packet;
        }
        //cm->update(&flows[i], traffics[i]);
    }
    QueryPerformanceCounter(&end);
    //printf("%d\t%lf\t%d\t%d\n", w, (double)(end.QuadPart - begin.QuadPart) * 1000 / packet, packet,ht_count);
    //printf("%d\t%lf\t", w, (double)(end.QuadPart - begin.QuadPart) * 1000 / packet);
    fss_cpu += (int)((double)(end.QuadPart - begin.QuadPart) * 1000 / packet);
    delete_fss_sketch(cs);

    //do_work_2(w);
    //printf("%d\t%d\n", heap_count_1,heap_count_2);
    //std::cout << d << '\t' << (double)(end.QuadPart - begin.QuadPart) * 1000 / count << std::endl;
}

void do_work(const int w) {
    fss_cpu = 0;
    cs_cpu = 0;
    cm_cpu = 0;
    for(int i=0;i<work_round;i++) {

        do_work_1(w);

    }
    for (int i = 0; i<work_round; i++) {

        do_work_2(w);

    }
    for (int i = 0; i<work_round; i++) {

        do_work_3(w);

    }
        printf("%d\t%d\t%d\t%d\n", w, fss_cpu/work_round,cs_cpu/work_round,cm_cpu/work_round);
}


#endif

#ifdef __cplusplus
int cm_cpu = 0;
void do_work_1(const int w) {
    int packet = 0;
    auto cm = new count_max(w, 2);
    LARGE_INTEGER begin, end, frequency;
    //std::cout << "begin" << std::endl;
    QueryPerformanceFrequency(&frequency);
    QueryPerformanceCounter(&begin);
    for (int i = 0; i < count; ++i) {
        if (i == 71) {
            i = 71;
        }
        elemtype t0 = traffics[i];
        elemtype t = t0;
        while (t0) {
            t = t0 > 1500 ? 1500 : t0;
            t0 -= t;
            cm->update(&flows[i], t);
            ++packet;
        }
        //cm->update(&flows[i], traffics[i]);
    }
    QueryPerformanceCounter(&end);
    cm_cpu += (int)((double)(end.QuadPart - begin.QuadPart) * 1000 / packet);
    //printf("%d\t%lf\t%d\n", w, (double)(end.QuadPart - begin.QuadPart) * 1000 / packet,packet);
    //printf("%d\t%d\n", heap_count_1,heap_count_2);
    //std::cout << d << '\t' << (double)(end.QuadPart - begin.QuadPart) * 1000 / count << std::endl;
}

void do_work(const int w) {
    cm_cpu = 0;
    for (int i = 0; i<50; i++) {

        do_work_1(w);

    }
    printf("%d\t%d\n", w, cm_cpu / 50);
}
#endif

int main() {
    flows = flow_gen(count);
    traffics = traffic_gen(count);
    //std::vector<int> v = { 1,2,3,4,5,6,7,8,9,10 };
    do_work(500);
    for (int d = 500; d < 10000; d += 1500) {
    do_work(d);
    }
    //for (int d = 500; d < 4000; d += 500) {
    //do_work_2(1000);
    //}
    system("pause");
}
