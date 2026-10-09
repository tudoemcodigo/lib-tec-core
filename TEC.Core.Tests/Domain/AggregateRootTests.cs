using TEC.Core.Domain;

namespace TEC.Core.Tests.Domain;

public class AggregateRootTests
{
    private sealed record PedidoAprovado(Guid PedidoId, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed class Pedido : AggregateRoot<Guid>
    {
        public Pedido(Guid id) => Id = id;

        public void Aprovar(DateTimeOffset agora) => RaiseDomainEvent(new PedidoAprovado(Id, agora));

        public void Registrar(IDomainEvent evento) => RaiseDomainEvent(evento);
    }

    private sealed class Cliente : AggregateRoot<Guid>
    {
        public Cliente(Guid id) => Id = id;
    }

    [Test]
    public async Task Raised_events_are_kept_in_order_until_cleared()
    {
        var pedido = new Pedido(Guid.NewGuid());
        var agora = DateTimeOffset.UnixEpoch;

        pedido.Aprovar(agora);
        pedido.Aprovar(agora.AddSeconds(1));

        await Assert.That(pedido.DomainEvents.Count).IsEqualTo(2);
        await Assert.That(pedido.DomainEvents[1].OccurredAt).IsEqualTo(agora.AddSeconds(1));

        pedido.ClearDomainEvents();
        await Assert.That(pedido.DomainEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Null_event_is_rejected()
    {
        var pedido = new Pedido(Guid.NewGuid());

        await Assert.That(() => pedido.Registrar(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Pending_events_are_limited()
    {
        var pedido = new Pedido(Guid.NewGuid());
        for (var i = 0; i < AggregateRoot<Guid>.MaxPendingEvents; i++)
            pedido.Aprovar(DateTimeOffset.UnixEpoch);

        await Assert.That(() => pedido.Aprovar(DateTimeOffset.UnixEpoch)).ThrowsExactly<InvalidOperationException>();
    }

    [Test]
    public async Task Equality_uses_concrete_type_and_id()
    {
        var id = Guid.NewGuid();

        await Assert.That(new Pedido(id) == new Pedido(id)).IsTrue();
        await Assert.That(new Pedido(id).GetHashCode()).IsEqualTo(new Pedido(id).GetHashCode());
        await Assert.That(new Pedido(id).Equals(new Cliente(id))).IsFalse();
        await Assert.That(new Pedido(id) != new Pedido(Guid.NewGuid())).IsTrue();
    }

    [Test]
    public async Task Transient_entities_are_only_equal_to_themselves()
    {
        var a = new Pedido(Guid.Empty);
        var b = new Pedido(Guid.Empty);

        await Assert.That(a == b).IsFalse();
        await Assert.That(a.Equals(a)).IsTrue();
        await Assert.That(a == null).IsFalse();
    }
}
