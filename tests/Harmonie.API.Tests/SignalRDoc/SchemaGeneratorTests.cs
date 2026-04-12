using FluentAssertions;
using Harmonie.API.SignalRDoc.Generator;
using Harmonie.API.SignalRDoc.Models;
using Xunit;

namespace Harmonie.API.Tests.SignalRDoc;

public enum FakeColor { Red, Green, Blue }

public sealed class FakePayload
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public Guid Id { get; set; }
}

public sealed class FakeRecursive
{
    public string Label { get; set; } = "";
    public FakeRecursive? Child { get; set; }
}

public sealed class SchemaGeneratorTests
{
    private readonly SchemaGenerator _generator = new();

    private AsyncApiSchema? Get(Type type) => _generator.GetSchema(type, new Dictionary<string, AsyncApiSchema>());

    [Fact]
    public void GetSchema_String_ReturnsStringType()
    {
        var schema = Get(typeof(string));
        schema.Should().NotBeNull();
        schema!.Type.Should().Be("string");
    }

    [Fact]
    public void GetSchema_Bool_ReturnsBooleanType()
    {
        var schema = Get(typeof(bool));
        schema!.Type.Should().Be("boolean");
    }

    [Fact]
    public void GetSchema_Int_ReturnsIntegerType()
    {
        var schema = Get(typeof(int));
        schema!.Type.Should().Be("integer");
    }

    [Fact]
    public void GetSchema_Long_ReturnsIntegerType()
    {
        var schema = Get(typeof(long));
        schema!.Type.Should().Be("integer");
    }

    [Fact]
    public void GetSchema_Double_ReturnsNumberType()
    {
        var schema = Get(typeof(double));
        schema!.Type.Should().Be("number");
    }

    [Fact]
    public void GetSchema_Decimal_ReturnsNumberType()
    {
        var schema = Get(typeof(decimal));
        schema!.Type.Should().Be("number");
    }

    [Fact]
    public void GetSchema_Guid_ReturnsStringWithUuidFormat()
    {
        var schema = Get(typeof(Guid));
        schema!.Type.Should().Be("string");
        schema.Format.Should().Be("uuid");
    }

    [Fact]
    public void GetSchema_DateTime_ReturnsStringWithDateTimeFormat()
    {
        var schema = Get(typeof(DateTime));
        schema!.Type.Should().Be("string");
        schema.Format.Should().Be("date-time");
    }

    [Fact]
    public void GetSchema_DateTimeOffset_ReturnsStringWithDateTimeFormat()
    {
        var schema = Get(typeof(DateTimeOffset));
        schema!.Type.Should().Be("string");
        schema.Format.Should().Be("date-time");
    }

    [Fact]
    public void GetSchema_Enum_ReturnsStringWithEnumValues()
    {
        var schema = Get(typeof(FakeColor));
        schema!.Type.Should().Be("string");
        schema.Enum.Should().BeEquivalentTo(new[] { "Red", "Green", "Blue" });
    }

    [Fact]
    public void GetSchema_Array_ReturnsArrayTypeWithItems()
    {
        var schema = Get(typeof(string[]));
        schema!.Type.Should().Be("array");
        schema.Items.Should().NotBeNull();
        schema.Items!.Type.Should().Be("string");
    }

    [Fact]
    public void GetSchema_ListOfGuid_ReturnsArrayTypeWithUuidItems()
    {
        var schema = Get(typeof(List<Guid>));
        schema!.Type.Should().Be("array");
        schema.Items!.Type.Should().Be("string");
        schema.Items.Format.Should().Be("uuid");
    }

    [Fact]
    public void GetSchema_NullableInt_ReturnsIntegerWithNullable()
    {
        var schema = Get(typeof(int?));
        schema!.Type.Should().Be("integer");
        schema.Nullable.Should().BeTrue();
    }

    [Fact]
    public void GetSchema_NullableGuid_ReturnsUuidWithNullable()
    {
        var schema = Get(typeof(Guid?));
        schema!.Type.Should().Be("string");
        schema.Format.Should().Be("uuid");
        schema.Nullable.Should().BeTrue();
    }

    [Fact]
    public void GetSchema_Void_ReturnsNull()
    {
        var schema = Get(typeof(void));
        schema.Should().BeNull();
    }

    [Fact]
    public void GetSchema_Task_ReturnsNull()
    {
        var schema = Get(typeof(Task));
        schema.Should().BeNull();
    }

    [Fact]
    public void GetSchema_TaskOfString_UnwrapsToString()
    {
        var schema = Get(typeof(Task<string>));
        schema!.Type.Should().Be("string");
    }

    [Fact]
    public void GetSchema_ValueTaskOfInt_UnwrapsToInteger()
    {
        var schema = Get(typeof(ValueTask<int>));
        schema!.Type.Should().Be("integer");
    }

    [Fact]
    public void GetSchema_ComplexObject_ReturnsRefAndRegistersSchema()
    {
        var schemas = new Dictionary<string, AsyncApiSchema>();
        var schema = _generator.GetSchema(typeof(FakePayload), schemas);

        schema!.Ref.Should().Be("#/components/schemas/FakePayload");
        schemas.Should().ContainKey("FakePayload");
        schemas["FakePayload"].Type.Should().Be("object");
        schemas["FakePayload"].Properties.Should().ContainKey("name");
        schemas["FakePayload"].Properties.Should().ContainKey("count");
        schemas["FakePayload"].Properties.Should().ContainKey("id");
    }

    [Fact]
    public void GetSchema_ComplexObjectCalledTwice_ReturnsSameRef()
    {
        var schemas = new Dictionary<string, AsyncApiSchema>();

        var schema1 = _generator.GetSchema(typeof(FakePayload), schemas);
        var schema2 = _generator.GetSchema(typeof(FakePayload), schemas);

        schema1!.Ref.Should().Be(schema2!.Ref);
        schemas.Should().ContainKey("FakePayload");
    }

    [Fact]
    public void GetSchema_CircularReference_DoesNotStackOverflow()
    {
        var schemas = new Dictionary<string, AsyncApiSchema>();
        var schema = _generator.GetSchema(typeof(FakeRecursive), schemas);

        schema!.Ref.Should().Be("#/components/schemas/FakeRecursive");
        schemas.Should().ContainKey("FakeRecursive");

        // The child property references itself without infinite recursion.
        // Note: reference-type nullability (FakeRecursive?) is erased at runtime,
        // so nullable is not set on reference-type properties.
        var childSchema = schemas["FakeRecursive"].Properties?.GetValueOrDefault("child");
        childSchema.Should().NotBeNull();
        childSchema!.Ref.Should().Be("#/components/schemas/FakeRecursive");
    }
}
