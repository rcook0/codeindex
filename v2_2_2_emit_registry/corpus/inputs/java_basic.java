package demo;
public class Hello {
  private int value = 1;
  public static int max(int a, int b) { return a > b ? a : b; }
  public void run() { int m = max(value, 2); System.out.println("value=" + m); }
}
