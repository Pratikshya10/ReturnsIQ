using ReturnsIQ.Security;
using Xunit;

public class ClassificationValidatorTests
{
    private const string Good =
        """{"category":"DAMAGED_IN_TRANSIT","sentiment":"negative","root_cause":"No padding in box","is_product_defect":false,"confidence":0.93}""";

    [Fact]
    public void Accepts_valid_json()
    {
        var r = ClassificationValidator.Validate(Good);
        Assert.True(r.Ok);
        Assert.Equal("DAMAGED_IN_TRANSIT", r.Value!.Category);
        Assert.Equal(0.93, r.Value.Confidence);
    }

    [Fact]
    public void Accepts_json_inside_code_fences()
        => Assert.True(ClassificationValidator.Validate("```json\n" + Good + "\n```").Ok);

    [Fact]
    public void Rejects_forged_category()
        => Assert.False(ClassificationValidator.Validate(Good.Replace("DAMAGED_IN_TRANSIT", "REFUND_APPROVED")).Ok);

    [Fact]
    public void Rejects_out_of_range_confidence()
        => Assert.False(ClassificationValidator.Validate(Good.Replace("0.93", "1.5")).Ok);

    [Fact]
    public void Rejects_missing_field()
        => Assert.False(ClassificationValidator.Validate("""{"category":"OTHER","sentiment":"neutral"}""").Ok);

    [Fact]
    public void Rejects_prose_around_json()
        => Assert.False(ClassificationValidator.Validate("Sure! Here you go: " + Good).Ok);

    [Fact]
    public void Rejects_empty()
        => Assert.False(ClassificationValidator.Validate("").Ok);
}