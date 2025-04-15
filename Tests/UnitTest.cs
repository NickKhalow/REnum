namespace REnum.Tests;

[REnum]
[REnumField(typeof(HouseAddress))]
[REnumField(typeof(ApartmentAddress))]
public partial struct Address
{
}


/// <summary>
/// Use a class to show REnum works with both classes and structs
/// </summary>
public class HouseAddress
{
    public string Street;
    public string Building;
}


public readonly struct ApartmentAddress
{
    public readonly string Street;
    public readonly string Building;
    public readonly string Apt;

    public ApartmentAddress(string street, string building, string apt)
    {
        Street = street;
        Building = building;
        Apt = apt;
    }
}


public class REnumUnionTest
{
    [Test]
    public void CreateAndCompareTest()
    {
        var houseAddress = new HouseAddress
        {
            Street = "Rockefeller Street",
            Building = "1273"
        };
        var address = Address.FromHouseAddress(houseAddress);

        Assert.That(address.IsApartmentAddress(out _), Is.False);
        Assert.That(address.IsHouseAddress(out var receivedAddress), Is.True);
        Assert.That(houseAddress, Is.EqualTo(receivedAddress));

        var sherlockApartment = new ApartmentAddress(
            street: "Baker Street",
            building: "221B",
            apt: "Detective's Den"
        );
        var secondAddress = Address.FromApartmentAddress(sherlockApartment);
        Assert.That(address, Is.Not.EqualTo(secondAddress));
    }

    [Test]
    public void MatchTest()
    {
        string prefix = "Street is: ";

        var houseAddress = new HouseAddress
        {
            Street = "Rockefeller Street",
            Building = "1273"
        };
        var address = Address.FromHouseAddress(houseAddress);

        string street = address.Match(
            prefix,
            static (p, house) => $"{p}{house.Street}",
            static (p, apartment) => $"{p}{apartment.Street}"
        );
        
        Assert.That(street, Is.EqualTo($"{prefix}{houseAddress.Street}"));
    }
}