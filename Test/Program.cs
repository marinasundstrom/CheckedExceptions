using Test;

MultipleThrows multipleThrows = new MultipleThrows();
multipleThrows.ProcessData1();

Foo();

static void Foo()
{
    throw new NotImplementedException();
}