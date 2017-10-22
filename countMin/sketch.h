#pragma once
#ifndef SKETCH_H
#define SKETCH_H 

#include <map>
#include <vector>
#include <string.h>
#include "md5.h"
#include <iostream>
typedef __int64_t int64;
typedef __uint32_t uint32;
typedef __uint64_t uint64;

template <typename TKey, typename TValue>
class count_line {
protected:
    std::vector<TValue> count;
    int w;
    // hash seed is used as a mask
    int hash_seed;
    pthread_mutex_t* mutex;
public:
    count_line() {}

    count_line(int _w) {
        this->w = _w;
        this->count = std::vector<TValue>(_w);
        this->hash_seed = rand();
        mutex = new pthread_mutex_t();
        int err = pthread_mutex_init(mutex, NULL);
    }

    virtual ~count_line() {}

    // hash anything into w
    int hash(TKey _key) {
        char* key = (char*)&_key;
        size_t key_length = sizeof(key);
        unsigned char* tmp = (unsigned char*)malloc(key_length);
        memcpy(tmp, key, key_length);
        unsigned char buf[16];
        MD5_CTX md5;
        MD5Init(&md5);
        MD5Update(&md5, tmp, key_length);
        MD5Final(&md5, buf);
        uint32* ibuf = (uint32*)buf;
        uint h = ibuf[0] + ibuf[1] + ibuf[2] + ibuf[3];
        h = (h ^ hash_seed) % w;
        free(tmp);
        return h;
        //// in bytes.
        //int length = key_length;
        //// pad
        //if (key_length % 4)length = key_length + (key_length % 4);
        //uint32* padded_key = new uint32[length / 4];
        //memset(padded_key, 0, length);
        //memcpy(padded_key, flow_id, key_length);
        //uint32 sum = 0;
        //for (int i = 0; i < length / 4; i++) {
        //    sum += padded_key[i];
        //}
        //sum = sum ^ hash_seed;
        //int h = sum / w;
        //return h;
    }


    TValue operator[](int index) {
        return count[index];
    }

    TValue& operator[](TKey key) {
        return &count[hash(key)];
    }

    virtual int add(TKey key, TValue value) {
        pthread_mutex_lock(mutex);
        int idx = hash(key);
        count[idx] += value;
        pthread_mutex_unlock(mutex);
        return count[idx];
    }

    TValue query(TKey key) {
        int idx = hash(key);
        return count[idx];
    }

    virtual void print() {
        for (auto val : count) {
            std::cout << val << '\t';
        }
        std::cout << std::endl;
    }
};

template <typename TKey, typename TValue,
          typename TLine=count_line<TKey, TValue>>
class switch_sketch {
private:
    std::vector<TLine> count;
    int w;
    int d;
public:
    switch_sketch() {}

    switch_sketch(int _w, int _d) {
        this->w = _w;
        this->d = _d;
        this->count = std::vector<count_line<TKey, TValue>>(d);
        for (int i = 0; i < d; i++) {
            count[i] = count_line<TKey, TValue>(w);
        }
    }

    ~switch_sketch() {}

    int add(TKey key, TValue value) {
        for (auto& array : count) {
            array.add(key, value);
        }
        return 0;
    }


    TValue query(TKey key) {
        TValue min = 999999999999L;
        for (auto& array : count) {
            auto t = array.query(key);
            if (t < min)min = t;
        }
        return min;
    }

    void print() {
        for (auto line : count) {
            line.print();
        }
    }
};

template <typename TKey, typename TValue,
          typename TLine = count_line<TKey, TValue>>
class count_min {
private:
    std::map<std::string, switch_sketch<TKey, TValue, TLine>> map;
    int w;
    int d;
public:
    count_min(int _w, int _d) {
        this->w = _w;
        this->d = _d;
        map = std::map<std::string, switch_sketch<TKey, TValue>>();
    }

    int add(std::string datapath, TKey flow_key, int packet_size) {
        if (datapath[0] == '\0') {
            return -1;
        }
        if (map.count(datapath) != 1) {
            map[datapath] = switch_sketch<TKey, TValue>(w, d);
        }
        map[datapath].add(flow_key, packet_size);
        return 0;
    }

    TValue query(TKey flow_key) {
        TValue min = 999999999999L;
        for (auto& item : map) {
            auto t = item.second.query(flow_key);
            if (t < min)min = t;
        }
        return min;
    }

    void print() {
        for (auto pair:map) {
            std::cout << pair.first << std::endl;
            pair.second.print();
        }
    }

};

template <typename TKey, typename TValue>
class count_line_ex:public count_line<TKey, TValue> {

public:
    std::vector<TKey> max_key;

    virtual int add(TKey key, TValue value) {
        pthread_mutex_lock(this->mutex);
        int idx = hash(key);
        if (key == max_key[idx])
            this->count[idx] += value;
        else {
            if (value > this->count[idx]) {
                this->count[idx] = value - this->count[idx];
                max_key[idx] = key;
            }
            else {
                this->count[idx] = this->count[idx] - value;
            }
        }
        pthread_mutex_unlock(this->mutex);
        return this->count[idx];
    }
    virtual void print() {
        for (int i = 0; i < this->w;i++) {
            std::cout << this->max_key[i]<<" : "<<this->count[i] << '\t';
        }
        std::cout << std::endl;
    }
};

#endif // !SKETCH_H
