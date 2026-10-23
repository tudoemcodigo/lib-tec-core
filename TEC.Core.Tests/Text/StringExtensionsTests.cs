using TEC.Core.Text.Extensions;

namespace TEC.Core.Tests.Text;

public class StringExtensionsTests
{
    [Test]
    public async Task RemoveAccents() =>
        await Assert.That("Ação Coração Pinguim".RemoveAccents()).IsEqualTo("Acao Coracao Pinguim");

    [Test]
    public async Task OnlyDigits() =>
        await Assert.That("(11) 98765-4321".OnlyDigits()).IsEqualTo("11987654321");

    [Test]
    public async Task ToSlug() =>
        await Assert.That("  Promoção de Verão -- 2026! ".ToSlug()).IsEqualTo("promocao-de-verao-2026");

    [Test]
    public async Task ToTitleCase() =>
        await Assert.That("MARIA DA SILVA E SOUZA".ToTitleCase()).IsEqualTo("Maria da Silva e Souza");

    [Test]
    [Arguments("Nome do Cliente", "NomeDoCliente", "nomeDoCliente", "nome_do_cliente", "nome-do-cliente")]
    [Arguments("NomeDoCliente", "NomeDoCliente", "nomeDoCliente", "nome_do_cliente", "nome-do-cliente")]
    [Arguments("código_postal", "CodigoPostal", "codigoPostal", "codigo_postal", "codigo-postal")]
    public async Task CaseConversions(string input, string pascal, string camel, string snake, string kebab)
    {
        await Assert.That(input.ToPascalCase()).IsEqualTo(pascal);
        await Assert.That(input.ToCamelCase()).IsEqualTo(camel);
        await Assert.That(input.ToSnakeCase()).IsEqualTo(snake);
        await Assert.That(input.ToKebabCase()).IsEqualTo(kebab);
    }

    [Test]
    public async Task Truncate()
    {
        await Assert.That("Texto muito longo".Truncate(10)).IsEqualTo("Texto m...");
        await Assert.That("Curto".Truncate(10)).IsEqualTo("Curto");
    }

    [Test]
    public async Task CompareIgnoringAccents()
    {
        await Assert.That("São Paulo".EqualsIgnoreCaseAndAccents("sao paulo")).IsTrue();
        await Assert.That("Avenida São João".ContainsIgnoreCaseAndAccents("SAO JOAO")).IsTrue();
    }

    [Test]
    public async Task Misc()
    {
        await Assert.That("  a   b\t\nc ".CollapseWhitespace()).IsEqualTo("a b c");
        await Assert.That("   ".NullIfWhiteSpace()).IsNull();
        await Assert.That("abcdef".Left(3)).IsEqualTo("abc");
        await Assert.That("abcdef".Right(3)).IsEqualTo("def");
        await Assert.That("olá".ToBase64().FromBase64()).IsEqualTo("olá");
    }
}
