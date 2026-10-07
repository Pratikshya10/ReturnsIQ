using ReturnsIQ.Security;
using Xunit;

public class PiiRedactorTests
{
    [Theory]
    [InlineData("Email me: rahul.v@outlook.com", "Email me: [EMAIL]")]
    [InlineData("Call me at +91 98765 43210.", "Call me at [PHONE].")]
    [InlineData("My number is 415-555-0134.", "My number is [PHONE].")]
    [InlineData("Call 555-867-5309 please.", "Call [PHONE] please.")]
    [InlineData("My card ending 4242 was charged twice too, card 4111 1111 1111 1111.",
                "My card ending 4242 was charged twice too, card [CARD].")]
    [InlineData("typo card 4111 1111 1111 1112 here", "typo card [CARD] here")]
    public void Redacts_pii(string input, string expected)
        => Assert.Equal(expected, PiiRedactor.Redact(input).Text);

    [Theory]
    [InlineData("Size 9 fits like an 8, toes are cramped.")]
    [InlineData("Arrived 2 weeks late, no longer needed it.")]
    [InlineData("Description said 12 hour battery, I only get 4.")]
    [InlineData("Order 1001 placed 2026-09-22 was late")]
    public void Leaves_normal_numbers_alone(string input)
    {
        var r = PiiRedactor.Redact(input);
        Assert.Equal(input, r.Text);
        Assert.Empty(r.Counts);
    }

    [Fact]
    public void Counts_each_type()
    {
        var r = PiiRedactor.Redact("Call me at +91 98765 43210, email a.b@x.co, card 4111 1111 1111 1111");
        Assert.Equal("Call me at [PHONE], email [EMAIL], card [CARD]", r.Text);
        Assert.Equal(1, r.Counts["PHONE"]);
        Assert.Equal(1, r.Counts["EMAIL"]);
        Assert.Equal(1, r.Counts["CARD"]);
    }

    [Fact]
    public void Handles_null_and_empty()
    {
        Assert.Equal("", PiiRedactor.Redact(null).Text);
        Assert.Equal("", PiiRedactor.Redact("").Text);
    }
}