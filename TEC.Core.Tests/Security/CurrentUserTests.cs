using TEC.Core.Security;

namespace TEC.Core.Tests.Security;

public class CurrentUserTests
{
    private sealed class FixedUser(PrincipalKind kind, string? id, string? tenantId = null) : ICurrentUser
    {
        public PrincipalKind Kind => kind;
        public string? Id => id;
        public string? TenantId => tenantId;
    }

    // Implementação que tenta declarar o próprio IsAuthenticated: não substitui o membro da interface
    private sealed class ContradictoryUser : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public PrincipalKind Kind => PrincipalKind.Anonymous;
        public string? Id => null;
        public string? TenantId => null;
    }

    [Test]
    [Arguments(PrincipalKind.Anonymous, false)]
    [Arguments(PrincipalKind.User, true)]
    [Arguments(PrincipalKind.Application, true)]
    [Arguments(PrincipalKind.System, true)]
    public async Task IsAuthenticated_IsDerivedFromKind(PrincipalKind kind, bool expected)
    {
        ICurrentUser user = new FixedUser(kind, kind == PrincipalKind.Anonymous ? null : "id");

        await Assert.That(user.IsAuthenticated).IsEqualTo(expected);
    }

    [Test]
    public async Task IsAuthenticated_CannotContradictKind()
    {
        ICurrentUser user = new ContradictoryUser();

        await Assert.That(user.IsAuthenticated).IsFalse();
    }
}
