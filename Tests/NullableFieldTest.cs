namespace REnum.Tests;

// Test enum with nullable fields
[REnum]
[REnumField(typeof(int?))] // Nullable value type
[REnumField(typeof(UserData?))] // Nullable struct
[REnumField(typeof(string), nullable: true)] // Nullable reference type - use nullable parameter
[REnumFieldEmpty("Empty")]
public partial struct NullableResult
{
}


public readonly struct UserData
{
    public readonly int Id;
    public readonly string Name;

    public UserData(int id, string name)
    {
        Id = id;
        Name = name;
    }
}


public class NullableFieldTest
{
    [Test]
    public void NullableValueType_WithValue()
    {
        var result = NullableResult.FromInt32(42);

        Assert.That(result.IsInt32(out var value), Is.True);
        Assert.That(value, Is.EqualTo(42));
        Assert.That(result.GetKind(), Is.EqualTo(NullableResult.Kind.Int32));
    }

    [Test]
    public void NullableValueType_WithNull()
    {
        var result = NullableResult.FromInt32(null);

        Assert.That(result.IsInt32(out var value), Is.True);
        Assert.That(value, Is.Null);
        Assert.That(result.GetKind(), Is.EqualTo(NullableResult.Kind.Int32));
    }

    [Test]
    public void NullableStruct_WithValue()
    {
        var userData = new UserData(1, "Alice");
        var result = NullableResult.FromUserData(userData);

        Assert.That(result.IsUserData(out var value), Is.True);
        Assert.That(value, Is.Not.Null);
        Assert.That(value!.Value.Id, Is.EqualTo(1));
        Assert.That(value!.Value.Name, Is.EqualTo("Alice"));
    }

    [Test]
    public void NullableStruct_WithNull()
    {
        var result = NullableResult.FromUserData(null);

        Assert.That(result.IsUserData(out var value), Is.True);
        Assert.That(value, Is.Null);
    }

    [Test]
    public void NullableReferenceType_WithValue()
    {
        var result = NullableResult.FromString("Hello");

        Assert.That(result.IsString(out var value), Is.True);
        Assert.That(value, Is.EqualTo("Hello"));
    }

    [Test]
    public void NullableReferenceType_WithNull()
    {
        var result = NullableResult.FromString(null);

        Assert.That(result.IsString(out var value), Is.True);
        Assert.That(value, Is.Null);
    }

    [Test]
    public void EmptyVariant_DoesNotMatchOthers()
    {
        var result = NullableResult.Empty();

        Assert.That(result.IsEmpty(), Is.True);
        Assert.That(result.IsInt32(out _), Is.False);
        Assert.That(result.IsUserData(out _), Is.False);
        Assert.That(result.IsString(out _), Is.False);
    }

    [Test]
    public void Match_WithNullableTypes()
    {
        var intResult = NullableResult.FromInt32(100);
        var nullIntResult = NullableResult.FromInt32(null);
        var stringResult = NullableResult.FromString("test");
        var nullStringResult = NullableResult.FromString(null);

        // Match with value
        string intMessage = intResult.Match(
            onInt32: i => i.HasValue ? $"Int: {i.Value}" : "Int: null",
            onUserData: u => u.HasValue ? $"User: {u.Value.Name}" : "User: null",
            onString: s => s != null ? $"String: {s}" : "String: null",
            onEmpty: () => "Empty"
        );
        Assert.That(intMessage, Is.EqualTo("Int: 100"));

        // Match with null int
        string nullIntMessage = nullIntResult.Match(
            onInt32: i => i.HasValue ? $"Int: {i.Value}" : "Int: null",
            onUserData: u => u.HasValue ? $"User: {u.Value.Name}" : "User: null",
            onString: s => s != null ? $"String: {s}" : "String: null",
            onEmpty: () => "Empty"
        );
        Assert.That(nullIntMessage, Is.EqualTo("Int: null"));

        // Match with string value
        string stringMessage = stringResult.Match(
            onInt32: i => i.HasValue ? $"Int: {i.Value}" : "Int: null",
            onUserData: u => u.HasValue ? $"User: {u.Value.Name}" : "User: null",
            onString: s => s != null ? $"String: {s}" : "String: null",
            onEmpty: () => "Empty"
        );
        Assert.That(stringMessage, Is.EqualTo("String: test"));

        // Match with null string
        string nullStringMessage = nullStringResult.Match(
            onInt32: i => i.HasValue ? $"Int: {i.Value}" : "Int: null",
            onUserData: u => u.HasValue ? $"User: {u.Value.Name}" : "User: null",
            onString: s => s != null ? $"String: {s}" : "String: null",
            onEmpty: () => "Empty"
        );
        Assert.That(nullStringMessage, Is.EqualTo("String: null"));
    }

    [Test]
    public void Equality_WithNullableTypes()
    {
        var result1 = NullableResult.FromInt32(42);
        var result2 = NullableResult.FromInt32(42);
        var result3 = NullableResult.FromInt32(99);
        var result4 = NullableResult.FromInt32(null);
        var result5 = NullableResult.FromInt32(null);

        // Same values should be equal
        Assert.That(result1, Is.EqualTo(result2));

        // Different values should not be equal
        Assert.That(result1, Is.Not.EqualTo(result3));

        // Null values of same type should be equal
        Assert.That(result4, Is.EqualTo(result5));

        // Value vs null should not be equal
        Assert.That(result1, Is.Not.EqualTo(result4));
    }

    [Test]
    public void ToString_WithNullableTypes()
    {
        var intResult = NullableResult.FromInt32(42);
        var nullIntResult = NullableResult.FromInt32(null);
        var stringResult = NullableResult.FromString("hello");
        var nullStringResult = NullableResult.FromString(null);

        // Non-null values
        Assert.That(intResult.ToString(), Is.EqualTo("42"));
        Assert.That(stringResult.ToString(), Is.EqualTo("hello"));

        // Null values should return "null"
        Assert.That(nullIntResult.ToString(), Is.EqualTo("null"));
        Assert.That(nullStringResult.ToString(), Is.EqualTo("null"));
    }

    [Test]
    public void MaybeNullWhen_BehaviorIsCorrect()
    {
        var result = NullableResult.FromInt32(42);
        var emptyResult = NullableResult.Empty();

        // When IsXXX returns true, the out parameter is valid
        if (result.IsInt32(out var value))
        {
            // Compiler knows 'value' can be used here
            var _ = value.HasValue; // This compiles without warning
        }

        // When IsXXX returns false, the out parameter may be default
        if (!emptyResult.IsInt32(out var emptyValue))
        {
            // emptyValue is default(int?) which is null
            Assert.That(emptyValue, Is.Null);
        }
    }
}
