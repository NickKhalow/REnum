namespace REnum.Tests;

[REnum]
[REnumField(typeof(User))]
[REnumFieldEmpty("NameTaken")]
[REnumFieldEmpty("InvalidName")]
public partial struct UserResult
{
}


public class User
{
    public readonly int Id;
    public readonly string Name;
    
    public User(int id, string name)
    {
        Id = id;
        Name = name;
    }
}


public class EmptyFieldTest
{
    [Test]
    public void CreateAndCompareTest()
    {
        var user = new User(10, "Mike");
        var userResult = UserResult.FromUser(user);

        Assert.That(userResult.IsInvalidName(), Is.False);
        Assert.That(userResult.IsUser(out User? receivedUser), Is.True);
        Assert.That(user, Is.EqualTo(receivedUser));

        var secondResult = UserResult.InvalidName();
        Assert.That(userResult, Is.Not.EqualTo(secondResult));
    }

    [Test]
    public void MatchTest()
    {
        const string prefix = "Name is: ";

        var user = new User(16, "John");
        UserResult result = UserResult.FromUser(user);

        // matching with the context
        string message = result.Match(
            prefix,
            onNameTaken: static p => p,
            onInvalidName: static p => p,
            onUser: static (p, user) => $"{p}{user.Name}"
        );

        Assert.That(message, Is.EqualTo($"{prefix}{user.Name}"));

        // matching without capturing the context
        int id = result.Match(
            static user => user.Id,
            onNameTaken: static () => 0,
            onInvalidName: static () => 0
        );

        Assert.That(id, Is.EqualTo(user.Id));
    }
}