namespace Examples;

using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class GdUnitExampleTest
{
    [TestCase]
    public void test_StringToLower() {
        AssertString("AbcD".ToLower()).IsEqual("abcd");
    }
}