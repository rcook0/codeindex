// value should not be indexed here: value foo bar
public class C {
  /* block comment with identifiers: alpha beta gamma */
  private int alpha = 3; // inline comment mentions beta
  public void beta() { int gamma = alpha; }
}
