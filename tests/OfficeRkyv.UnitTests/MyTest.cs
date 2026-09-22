namespace MyTests;

public class MyTests // No [TestClass] needed!
{
    [Test] // Available without explicit using statement
    public async Task MyTest()
    {
        await Assert.That(true).IsTrue(); // Assert is available automatically
    }
}
