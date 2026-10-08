using TEC.Core.Text.Formatting;
using TEC.Core.Text.Generation;
using TEC.Core.Text.Masking;
using TEC.Core.Text.Validation;

namespace TEC.Core.Tests.Text;

public class DocumentTests
{
    [Test]
    [Arguments("529.982.247-25", true)]
    [Arguments("52998224725", true)]
    [Arguments("529.982.247-24", false)]
    [Arguments("111.111.111-11", false)]
    [Arguments("1234", false)]
    [Arguments("abc52998224725", false)]
    [Arguments(null, false)]
    public async Task IsValidCpf(string? cpf, bool expected) =>
        await Assert.That(DocumentValidator.IsValidCpf(cpf)).IsEqualTo(expected);

    [Test]
    [Arguments("11.222.333/0001-81", true)]
    [Arguments("11222333000181", true)]
    [Arguments("11.222.333/0001-80", false)]
    [Arguments("12.ABC.345/01DE-35", true)] // CNPJ alfanumérico (exemplo oficial da Receita Federal)
    [Arguments("12abc34501de35", true)]
    [Arguments("12.ABC.345/01DE-36", false)]
    [Arguments("00.000.000/0000-00", false)]
    public async Task IsValidCnpj(string cnpj, bool expected) =>
        await Assert.That(DocumentValidator.IsValidCnpj(cnpj)).IsEqualTo(expected);

    [Test]
    [Arguments("12056708045", true)]
    [Arguments("12056708046", false)]
    public async Task IsValidPis(string pis, bool expected) =>
        await Assert.That(DocumentValidator.IsValidPis(pis)).IsEqualTo(expected);

    [Test]
    [Arguments("(11) 98765-4321", true)]
    [Arguments("+55 11 98765-4321", true)]
    [Arguments("(11) 3333-4444", true)]
    [Arguments("(11) 88765-4321", false)]
    [Arguments("(01) 3333-4444", false)]
    [Arguments("12345", false)]
    public async Task IsValidPhone(string phone, bool expected) =>
        await Assert.That(DocumentValidator.IsValidPhone(phone)).IsEqualTo(expected);

    [Test]
    [Arguments("usuario@empresa.com.br", true)]
    [Arguments("nome.sobrenome+tag@dominio.io", true)]
    [Arguments("invalido@", false)]
    [Arguments("sem-arroba.com", false)]
    public async Task IsValidEmail(string email, bool expected) =>
        await Assert.That(DocumentValidator.IsValidEmail(email)).IsEqualTo(expected);

    // Validador e formatador usam os mesmos conjuntos de caracteres: o que a validação recusa não é formatado
    [Test]
    [Arguments("529.982.247-25\n")]
    [Arguments("529.982.247-25\t")]
    [Arguments("529_982_247_25")]
    [Arguments("529\u00A0982\u00A0247\u00A025")]
    [Arguments("abc52998224725")]
    public async Task ValidatorAndFormatter_AgreeOnMaskCharacters(string cpf)
    {
        await Assert.That(DocumentValidator.IsValidCpf(cpf)).IsFalse();
        await Assert.That(DocumentFormatter.FormatCpf(cpf)).IsEqualTo(cpf);
    }

    [Test]
    public async Task ValidatorAndFormatter_AgreeOnPhoneAndCnpjMaskCharacters()
    {
        await Assert.That(DocumentValidator.IsValidPhone("(11) 98765-4321\n")).IsFalse();
        await Assert.That(DocumentFormatter.FormatPhone("(11) 98765-4321\n")).IsEqualTo("(11) 98765-4321\n");
        await Assert.That(DocumentValidator.IsValidCnpj("12.ABC.345/01DE-35_")).IsFalse();
        await Assert.That(DocumentFormatter.FormatCnpj("12.ABC.345/01DE-35_")).IsEqualTo("12.ABC.345/01DE-35_");
        await Assert.That(DocumentValidator.IsValidPhone("+55 (11) 98765-4321")).IsTrue();
    }

    [Test]
    public async Task Formatters()
    {
        await Assert.That(DocumentFormatter.FormatCpf("52998224725")).IsEqualTo("529.982.247-25");
        await Assert.That(DocumentFormatter.FormatCnpj("11222333000181")).IsEqualTo("11.222.333/0001-81");
        await Assert.That(DocumentFormatter.FormatCnpj("12abc34501de35")).IsEqualTo("12.ABC.345/01DE-35");
        await Assert.That(DocumentFormatter.FormatCep("01310100")).IsEqualTo("01310-100");
        await Assert.That(DocumentFormatter.FormatPhone("11987654321")).IsEqualTo("(11) 98765-4321");
        await Assert.That(DocumentFormatter.FormatPhone("1133334444")).IsEqualTo("(11) 3333-4444");
        await Assert.That(DocumentFormatter.FormatPhone("5511987654321")).IsEqualTo("+55 (11) 98765-4321");
        await Assert.That(DocumentFormatter.FormatCpf("123")).IsEqualTo("123");
        await Assert.That(MaskFormatter.Apply("AB12", "##-##")).IsEqualTo("AB-12");
    }

    [Test]
    public async Task Generators_ProduceValidDocuments()
    {
        for (int i = 0; i < 200; i++)
        {
            await Assert.That(DocumentValidator.IsValidCpf(DocumentGenerator.GenerateCpf(formatted: i % 2 == 0))).IsTrue();
            await Assert.That(DocumentValidator.IsValidCnpj(DocumentGenerator.GenerateCnpj(formatted: i % 2 == 0))).IsTrue();
            await Assert.That(DocumentValidator.IsValidCnpj(DocumentGenerator.GenerateCnpj(alphanumeric: true))).IsTrue();
            await Assert.That(DocumentValidator.IsValidPis(DocumentGenerator.GeneratePis())).IsTrue();
        }
    }

    [Test]
    public async Task Masking()
    {
        await Assert.That(SensitiveDataMasker.MaskCpf("529.982.247-25")).IsEqualTo("***.982.247-**");
        await Assert.That(SensitiveDataMasker.MaskCnpj("11222333000181")).IsEqualTo("**.222.333/****-**");
        await Assert.That(SensitiveDataMasker.MaskEmail("joao.silva1@empresa.com")).IsEqualTo("jo*********@empresa.com");
        await Assert.That(SensitiveDataMasker.MaskPhone("11987654321")).IsEqualTo("(11) *****-4321");
        await Assert.That(SensitiveDataMasker.MaskCreditCard("4111 1111 1111 1234")).IsEqualTo("**** **** **** 1234");
        await Assert.That(SensitiveDataMasker.Mask("123456789", 2, 2)).IsEqualTo("12*****89");
        await Assert.That(SensitiveDataMasker.Mask("abc", 2, 2)).IsEqualTo("***");
    }

    // Regressão: a fronteira visível podia cortar um par surrogate (emoji), gerando UTF-16 inválido
    [Test]
    public async Task Mask_DoesNotSplitSurrogatePairs()
    {
        const string value = "a😀bcd😀e"; // "😀" ocupa 2 chars (par surrogate)

        var start = SensitiveDataMasker.Mask(value, visibleStart: 2);   // corta no meio do primeiro emoji
        var end = SensitiveDataMasker.Mask(value, visibleEnd: 2);       // corta no meio do último emoji

        await Assert.That(start).IsEqualTo("a********");
        await Assert.That(end).IsEqualTo("********e");
        foreach (var masked in new[] { start, end })
            await Assert.That(masked.Any(char.IsSurrogate)).IsFalse();

        // Fronteira fora do par: mantém o emoji inteiro visível
        await Assert.That(SensitiveDataMasker.Mask(value, visibleStart: 3)).IsEqualTo("a😀******");
    }
}
