using GarnetDG.Reactivity;

var ctx = new ReactivityContext();

var test1 = ctx.Reactive<IReactiveTest1>(new ReactiveTest1 { TestInt = 5, TestString = "Hello" });
ctx.WatchEffect(() =>
{
    Console.WriteLine(test1.TestString);
});
test1.TestString = "asdf";

Console.WriteLine("=======");

var test2 = ctx.Reactive<IReactiveTest2>(new ReactiveTest2 { Test = new ReactiveTest1 { TestInt = 1, TestString = "test2" } });
ctx.WatchEffect(() =>
{
    Console.WriteLine(test2.Test.TestString);
});
test2.Test.TestString = "foo";


public interface IReactiveTest1 : IReactive
{
    public int TestInt { get; set; }
    public string TestString { get; set; }
}
public class ReactiveTest1 : IReactiveTest1
{
    public int TestInt { get; set; }
    public string TestString { get; set; } = "";
}

public interface IReactiveTest2 : IReactive
{
    public IReactiveTest1 Test { get; set; }
}

public class ReactiveTest2 : IReactiveTest2
{
    public IReactiveTest1 Test { get; set; } = null!;
}
