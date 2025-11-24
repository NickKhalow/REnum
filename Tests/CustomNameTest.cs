namespace REnum.Tests;

[REnum]
[REnumField(typeof(PaymentCard), "Card")]
[REnumField(typeof(PaymentCash))] // No custom name, should use "PaymentCash"
[REnumField(typeof(PaymentCrypto), "Crypto")]
public partial struct Payment
{
}


public readonly struct PaymentCard
{
    public readonly string CardNumber;
    public readonly string CardHolder;

    public PaymentCard(string cardNumber, string cardHolder)
    {
        CardNumber = cardNumber;
        CardHolder = cardHolder;
    }
}


public readonly struct PaymentCash
{
    public readonly decimal Amount;

    public PaymentCash(decimal amount)
    {
        Amount = amount;
    }
}


public readonly struct PaymentCrypto
{
    public readonly string WalletAddress;
    public readonly string CryptoType;

    public PaymentCrypto(string walletAddress, string cryptoType)
    {
        WalletAddress = walletAddress;
        CryptoType = cryptoType;
    }
}


public class CustomNameTest
{
    [Test]
    public void CustomName_EnumMembersExist()
    {
        // Verify that the enum members have the correct names
        var cardKind = Payment.Kind.Card; // Custom name
        var cashKind = Payment.Kind.PaymentCash; // Default name (type name)
        var cryptoKind = Payment.Kind.Crypto; // Custom name

        Assert.That(cardKind.ToString(), Is.EqualTo("Card"));
        Assert.That(cashKind.ToString(), Is.EqualTo("PaymentCash"));
        Assert.That(cryptoKind.ToString(), Is.EqualTo("Crypto"));
    }

    [Test]
    public void CustomName_FactoryMethodsWork()
    {
        var card = new PaymentCard("1234-5678-9012-3456", "John Doe");
        var payment = Payment.FromCard(card);

        Assert.That(payment.IsCard(out var receivedCard), Is.True);
        Assert.That(receivedCard, Is.EqualTo(card));

        var cash = new PaymentCash(100.50m);
        var cashPayment = Payment.FromPaymentCash(cash);

        Assert.That(cashPayment.IsPaymentCash(out var receivedCash), Is.True);
        Assert.That(receivedCash.Amount, Is.EqualTo(100.50m));

        var crypto = new PaymentCrypto("0x1234...", "Bitcoin");
        var cryptoPayment = Payment.FromCrypto(crypto);

        Assert.That(cryptoPayment.IsCrypto(out var receivedCrypto), Is.True);
        Assert.That(receivedCrypto.CryptoType, Is.EqualTo("Bitcoin"));
    }

    [Test]
    public void CustomName_MatcherMethodsWork()
    {
        var card = new PaymentCard("1234-5678-9012-3456", "John Doe");
        var payment = Payment.FromCard(card);

        // Test Match with return value
        string paymentType = payment.Match(
            onCard: c => $"Card: {c.CardHolder}",
            onPaymentCash: c => $"Cash: {c.Amount}",
            onCrypto: c => $"Crypto: {c.CryptoType}"
        );

        Assert.That(paymentType, Is.EqualTo("Card: John Doe"));

        // Test with cash
        var cashPayment = Payment.FromPaymentCash(new PaymentCash(50.25m));
        decimal amount = cashPayment.Match(
            onCard: c => 0m,
            onPaymentCash: c => c.Amount,
            onCrypto: c => 0m
        );

        Assert.That(amount, Is.EqualTo(50.25m));
    }

    [Test]
    public void CustomName_ComparisonAndEquality()
    {
        var card1 = new PaymentCard("1234", "John");
        var card2 = new PaymentCard("1234", "John");
        var card3 = new PaymentCard("5678", "Jane");

        var payment1 = Payment.FromCard(card1);
        var payment2 = Payment.FromCard(card2);
        var payment3 = Payment.FromCard(card3);

        // Same card data should be equal
        Assert.That(payment1, Is.EqualTo(payment2));

        // Different card data should not be equal
        Assert.That(payment1, Is.Not.EqualTo(payment3));

        // Different payment types should not be equal
        var cashPayment = Payment.FromPaymentCash(new PaymentCash(100));
        Assert.That(payment1, Is.Not.EqualTo(cashPayment));
    }

    [Test]
    public void CustomName_ToStringWorks()
    {
        var card = new PaymentCard("1234", "John");
        var payment = Payment.FromCard(card);

        var toString = payment.ToString();
        Assert.That(toString, Does.Contain("PaymentCard")); // Should contain the type info
    }
}
