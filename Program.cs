using GarnetDG.Reactivity;

var ctx = new ReactivityContext();

var test1 = ctx.Reactive<IReactiveTest1>(new ReactiveTest1 { TestInt = 5, TestString = "Hello" });
ctx.WatchEffect(() =>
{
    Console.WriteLine(test1.TestString);
});
test1.TestString = "asdf";


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
