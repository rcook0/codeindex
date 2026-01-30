#include <iostream>
using namespace std;
int max2(int a, int b) { return a > b ? a : b; }
int main() {
  int value = 1;
  int m = max2(value, 2);
  std::cout << "value=" << m << std::endl;
  return 0;
}
