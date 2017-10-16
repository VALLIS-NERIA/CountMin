#pragma once
#include <map>
#include <vector>
#include <cstdint>
using std::vector;
using std::map;
typedef int64_t int64;
typedef uint32_t uint32;

class CountArray {
private:
    std::vector<int64> count;
    int w;
    // hash seed is used as a mask
    int hash_seed;
    //pthread_mutex_t* mutex;
public:
    CountArray();

    CountArray(int _w);

    ~CountArray();

    // hash anything into w
    int hash(void* key, int key_length);


    int64 operator [](int index);

    int add(void* key, int key_length, int64 value);

    int64 query(void* key, int key_length);
};

class SwitchKetch {
private:
    std::vector<CountArray> count;
    int w;
    int d;
public:
	SwitchKetch(){}

    SwitchKetch(int _w, int _d);

    ~SwitchKetch();

    int add(void* key, int key_length, int64 value);
    int64 query(void* key, int key_length);
};

class CountMin {
private:
    std::map<void*, SwitchKetch> map;
    int w;
    int d;
public:
    CountMin(int _w, int _d);
    int add(void* _datapath, int _dp_length, void* _flow, int _flow_length, int _packet_size);
    int64 query(void* _flow, int _flow_length);
};