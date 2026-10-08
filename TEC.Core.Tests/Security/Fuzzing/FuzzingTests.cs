using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Fluent;
using TEC.Core.Common.Serialization;
using TEC.Core.Cryptography.Asymmetric;
using TEC.Core.Cryptography.Hashing;
using TEC.Core.Cryptography.Hybrid;
using TEC.Core.Cryptography.Symmetric;
using TEC.Core.Csv;
using TEC.Core.Dates.BusinessDays;
using TEC.Core.Dates.Holidays;
using TEC.Core.Dates.Holidays.Sources;
using TEC.Core.Numbers.Extensions;
using TEC.Core.Numbers.Words;
using TEC.Core.Responses;
using TEC.Core.Text.Extensions;
using TEC.Core.Text.Formatting;
using TEC.Core.Text.Generation;
using TEC.Core.Text.Masking;
using TEC.Core.Text.Validation;

namespace TEC.Core.Tests.Security.Fuzzing;

/// <summary>
/// Testes de propriedade (FsCheck) com entradas hostis geradas aleatoriamente. Cada propriedade roda centenas de casos;
/// em caso de falha, a mensagem traz o contraexemplo e a semente para reproduzir.
/// </summary>
public partial class FuzzingTests
{
    // ---------- CSV ----------

    public sealed class FuzzRow
    {
        public string Text { get; set; } = string.Empty;
        public string? Optional { get; set; }
        public char Symbol { get; set; }
        public int Number { get; set; }
    }

    public sealed class TypedRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public decimal Value { get; set; }
        public DayOfWeek Day { get; set; }
        public bool Active { get; set; }
    }

    private static readonly Gen<CsvOptions> CsvOptionsGen =
        from delimiter in Gen.Elements(';', ',', '\t', '|')
        from sanitize in Gen.Elements(true, false)
        from quoteAll in Gen.Elements(true, false)
        select new CsvOptions { Delimiter = delimiter, SanitizeFormulas = sanitize, QuoteAllFields = quoteAll };

    [Test]
    public void Csv_ArbitraryText_RoundTripsExactly()
    {
        var rowGen =
            from text in Hostile.Text
            from optional in Hostile.Text
            from symbol in Hostile.Char
            from number in Gen.Choose(int.MinValue, int.MaxValue)
            select new FuzzRow { Text = text, Optional = optional, Symbol = symbol, Number = number };
        var caseGen = Gen.Zip(CsvOptionsGen, rowGen.NonEmptyListOf());

        Prop.ForAll(caseGen.Select(c => (Options: c.Item1, Rows: c.Item2.ToList())).ToArbitrary(), c =>
        {
            using var stream = new MemoryStream();
            new CsvWriter(c.Options).WriteAsync(stream, c.Rows).GetAwaiter().GetResult();
            stream.Position = 0;
            var read = new CsvReader(c.Options).ReadAsync<FuzzRow>(stream).ToListAsync().AsTask().GetAwaiter().GetResult();

            if (read.Count != c.Rows.Count)
                throw new InvalidOperationException($"{read.Count} linhas lidas de {c.Rows.Count}");
            for (int i = 0; i < read.Count; i++)
            {
                var (expected, actual) = (c.Rows[i], read[i]);
                // string? vazia é lida como null (célula vazia)
                var expectedOptional = string.IsNullOrEmpty(expected.Optional) ? null : expected.Optional;
                if (actual.Text != expected.Text || actual.Optional != expectedOptional || actual.Symbol != expected.Symbol || actual.Number != expected.Number)
                {
                    throw new InvalidOperationException(
                        $"Linha {i}: Text {Hostile.Show(expected.Text)} → {Hostile.Show(actual.Text)}; " +
                        $"Optional {Hostile.Show(expectedOptional)} → {Hostile.Show(actual.Optional)}; " +
                        $"Symbol {Hostile.Show(expected.Symbol.ToString())} → {Hostile.Show(actual.Symbol.ToString())}");
                }
            }
        }).Check(Hostile.Config(400));
    }

    [Test]
    public void Csv_ArbitraryInput_FailsOnlyWithCsvException()
    {
        var caseGen =
            from text in Hostile.AnyText
            from header in Gen.Elements(true, false)
            from strict in Gen.Elements(true, false)
            from skip in Gen.Elements(true, false)
            select (Text: text, Options: new CsvOptions { HasHeader = header, StrictColumnCount = strict, SkipInvalidRows = skip, MaxFieldLength = 64, MaxColumns = 16 });

        Prop.ForAll(caseGen.ToArbitrary(), c =>
        {
            // Cabeçalho válido na frente para que o conteúdo hostil chegue à conversão de valores
            var content = (c.Options.HasHeader ? "Id;Name;Date;Value;Day;Active\n" : "") + c.Text;
            try
            {
                new CsvReader(c.Options).ReadAsync<TypedRow>(new MemoryStream(Encoding.UTF8.GetBytes(content)))
                    .ToListAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (CsvException)
            {
            }
        }).Check(Hostile.Config(500));
    }

    // ---------- JSON ----------

    public sealed record JsonDto(int Id, string? Name, DateOnly Date, DayOfWeek Day, decimal Value, string[]? Tags);

    private static readonly string[] JsonTokens = ["{", "}", "[", "]", ":", ",", "\"id\"", "\"name\"", "\"date\"", "\"day\"", "\"value\"",
        "\"tags\"", "1", "-1", "1e400", "0.5", "\"Monday\"", "\"Segunda\"", "7", "\"2026-02-30\"", "\"2026-01-01\"", "null", "true", "\"\\u0000\"", "\"\\uD800\""];

    [Test]
    public void Json_ArbitraryInput_TryFromJsonNeverThrows_FromJsonThrowsOnlyJsonException()
    {
        var jsonGen = Gen.OneOf(Hostile.AnyText, Gen.Elements(JsonTokens).ListOf().Select(string.Concat));

        Prop.ForAll(jsonGen.ToArbitrary(), json =>
        {
            _ = json.TryFromJson<JsonDto>(out _);
            _ = json.TryFromJson<ApiResponse<JsonDto>>(out _);
            try
            {
                _ = json.FromJson<JsonDto>();
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException)
            {
            }
        }).Check(Hostile.Config(500));
    }

    // ---------- Criptografia ----------

    private static readonly AesGcmCryptography Aes = new();
    private static readonly byte[] AesKey = Aes.GenerateKey();

    [Test]
    public void AesGcm_ArbitraryData_RoundTrips_AndAnyBitFlipOrTruncationIsRejected()
    {
        var caseGen =
            from data in Hostile.Bytes(2048)
            from aad in Hostile.Bytes(32)
            from position in Gen.Choose(0, int.MaxValue)
            from bit in Gen.Choose(0, 7)
            from cut in Gen.Choose(0, int.MaxValue)
            select (Data: data, Aad: aad, Position: position, Bit: bit, Cut: cut);

        Prop.ForAll(caseGen.ToArbitrary(), c =>
        {
            var encrypted = Aes.Encrypt(c.Data, AesKey, c.Aad);
            if (!Aes.Decrypt(encrypted, AesKey, c.Aad).AsSpan().SequenceEqual(c.Data))
                throw new InvalidOperationException("Ida e volta divergente.");

            var tampered = (byte[])encrypted.Clone();
            tampered[c.Position % tampered.Length] ^= (byte)(1 << c.Bit);
            ExpectCryptographicException(() => Aes.Decrypt(tampered, AesKey, c.Aad), "bit invertido");
            ExpectCryptographicException(() => Aes.Decrypt(encrypted[..(c.Cut % encrypted.Length)], AesKey, c.Aad), "dados truncados");
            ExpectCryptographicException(() => Aes.Decrypt(encrypted, AesKey, [.. c.Aad, 0]), "dado associado diferente");
        }).Check(Hostile.Config(500));
    }

    [Test]
    public void AesGcm_Stream_AnyBitFlipOrTruncationIsRejected()
    {
        var caseGen =
            from length in Gen.Elements(0, 1, 1000, 64 * 1024, 64 * 1024 + 1, 150_000)
            from position in Gen.Choose(0, int.MaxValue)
            from bit in Gen.Choose(0, 7)
            from truncate in Gen.Elements(true, false)
            select (Length: length, Position: position, Bit: bit, Truncate: truncate);

        Prop.ForAll(caseGen.ToArbitrary(), c =>
        {
            var data = RandomNumberGenerator.GetBytes(c.Length);
            using var encrypted = new MemoryStream();
            Aes.EncryptAsync(new MemoryStream(data), encrypted, AesKey).GetAwaiter().GetResult();
            var bytes = encrypted.ToArray();

            if (c.Truncate)
                bytes = bytes[..(c.Position % bytes.Length)];
            else
                bytes[c.Position % bytes.Length] ^= (byte)(1 << c.Bit);

            ExpectCryptographicException(() => Aes.DecryptAsync(new MemoryStream(bytes), Stream.Null, AesKey).GetAwaiter().GetResult(),
                c.Truncate ? "stream truncado" : "bit invertido no stream");
        }).Check(Hostile.Config(150));
    }

    private static readonly Lazy<RsaKeyPair> RsaKeys = new(() => new RsaCryptography().GenerateKeyPair());

    [Test]
    public void Hybrid_AnyTampering_FailsWithTheSameGenericError()
    {
        var hybrid = new HybridCryptography();
        var keys = RsaKeys.Value;
        string? genericMessage = null;

        var caseGen =
            from data in Hostile.Bytes(512)
            from position in Gen.Choose(0, int.MaxValue)
            from bit in Gen.Choose(0, 7)
            select (Data: data, Position: position, Bit: bit);

        Prop.ForAll(caseGen.ToArbitrary(), c =>
        {
            var encrypted = hybrid.Encrypt(c.Data, keys.PublicKeyPem);
            encrypted[c.Position % encrypted.Length] ^= (byte)(1 << c.Bit);
            try
            {
                hybrid.Decrypt(encrypted, keys.PrivateKeyPem);
                throw new InvalidOperationException("Dado adulterado foi aceito.");
            }
            catch (CryptographicException ex)
            {
                // Sem oráculo: qualquer adulteração (versão, chave cifrada, nonce, tag, conteúdo) gera a mesma mensagem
                genericMessage ??= ex.Message;
                if (ex.Message != genericMessage && !ex.Message.Contains("versão", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Mensagens diferentes revelam a causa: '{genericMessage}' x '{ex.Message}'");
            }
        }).Check(Hostile.Config(100));
    }

    [Test]
    public void Rsa_ArbitraryPem_FailsOnlyWithArgumentOrCryptographicException()
    {
        var rsa = new RsaCryptography();
        var pemGen = Gen.OneOf(
            Hostile.AnyText,
            Hostile.Text.Select(body => $"-----BEGIN PUBLIC KEY-----\n{body}\n-----END PUBLIC KEY-----"),
            Hostile.Bytes(400).Select(bytes => $"-----BEGIN PUBLIC KEY-----\n{Convert.ToBase64String(bytes)}\n-----END PUBLIC KEY-----"),
            Hostile.Bytes(400).Select(bytes => $"-----BEGIN RSA PRIVATE KEY-----\n{Convert.ToBase64String(bytes)}\n-----END RSA PRIVATE KEY-----"));

        Prop.ForAll(pemGen.ToArbitrary(), pem =>
        {
            try { rsa.Encrypt([1, 2, 3], pem); } catch (Exception ex) when (ex is ArgumentException or CryptographicException) { }
            try { rsa.VerifyData([1, 2, 3], new byte[256], pem); } catch (Exception ex) when (ex is ArgumentException or CryptographicException) { }
            try { rsa.SignData([1, 2, 3], pem); } catch (Exception ex) when (ex is ArgumentException or CryptographicException) { }
        }).Check(Hostile.Config(300));
    }

    [Test]
    public void PasswordHasher_ArbitraryStoredHash_ReturnsFalseWithoutThrowing()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var storedGen = Gen.OneOf(
            Hostile.AnyText,
            from iterations in Gen.Elements("-1", "0", "1", "99999", "10000001", "2147483647", "99999999999", " 100000", "1e5", "١٠٠٠٠٠")
            from salt in Hostile.Bytes(80)
            from hash in Hostile.Bytes(80)
            from separator in Gen.Elements("$", "$$", ":")
            select string.Join(separator, "PBKDF2-SHA256", iterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash)));

        Prop.ForAll(storedGen.ToArbitrary(), Hostile.Text.ToArbitrary(), (stored, password) =>
        {
            // Senha vazia ou hash inválido: sempre false, nunca exceção (o hash armazenado também é entrada não confiável)
            if (hasher.Verify(password, stored))
                throw new InvalidOperationException($"Hash forjado aceito: {Hostile.Show(stored)}");
            _ = hasher.NeedsRehash(stored);
        }).Check(Hostile.Config(40));
    }

    [Test]
    public void FixedTimeEquals_MatchesOrdinalAndHexEquality()
    {
        var pairGen = Gen.OneOf(
            Gen.Zip(Hostile.AnyText, Hostile.AnyText),
            Hostile.AnyText.Select(s => (s, s)),
            Hostile.Bytes(48).Select(b => (Convert.ToHexString(b), Convert.ToHexString(b).ToLowerInvariant())));

        Prop.ForAll(pairGen.ToArbitrary(), p =>
        {
            if (HashHelper.FixedTimeEquals(p.Item1, p.Item2) != string.Equals(p.Item1, p.Item2, StringComparison.Ordinal))
                throw new InvalidOperationException($"FixedTimeEquals divergente: {Hostile.Show(p.Item1)} x {Hostile.Show(p.Item2)}");

            bool bothHex = HexRegex().IsMatch(p.Item1) && HexRegex().IsMatch(p.Item2);
            bool expectedHex = bothHex && string.Equals(p.Item1, p.Item2, StringComparison.OrdinalIgnoreCase);
            if (HashHelper.FixedTimeEqualsHex(p.Item1, p.Item2) != expectedHex)
                throw new InvalidOperationException($"FixedTimeEqualsHex divergente: {Hostile.Show(p.Item1)} x {Hostile.Show(p.Item2)}");
        }).Check(Hostile.Config(500));
    }

    // \z (fim absoluto): $ aceitaria uma quebra de linha no final
    [GeneratedRegex(@"^(?:[0-9a-fA-F]{2})*\z")]
    private static partial Regex HexRegex();

    // ---------- Documentos e texto ----------

    [Test]
    public void Documents_ArbitraryInput_NeverThrow_AndFormattingIsConsistent()
    {
        Prop.ForAll(Hostile.AnyText.ToArbitrary(), value =>
        {
            bool cpf = DocumentValidator.IsValidCpf(value);
            bool cnpj = DocumentValidator.IsValidCnpj(value);
            _ = DocumentValidator.IsValidCpfOrCnpj(value) | DocumentValidator.IsValidPis(value) | DocumentValidator.IsValidCep(value)
                | DocumentValidator.IsValidPhone(value) | DocumentValidator.IsValidEmail(value);

            foreach (var format in new Func<string?, string>[] { DocumentFormatter.FormatCpf, DocumentFormatter.FormatCnpj, DocumentFormatter.FormatCep,
                DocumentFormatter.FormatPis, DocumentFormatter.FormatPhone, DocumentFormatter.FormatCpfOrCnpj })
            {
                var once = format(value);
                if (format(once) != once)
                    throw new InvalidOperationException($"Formatação não idempotente: {Hostile.Show(value)} → {Hostile.Show(once)} → {Hostile.Show(format(once))}");
            }

            // Documento válido continua válido depois de formatado, com os mesmos caracteres úteis
            if (cpf && (!DocumentValidator.IsValidCpf(DocumentFormatter.FormatCpf(value)) || DocumentFormatter.RemoveMask(DocumentFormatter.FormatCpf(value)) != DocumentFormatter.RemoveMask(value)))
                throw new InvalidOperationException($"CPF válido mudou ao formatar: {Hostile.Show(value)}");
            if (cnpj && !DocumentValidator.IsValidCnpj(DocumentFormatter.FormatCnpj(value)))
                throw new InvalidOperationException($"CNPJ válido mudou ao formatar: {Hostile.Show(value)}");

            _ = SensitiveDataMasker.MaskCpf(value) + SensitiveDataMasker.MaskCnpj(value) + SensitiveDataMasker.MaskEmail(value)
                + SensitiveDataMasker.MaskPhone(value) + SensitiveDataMasker.MaskCreditCard(value);
        }).Check(Hostile.Config(500));
    }

    [Test]
    public void GeneratedDocuments_AreAlwaysValid_InEveryFormat()
    {
        Prop.ForAll(Gen.Elements(true, false).ToArbitrary(), formatted =>
        {
            var cpf = DocumentGenerator.GenerateCpf(formatted);
            var cnpj = DocumentGenerator.GenerateCnpj(formatted);
            var alphanumeric = DocumentGenerator.GenerateCnpj(formatted, alphanumeric: true);
            var pis = DocumentGenerator.GeneratePis(formatted);
            if (!DocumentValidator.IsValidCpf(cpf) || !DocumentValidator.IsValidCnpj(cnpj) || !DocumentValidator.IsValidCnpj(alphanumeric)
                || !DocumentValidator.IsValidPis(pis) || !DocumentValidator.IsValidCnpj(alphanumeric.ToLowerInvariant()))
                throw new InvalidOperationException($"Documento gerado inválido: {cpf} {cnpj} {alphanumeric} {pis}");
        }).Check(Hostile.Config(1000));
    }

    [Test]
    public void Mask_KeepsLengthAndOnlyRequestedCharactersVisible()
    {
        var caseGen =
            from value in Hostile.Text
            from start in Gen.Choose(0, 10)
            from end in Gen.Choose(0, 10)
            select (Value: value, Start: start, End: end);

        Prop.ForAll(caseGen.ToArbitrary(), c =>
        {
            var masked = SensitiveDataMasker.Mask(c.Value, c.Start, c.End);
            if (masked.Length != c.Value.Length)
                throw new InvalidOperationException("Tamanho alterado.");

            // Nunca mais visível do que o pedido, e nunca meio par surrogate (texto UTF-16 continua válido)
            int visible = masked.Zip(c.Value).Count(p => p.First == p.Second && p.First != SensitiveDataMasker.DefaultMaskChar);
            if (visible > c.Start + c.End || !IsValidUtf16(masked))
                throw new InvalidOperationException($"Máscara expôs demais: {Hostile.Show(c.Value)} → {Hostile.Show(masked)}");
        }).Check(Hostile.Config(500));
    }

    [Test]
    public void StringExtensions_ArbitraryInput_NeverThrow_AndSlugIsSafe()
    {
        Prop.ForAll(Hostile.AnyText.ToArbitrary(), Gen.Choose(0, 50).ToArbitrary(), (value, length) =>
        {
            var slug = value.ToSlug();
            if (!SlugRegex().IsMatch(slug))
                throw new InvalidOperationException($"Slug inseguro: {Hostile.Show(value)} → {Hostile.Show(slug)}");

            _ = value.RemoveAccents() + value.OnlyDigits() + value.OnlyLettersAndDigits() + value.CollapseWhitespace()
                + value.Truncate(length) + value.Left(length) + value.Right(length) + value.ToTitleCase() + value.ToPascalCase()
                + value.ToCamelCase() + value.ToSnakeCase() + value.ToKebabCase();
            _ = value.EqualsIgnoreCaseAndAccents(slug) | value.ContainsIgnoreCaseAndAccents("a") | value.TryFromBase64(out _);
            if (IsValidUtf16(value) && value.ToBase64().FromBase64() != value)
                throw new InvalidOperationException($"Base64 divergente: {Hostile.Show(value)}");
        }).Check(Hostile.Config(500));
    }

    [GeneratedRegex(@"^(?:[a-z0-9]+(?:-[a-z0-9]+)*)?\z")]
    private static partial Regex SlugRegex();

    // ---------- Números e datas ----------

    [Test]
    public void BrazilianDecimal_FormatThenParse_RoundTrips_AndArbitraryTextNeverThrows()
    {
        var decimalGen = from units in Gen.Choose(int.MinValue, int.MaxValue)
                         from scale in Gen.Choose(0, 1_000_000)
                         from cents in Gen.Choose(0, 99)
                         select units * (decimal)scale / 1000 + cents / 100m;

        Prop.ForAll(decimalGen.ToArbitrary(), Hostile.AnyText.ToArbitrary(), (value, text) =>
        {
            var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            if (!value.ToBrazilianNumber().TryParseBrazilianDecimal(out var parsed) || parsed != rounded)
                throw new InvalidOperationException($"{value} → {value.ToBrazilianNumber()} → {parsed}");
            if (!value.ToCurrency().TryParseBrazilianDecimal(out parsed) || parsed != rounded)
                throw new InvalidOperationException($"{value} → {value.ToCurrency()} → {parsed}");

            _ = text.TryParseBrazilianDecimal(out _);
        }).Check(Hostile.Config(500));
    }

    [Test]
    public void NumberToWords_WholeRange_NeverThrowsInside_AndRejectsOutside()
    {
        var numberGen = Gen.OneOf(
            Gen.Choose(int.MinValue, int.MaxValue).Select(i => (long)i),
            Gen.Choose(0, int.MaxValue).Select(i => NumberToWordsConverter.MaxValue - i),
            Gen.Choose(0, int.MaxValue).Select(i => -NumberToWordsConverter.MaxValue + i));

        Prop.ForAll(numberGen.ToArbitrary(), number =>
        {
            if (string.IsNullOrWhiteSpace(NumberToWordsConverter.ToWords(number)))
                throw new InvalidOperationException($"Extenso vazio para {number}");
        }).Check(Hostile.Config(500));

        foreach (var outside in new[] { NumberToWordsConverter.MaxValue + 1, -NumberToWordsConverter.MaxValue - 1, long.MaxValue, long.MinValue })
        {
            try
            {
                NumberToWordsConverter.ToWords(outside);
                throw new InvalidOperationException($"{outside} aceito fora do limite");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }
    }

    [Test]
    public void BusinessDays_AddAndCount_AreConsistentInverses()
    {
        var calculator = new BusinessDayCalculator(new BrazilianNationalHolidays());
        var caseGen = from day in Gen.Choose(new DateOnly(1990, 1, 1).DayNumber, new DateOnly(2150, 1, 1).DayNumber)
                      from days in Gen.Choose(1, 2_000)
                      select (Start: calculator.NextOrSameBusinessDay(DateOnly.FromDayNumber(day)), Days: days);

        Prop.ForAll(caseGen.ToArbitrary(), c =>
        {
            var end = calculator.AddBusinessDays(c.Start, c.Days);
            if (!calculator.IsBusinessDay(end) || calculator.CountBusinessDays(c.Start, end) != c.Days + 1
                || calculator.AddBusinessDays(end, -c.Days) != c.Start || calculator.GetBusinessDays(c.Start, end).Count != c.Days + 1)
                throw new InvalidOperationException($"{c.Start} + {c.Days} = {end}");
        }).Check(Hostile.Config(300));
    }

    // ---------- Fontes de feriados ----------

    [Test]
    public void HolidaySources_ArbitraryContent_FailOnlyWithHolidaySourceException()
    {
        var contentGen = Gen.OneOf(
            Hostile.AnyText,
            Hostile.Text.Select(t => "Data;Descricao;Uf;CodigoIbge\n" + t),
            Gen.Elements(JsonTokens.Concat(["\"data\"", "\"descricao\"", "\"uf\"", "\"codigoIbge\"", "\"SP\"", "3550308", "\"XX\""]).ToArray())
                .ListOf().Select(string.Concat));

        Prop.ForAll(contentGen.ToArbitrary(), content =>
        {
            Func<Stream> open = () => new MemoryStream(Encoding.UTF8.GetBytes(content));
            foreach (var source in new IHolidaySource[] { new CsvHolidaySource(open, "csv"), new JsonHolidaySource(open, "json") })
            {
                try
                {
                    HolidayCalendar.CreateBuilder().AddSource(source).BuildAsync().GetAwaiter().GetResult();
                }
                catch (HolidaySourceException)
                {
                }
            }
        }).Check(Hostile.Config(400));
    }

    private static void ExpectCryptographicException(Action action, string description)
    {
        try
        {
            action();
        }
        catch (CryptographicException)
        {
            return;
        }

        throw new InvalidOperationException($"Adulteração não detectada: {description}");
    }

    private static bool IsValidUtf16(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                i++;
            else if (char.IsSurrogate(value[i]))
                return false;
        }

        return true;
    }
}
