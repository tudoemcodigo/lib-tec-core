using TEC.Core.Domain;

namespace TEC.Core.Tests.Domain;

public class AuditableBaseTests
{
    private sealed class Pedido : AuditableAggregateRoot<Guid>
    {
        public Pedido(Guid id) => Id = id;
    }

    private sealed class Item : AuditableDomainEntity<Guid>
    {
        public Item(Guid id) => Id = id;
    }

    [Test]
    public async Task Auditable_aggregate_root_exposes_persistence_contracts_with_defaults()
    {
        var id = Guid.NewGuid();
        var pedido = new Pedido(id);

        await Assert.That(pedido).IsAssignableTo<IEntity<Guid>>();
        await Assert.That(pedido).IsAssignableTo<ISoftDelete>();
        await Assert.That(pedido).IsAssignableTo<IAuditable>();
        await Assert.That(pedido).IsAssignableTo<IHasDomainEvents>();
        await Assert.That(((IEntity<Guid>)pedido).Id).IsEqualTo(id);
        await Assert.That(pedido.IsDeleted).IsFalse();
        await Assert.That(pedido.DeletedAt).IsNull();
        await Assert.That(pedido.CreatedBy).IsNull();
        await Assert.That(pedido.UpdatedAt).IsNull();
    }

    [Test]
    public async Task Auditable_domain_entity_keeps_identity_equality()
    {
        var id = Guid.NewGuid();

        await Assert.That(new Item(id)).IsEqualTo(new Item(id));
        await Assert.That(new Item(id)).IsNotEqualTo(new Item(Guid.NewGuid()));
        await Assert.That(new Item(Guid.Empty)).IsNotEqualTo(new Item(Guid.Empty));
        await Assert.That(new Item(id)).IsAssignableTo<ISoftDelete>();
        await Assert.That(new Item(id)).IsAssignableTo<IAuditable>();
    }

    [Test]
    public async Task Persistence_fields_are_not_writable_by_the_domain()
    {
        var tipo = typeof(AuditableAggregateRoot<Guid>);
        string[] campos =
        [
            nameof(ISoftDelete.IsDeleted), nameof(ISoftDelete.DeletedAt), nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy),
            nameof(IAuditable.CreatedByTenant), nameof(IAuditable.UpdatedAt), nameof(IAuditable.UpdatedBy),
            nameof(IAuditable.UpdatedByTenant), nameof(IAuditable.DeletedBy), nameof(IAuditable.DeletedByTenant),
        ];

        foreach (var campo in campos)
        {
            var setter = tipo.GetProperty(campo)!.SetMethod;
            await Assert.That(setter is { IsPrivate: true }).IsTrue().Because($"{campo} deve ter setter privado");
        }
    }
}
