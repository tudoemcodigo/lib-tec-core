using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TEC.Core.Enums;
using TEC.Core.Enums.Extensions;

namespace TEC.Core.Tests.Enums;

public class EnumHelperTests
{
    public enum OrderStatus
    {
        [Description("Aguardando pagamento")] Pending = 1,
        [Display(Name = "Pago")] Paid = 2,
        Canceled = 3
    }

    [Test]
    public async Task GetDescription_And_DisplayName()
    {
        await Assert.That(OrderStatus.Pending.GetDescription()).IsEqualTo("Aguardando pagamento");
        await Assert.That(OrderStatus.Paid.GetDescription()).IsEqualTo("Pago");
        await Assert.That(OrderStatus.Canceled.GetDescription()).IsEqualTo("Canceled");
        await Assert.That(OrderStatus.Paid.GetDisplayName()).IsEqualTo("Pago");
        await Assert.That(OrderStatus.Paid.GetCode()).IsEqualTo(2);
    }

    [Test]
    [Arguments("Pending", OrderStatus.Pending)]
    [Arguments("paid", OrderStatus.Paid)]
    [Arguments("3", OrderStatus.Canceled)]
    [Arguments("aguardando pagamento", OrderStatus.Pending)]
    [Arguments("Pago", OrderStatus.Paid)]
    public async Task TryParse_AcceptsNameCodeAndDescription(string text, OrderStatus expected)
    {
        await Assert.That(EnumHelper.TryParse<OrderStatus>(text, out var value)).IsTrue();
        await Assert.That(value).IsEqualTo(expected);
    }

    [Test]
    public async Task TryParse_UnknownValue_ReturnsFalse()
    {
        await Assert.That(EnumHelper.TryParse<OrderStatus>("99", out _)).IsFalse();
        await Assert.That("xyz".ToEnumOrDefault(OrderStatus.Canceled)).IsEqualTo(OrderStatus.Canceled);
        await Assert.That(() => "xyz".ToEnum<OrderStatus>()).ThrowsExactly<ArgumentException>();
    }

    [Test]
    public async Task GetItems_ListsAllMembers()
    {
        var items = EnumHelper.GetItems<OrderStatus>();

        await Assert.That(items.Count).IsEqualTo(3);
        await Assert.That((items[0].Value, items[0].Code, items[0].Name, items[0].Description))
            .IsEqualTo((OrderStatus.Pending, 1L, "Pending", "Aguardando pagamento"));
    }
}
