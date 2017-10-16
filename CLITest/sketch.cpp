#include "sketch.h"
#include "md5.h"

/*   implementation   */
CountMin::CountMin(int _w, int _d) {
    this->w = _w;
    this->d = _d;
    map = std::map<void *, SwitchKetch>();
}

int CountMin::add(void* _datapath, int _dp_length, void* _flow, int _flow_length, int _packet_size) {
    if (map.count(_datapath) != 1) {
        map[_datapath] = SwitchKetch(w, d);
    }
    map[_datapath].add(_flow, _flow_length, _packet_size);
    return 0;
}

int64 CountMin::query(void* _flow, int _flow_length) {
    int64 min = 999999999999L;
    for (auto& item : map) {
        auto t = item.second.query(_flow, _flow_length);
        if (t < min)min = t;
    }
    return min;
}

/*   implementation   */
SwitchKetch::SwitchKetch(int _w, int _d) {
    this->w = _w;
    this->d = _d;
    this->count = vector<CountArray>(d);
    for (int i = 0; i < d; i++) {
        count[i] = CountArray(w);
    }
}

SwitchKetch::~SwitchKetch() {

}

int SwitchKetch::add(void* key, int key_length, int64 value) {
    for (auto& array : count) {
        array.add(key, key_length, value);
    }
    return 0;
}

int64 SwitchKetch::query(void* key, int key_length) {
    int64 min = 999999999999L;
    for (auto& array : count) {
        auto t = array.query(key, key_length);
        if (t < min)min = t;
    }
    return min;
}

/*   implementation   */
int64 CountArray::operator[](int index) {
    return count[index];
}

CountArray::CountArray(int _w) {
    this->w = _w;
    this->count = std::vector<int64>(_w);
    this->hash_seed = rand();
    //mutex = new pthread_mutex_t();
    //int err = pthread_mutex_init(mutex, NULL);
}

CountArray::CountArray() {

}

CountArray::~CountArray() {
    //delete[] count;
}

int CountArray::hash(void* key, int key_length) {
    unsigned char* tmp = (unsigned char*)malloc(key_length);
    memcpy(tmp, key, key_length);
    unsigned char buf[16];
    MD5_CTX md5;
    MD5Init(&md5);
    MD5Update(&md5, tmp, key_length);
    MD5Final(&md5, buf);
    uint32* ibuf = (uint32*)buf;
    int h = ibuf[0] + ibuf[1] + ibuf[2] + ibuf[3];
    h = h / w;
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


int CountArray::add(void* key, int key_length, int64 value) {
    //pthread_mutex_lock(mutex);
    int idx = hash(key, key_length);
    count[idx] += value;
    return count[idx];
    //pthread_mutex_unlock(mutex);
}

int64 CountArray::query(void* key, int key_length) {
    int idx = hash(key, key_length);
    return count[idx];
}




CountMin sketch(0,0);

extern "C" {
    int sketch_init(int _w, int _d) {
        sketch = CountMin(_w, _d);
        return 0;
    }

    int sketch_add(void* _switch, int _switch_length, void* _flow, int _flow_length, int _packet_size) {
        return sketch.add(_switch, _switch_length, _flow, _flow_length, _packet_size);
    }

    int64 sketch_query(void * _flow, int _flow_length) {
        return sketch.query(_flow, _flow_length);
    }
}
