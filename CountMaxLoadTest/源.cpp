#include <stdint.h>
#include <stdlib.h>
#include <time.h>
#include <windows.h>
#include <iostream>
#include <vector>
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

flow_key* flow_gen(const size_t size) {
    const auto array = new flow_key[size];
    for (int i = 0; i < size; ++i) {
        array[i].srcip = rand_uint32();
        array[i].dstip = rand_uint32();
        array[i].srcport = rand_uint16();
        array[i].dstport = rand_uint16();
        array[i].protocol = rand_uint16();
    }
    return array;
}

elemtype* traffic_gen(const size_t size) {
    const auto array = new elemtype[size];
    for (int i = 0; i < size; ++i) {
        array[i] = rand_uint32();
    }
    return array;
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

const auto count = 10000000;
const auto flows = flow_gen(count);
const auto traffics = traffic_gen(count);

void do_work(const int d) {
    const auto cm = new count_max(3000, d);
    LARGE_INTEGER begin, end, frequency;
    //std::cout << "begin" << std::endl;
    QueryPerformanceCounter(&begin);
    QueryPerformanceFrequency(&frequency);
    for (int i = 0; i < count; ++i) {
        cm->update(&flows[i], traffics[i]);
    }
    QueryPerformanceCounter(&end);
    std::cout << d << '\t' << (double)(end.QuadPart - begin.QuadPart) * 1000 / count << std::endl;
}

int main() {
    std::vector<int> v = { 1,2,3,4,5,6,7,8,9,10 };
    for (const auto d : v) {
        do_work(d);
    }
    system("pause");
}
