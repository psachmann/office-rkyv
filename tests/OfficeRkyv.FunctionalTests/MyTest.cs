namespace MyTests;

public class MyTests // No [TestClass] needed!
{
    [Test] // Available without explicit using statement
    public async Task MyTest()
    {
        var result = true;

        await Assert.That(result).IsTrue(); // Assert is available automatically
    }
}
