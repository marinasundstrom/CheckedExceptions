namespace Test.Cases;

//C.M(); // THROW004: Throwing 'Exception' is too general; use a more specific exception type instead

class C
{
    [Throws(typeof(Exception))]
    public static void M() { }
}