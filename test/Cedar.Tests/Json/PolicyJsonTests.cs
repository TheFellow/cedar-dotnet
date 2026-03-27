using System;
using System.Text.Json;
using Cedar.Core;
using Cedar.Core.Internal.Parser;
using Xunit;

namespace Cedar.Tests.Json;

public sealed class PolicyJsonTests
{
    [Fact]
    public void MarshalJson_WritesSimplePolicyShape()
    {
        string json = Policy.UnmarshalCedar("permit(principal, action, resource);").MarshalJson();

        Assert.Equal(
            "{\"effect\":\"permit\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"}}",
            json);
    }

    [Fact]
    public void UnmarshalJson_ReadsSimplePolicy()
    {
        Policy policy = Policy.UnmarshalJson("{\"effect\":\"forbid\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"}}\n");

        Assert.Equal(Effect.Forbid, policy.Effect);
        Assert.Equal("forbid(principal, action, resource);", policy.MarshalCedar());
    }

    [Fact]
    public void RoundTrip_CedarToJsonToCedar_PreservesSemantics()
    {
        const string cedar = "@env(\"prod\") permit(principal == User::\"alice\", action in [Action::\"read\"], resource) when { principal.getTag(\"team\") == \"infra\" } unless { false };";
        string expected = Policy.UnmarshalCedar(cedar).MarshalCedar();

        Policy roundTripped = Policy.UnmarshalJson(Policy.UnmarshalCedar(cedar).MarshalJson());

        Assert.Equal(expected, roundTripped.MarshalCedar());
    }

    [Theory]
    [InlineData("permit(principal == User::\"alice\", action, resource);", "\"principal\":{\"op\":\"==\",\"entity\":{\"type\":\"User\",\"id\":\"alice\"}}")]
    [InlineData("permit(principal in User::\"alice\", action, resource);", "\"principal\":{\"op\":\"in\",\"entity\":{\"type\":\"User\",\"id\":\"alice\"}}")]
    [InlineData("permit(principal is User, action, resource);", "\"principal\":{\"op\":\"is\",\"entity_type\":\"User\"}")]
    [InlineData("permit(principal is User in Org::\"acme\", action, resource);", "\"principal\":{\"op\":\"is\",\"entity_type\":\"User\",\"in\":{\"type\":\"Org\",\"id\":\"acme\"}}")]
    [InlineData("permit(principal, action in [Action::\"read\", Action::\"write\"], resource);", "\"action\":{\"op\":\"in\",\"entities\":[{\"type\":\"Action\",\"id\":\"read\"},{\"type\":\"Action\",\"id\":\"write\"}]}")]
    [InlineData("permit(principal, action, resource is Doc in Folder::\"f1\");", "\"resource\":{\"op\":\"is\",\"entity_type\":\"Doc\",\"in\":{\"type\":\"Folder\",\"id\":\"f1\"}}")]
    public void MarshalJson_EncodesScopeVariants(string cedar, string expectedFragment)
    {
        string json = Policy.UnmarshalCedar(cedar).MarshalJson();

        Assert.Contains(expectedFragment, json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1 == 2", "==")]
    [InlineData("1 != 2", "!=")]
    [InlineData("1 < 2", "<")]
    [InlineData("1 <= 2", "<=")]
    [InlineData("1 > 2", ">")]
    [InlineData("1 >= 2", ">=")]
    [InlineData("principal && action", "&&")]
    [InlineData("principal || action", "||")]
    [InlineData("1 + 2", "+")]
    [InlineData("1 - 2", "-")]
    [InlineData("1 * 2", "*")]
    [InlineData("principal in resource", "in")]
    [InlineData("-principal", "neg")]
    [InlineData("context.attr", ".")]
    [InlineData("context has attr", "has")]
    [InlineData("context.name like \"ab*\"", "like")]
    [InlineData("if true then 1 else 2", "if-then-else")]
    [InlineData("[1,2]", "Set")]
    [InlineData("{a:1}", "Record")]
    [InlineData("[1].contains(1)", ".contains")]
    [InlineData("[1].containsAll([1])", ".containsAll")]
    [InlineData("[1].containsAny([1])", ".containsAny")]
    [InlineData("[].isEmpty()", ".isEmpty")]
    [InlineData("principal.getTag(\"k\")", ".getTag")]
    [InlineData("principal.hasTag(\"k\")", ".hasTag")]
    [InlineData("resource is Folder", "is")]
    [InlineData("resource is Folder in Org::\"acme\"", "is")]
    [InlineData("myExt(1, true)", "myExt")]
    public void MarshalJson_UsesExpectedNodeDiscriminators(string expression, string expectedDiscriminator)
    {
        string cedar = $"permit(principal, action, resource) when {{ {expression} }};";
        using JsonDocument document = JsonDocument.Parse(Policy.UnmarshalCedar(cedar).MarshalJson());
        JsonElement body = document.RootElement.GetProperty("conditions")[0].GetProperty("body");
        JsonProperty discriminator = Assert.Single(body.EnumerateObject());

        Assert.Equal(expectedDiscriminator, discriminator.Name);
    }

    [Fact]
    public void MarshalJson_EmptySetSerializesAsSetEmptyArray()
    {
        const string cedar = "permit(principal, action, resource) when { [] };";
        string expected = Policy.UnmarshalCedar(cedar).MarshalCedar();

        string json = Policy.UnmarshalCedar(cedar).MarshalJson();
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement body = document.RootElement.GetProperty("conditions")[0].GetProperty("body");

        Assert.True(body.TryGetProperty("Set", out JsonElement setElement));
        Assert.Equal(JsonValueKind.Array, setElement.ValueKind);
        Assert.Equal(0, setElement.GetArrayLength());
        Assert.Single(body.EnumerateObject());

        Policy roundTripped = Policy.UnmarshalJson(json);

        Assert.Equal(expected, roundTripped.MarshalCedar());
    }

    [Fact]
    public void MarshalJson_UsesRecordPairsShape()
    {
        string json = Policy.UnmarshalCedar("permit(principal, action, resource) when { {a: 1, b: 2} };").MarshalJson();

        Assert.Contains("\"Record\":{\"pairs\":[{\"key\":\"a\"", json, StringComparison.Ordinal);
        Assert.Contains("\"key\":\"b\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MarshalJson_EncodesEmptyStringLikePatternAsLiteralComponent()
    {
        string json = Policy.UnmarshalCedar("permit(principal, action, resource) when { context.name like \"\" };").MarshalJson();

        Assert.Contains("\"pattern\":[{\"Literal\":\"\"}]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTrip_EmptyStringLikePattern_PreservesSemantics()
    {
        const string cedar = "permit(principal, action, resource) when { context.name like \"\" };";
        string expected = Policy.UnmarshalCedar(cedar).MarshalCedar();

        string json = Policy.UnmarshalCedar(cedar).MarshalJson();
        Policy roundTripped = Policy.UnmarshalJson(json);

        Assert.Contains("\"pattern\":[{\"Literal\":\"\"}]", json, StringComparison.Ordinal);
        Assert.Equal(expected, roundTripped.MarshalCedar());
    }

    [Fact]
    public void UnmarshalJson_ReadsEmptyStringLikePatternLiteralComponent()
    {
        const string json = "{\"effect\":\"permit\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"},\"conditions\":[{\"kind\":\"when\",\"body\":{\"like\":{\"left\":{\".\":{\"left\":{\"Var\":\"context\"},\"attr\":\"name\"}},\"pattern\":[{\"Literal\":\"\"}]}}}]}";

        Policy policy = Policy.UnmarshalJson(json);

        Assert.Equal("permit(principal, action, resource)\n  when { context.name like \"\" };", policy.MarshalCedar());
    }

    [Fact]
    public void UnmarshalJson_ReadsRecordPairsShape()
    {
        const string json = "{\"effect\":\"permit\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"},\"conditions\":[{\"kind\":\"when\",\"body\":{\"Record\":{\"pairs\":[{\"key\":\"a\",\"value\":{\"Value\":1}},{\"key\":\"b\",\"value\":{\"Value\":2}}]}}}]}";

        Policy policy = Policy.UnmarshalJson(json);

        Assert.Equal("permit(principal, action, resource)\n  when { {a: 1, b: 2} };", policy.MarshalCedar());
    }

    [Fact]
    public void MarshalJson_SortsAnnotationKeys()
    {
        string json = Policy.UnmarshalCedar("@z(\"last\") @a(\"first\") permit(principal, action, resource);").MarshalJson();

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement annotations = document.RootElement.GetProperty("annotations");
        JsonElement.ObjectEnumerator enumerator = annotations.EnumerateObject();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current.Name);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("z", enumerator.Current.Name);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void RoundTrip_PreservesLiteralSentinelKeysInRecord()
    {
        const string cedar = "permit(principal, action, resource) when { {\"__entity\": \"literal\", \"__extn\": \"also-literal\"} };";
        string expected = Policy.UnmarshalCedar(cedar).MarshalCedar();

        string json = Policy.UnmarshalCedar(cedar).MarshalJson();
        Policy roundTripped = Policy.UnmarshalJson(json);

        Assert.Contains("\"key\":\"__entity\"", json, StringComparison.Ordinal);
        Assert.Contains("\"key\":\"__extn\"", json, StringComparison.Ordinal);
        Assert.Equal(expected, roundTripped.MarshalCedar());
    }

    [Fact]
    public void UnmarshalJson_AcceptsDotMethodDiscriminators()
    {
        const string json = "{\"effect\":\"permit\",\"principal\":{\"op\":\"All\"},\"action\":{\"op\":\"All\"},\"resource\":{\"op\":\"All\"},\"conditions\":[{\"kind\":\"when\",\"body\":{\".contains\":{\"left\":{\"Set\":[{\"Value\":1}]},\"arg\":{\"Value\":1}}}}]}";

        Policy policy = Policy.UnmarshalJson(json);

        Assert.Equal("permit(principal, action, resource)\n  when { [1].contains(1) };", policy.MarshalCedar());
    }

    [Fact]
    public void MarshalJson_RoundTripsThroughParserAst()
    {
        const string cedar = "permit(principal, action, resource) when { context has user.name };";

        string json = Policy.UnmarshalCedar(cedar).MarshalJson();
        Policy reparsed = Policy.UnmarshalJson(json);

        Assert.Equal(CedarWriter.Write(Assert.Single(CedarParser.ParsePolicies(cedar))), reparsed.MarshalCedar());
    }

    [Fact]
    public void MarshalJson_EmptyRecord_PreservesRecordDiscriminator()
    {
        string json = Policy.UnmarshalCedar("permit(principal, action, resource) when { {} };").MarshalJson();

        Assert.Contains("\"Record\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pairs\":[]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundTrip_EmptyRecord_PreservesStructure()
    {
        const string cedar = "permit(principal, action, resource) when { {} };";
        string expected = Policy.UnmarshalCedar(cedar).MarshalCedar();

        string json = Policy.UnmarshalCedar(cedar).MarshalJson();
        Policy roundTripped = Policy.UnmarshalJson(json);

        Assert.Equal(expected, roundTripped.MarshalCedar());
    }

    [Fact]
    public void RoundTrip_RecordWithExtensionCallValues_PreservesExtensionCalls()
    {
        const string cedar = "permit(principal, action, resource) when { {d: duration(\"1h\"), dt: datetime(\"2024-01-01T00:00:00Z\")} };";
        string expected = Policy.UnmarshalCedar(cedar).MarshalCedar();

        string json = Policy.UnmarshalCedar(cedar).MarshalJson();
        Policy roundTripped = Policy.UnmarshalJson(json);

        Assert.Contains("\"duration\"", json, StringComparison.Ordinal);
        Assert.Contains("\"datetime\"", json, StringComparison.Ordinal);
        Assert.Equal(expected, roundTripped.MarshalCedar());
    }
}
