#include <map>
#include <vector>
#include <cstring>
#include <pthread.h>
using std::vector;
using std::map;
typedef __int64_t int64;
typedef __uint32_t uint32;

class CountArray {
private:
    int64* count;
    int w;
    // hash seed is used as a mask
    int hash_seed;
    pthread_mutex_t* mutex;
public:
    CountArray();

    CountArray(int _w);

    ~CountArray();

    // hash anything into w
    int hash(void* key, int key_length);

    uint32 move(uint32 value, uint32 n);

    int64 operator [](int index);

    int add(void* key, int key_length, int64 value);

    int64 query(void* key, int key_length);
};

class CountMin {
private:
    std::vector<CountArray> count;
    int w;
    int d;
public:
    CountMin();
    CountMin(int _w, int _d);

    ~CountMin();

    int add(void* key, int key_length, int64 value);
    int64 query(void* key, int key_length);
};

class Sketch {
private:
    std::map<void*, CountMin> map;
    int w;
    int d;
public:
    Sketch(int _w, int _d);
    int add(void* _datapath, int _dp_length, void* _flow, int _flow_length, int _packet_size);
    int64 query(void* _flow, int _flow_length);
};