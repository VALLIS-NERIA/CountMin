template<typename T>
void g(const T& val){}

int main() {
    int i = 0;
    const int ci = 0;
    auto x = i = ci;
    g<int>(i=ci);
}