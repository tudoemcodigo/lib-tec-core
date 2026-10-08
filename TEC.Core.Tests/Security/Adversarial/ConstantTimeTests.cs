using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using TEC.Core.Cryptography.Hashing;

namespace TEC.Core.Tests.Security.Adversarial;

/// <summary>
/// Canais laterais de tempo: comparações de segredos e verificação de senha não podem variar de tempo conforme o dado.
/// </summary>
/// <remarks>
/// O teste rápido (CI) compara medianas do PBKDF2. Os testes estatísticos (estilo dudect: teste t de Welch entre duas
/// classes de entrada intercaladas aleatoriamente) são pesados e ruidosos: rodam sob demanda, na categoria
/// <see cref="HeavyCategory"/> (veja docs/testes.md).
/// </remarks>
[NotInParallel]
public class ConstantTimeTests
{
    /// <summary>Categoria dos testes de segurança pesados ([Explicit]).</summary>
    public const string HeavyCategory = "Seguranca-Pesada";

    // Limite do |t| de Welch: acima disso há diferença de tempo detectável entre as classes (dudect usa 4,5 a 10)
    private const double LeakThreshold = 10;

    [Test]
    public async Task PasswordVerify_UnknownUserAndMalformedHash_CostTheSameAsWrongPassword()
    {
        var hasher = new Pbkdf2PasswordHasher(100_000);
        var stored = hasher.Hash("senha correta");
        var cases = new Dictionary<string, string?> { ["inexistente"] = null, ["hash inválido"] = "PBKDF2-SHA256$abc" };

        _ = hasher.Verify("aquecimento", stored);
        foreach (var (name, candidate) in cases)
        {
            // Medição em pares (um logo após o outro, em ordem alternada) e mediana das razões: ruído da máquina
            // (outros testes em paralelo, frequência da CPU) afeta os dois lados do par por igual
            var ratios = new List<double>();
            for (int i = 0; i < 15; i++)
            {
                double existing, other;
                if (i % 2 == 0)
                {
                    existing = Measure(() => hasher.Verify("senha errada", stored));
                    other = Measure(() => hasher.Verify("senha errada", candidate));
                }
                else
                {
                    other = Measure(() => hasher.Verify("senha errada", candidate));
                    existing = Measure(() => hasher.Verify("senha errada", stored));
                }

                ratios.Add(other / existing);
            }

            // Limites largos de propósito: o CI roda vários processos de teste em paralelo e o tempo de parede oscila.
            // A regressão que importa (sem a derivação fictícia) daria ~0,0003×, ordens de grandeza fora do intervalo;
            // a verificação estatística fina é PasswordVerify_DefaultIterations_UnknownUserIsIndistinguishable
            double ratio = Median(ratios);
            await Assert.That(ratio).IsBetween(0.25, 4.0).Because($"{name}: {ratio:F2}× o tempo de um usuário existente (enumeração de usuários por tempo)");
        }
    }

    [Test]
    [Explicit]
    [Category(HeavyCategory)]
    public async Task FixedTimeEquals_NoTimingDifference_BetweenEarlyAndLateMismatch()
    {
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var earlyMismatch = "0" + secret[1..];
        var lateMismatch = secret[..^1] + "0";
        if (earlyMismatch == secret || lateMismatch == secret)
            return;

        double t = WelchT(candidate => HashHelper.FixedTimeEquals(secret, candidate), earlyMismatch, lateMismatch);
        double tHex = WelchT(candidate => HashHelper.FixedTimeEqualsHex(secret, candidate), earlyMismatch.ToLowerInvariant(), lateMismatch.ToLowerInvariant());
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"|t| FixedTimeEquals = {Math.Abs(t):F2} · FixedTimeEqualsHex = {Math.Abs(tHex):F2} (limite {LeakThreshold})"));

        await Assert.That(Math.Abs(t)).IsLessThan(LeakThreshold);
        await Assert.That(Math.Abs(tHex)).IsLessThan(LeakThreshold);
    }

    [Test]
    [Explicit]
    [Category(HeavyCategory)]
    public async Task TimingDetector_DetectsANaiveComparison()
    {
        // Controle do método: uma comparação comum (que para no primeiro caractere diferente) precisa ser detectada,
        // senão um |t| baixo nos outros testes não prova nada
        var secret = new string('a', 32 * 1024);
        var earlyMismatch = "b" + secret[1..];
        var lateMismatch = secret[..^1] + "b";

        double t = WelchT(candidate => string.Equals(secret, candidate, StringComparison.Ordinal), earlyMismatch, lateMismatch);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"|t| string.Equals (vulnerável) = {Math.Abs(t):F2}"));

        await Assert.That(Math.Abs(t)).IsGreaterThan(LeakThreshold);
    }

    [Test]
    [Explicit]
    [Category(HeavyCategory)]
    public async Task PasswordVerify_DefaultIterations_UnknownUserIsIndistinguishable()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var stored = hasher.Hash("senha correta");

        double t = WelchT(candidate => hasher.Verify("senha errada", candidate), stored, null, samples: 300, batch: 1);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"|t| PBKDF2 existente × inexistente = {Math.Abs(t):F2}"));

        await Assert.That(Math.Abs(t)).IsLessThan(LeakThreshold);
    }

    private static double Measure(Func<bool> action)
    {
        long start = Stopwatch.GetTimestamp();
        _ = action();
        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    /// <summary>
    /// Teste t de Welch entre duas classes de entrada, sorteadas a cada amostra (estilo dudect). Cada amostra mede
    /// <paramref name="batch"/> chamadas; as amostras acima do percentil 90 são descartadas (interrupções do SO e do GC).
    /// </summary>
    /// <remarks>
    /// As duas classes passam pelo <b>mesmo</b> delegate e pelo mesmo laço: só a entrada muda. Delegates diferentes por
    /// classe são compilados em separado pelo JIT (nível de compilação e alinhamento de código próprios) e essa diferença
    /// aparece como falso vazamento. Cada entrada também é copiada 64 vezes em posições de memória sorteadas e usada em
    /// rodízio, para que o alinhamento dos dados (linhas de cache tocadas por comparações vetorizadas) não dependa da classe.
    /// </remarks>
    private static double WelchT(Func<string?, bool> operation, string? inputA, string? inputB, int samples = 40_000, int batch = 100)
    {
        const int copies = 64;
        var a = new List<double>(samples);
        var b = new List<double>(samples);
        // Random (não criptográfico) só sorteia a ordem das medições e as posições na memória, com semente fixa: sem uso
        // de segurança
#pragma warning disable CA5394
        var random = new Random(42);
        var poolA = new string?[copies];
        var poolB = new string?[copies];
        // Preenchimentos vivos até o fim: uma compactação do GC preserva os deslocamentos sorteados
        var padding = new List<byte[]>(copies * 2);
        for (int i = 0; i < copies; i++)
        {
            bool aFirst = random.Next(2) == 0;
            (aFirst ? poolA : poolB)[i] = Copy(aFirst ? inputA : inputB);
            (aFirst ? poolB : poolA)[i] = Copy(aFirst ? inputB : inputA);
        }

        for (int i = 0; i < 2_000; i++)
            _ = operation(poolA[i & (copies - 1)]) ^ operation(poolB[i & (copies - 1)]);

        int next = 0;
        for (int i = 0; i < samples; i++)
        {
            bool useA = random.Next(2) == 0;
            var pool = useA ? poolA : poolB;
            long start = Stopwatch.GetTimestamp();
            for (int j = 0; j < batch; j++)
                _ = operation(pool[next++ & (copies - 1)]);
            (useA ? a : b).Add(Stopwatch.GetTimestamp() - start);
        }

        GC.KeepAlive(padding);
        double cut = Percentile([.. a, .. b], 0.90);
        var croppedA = a.Where(x => x <= cut).ToArray();
        var croppedB = b.Where(x => x <= cut).ToArray();
        double meanA = croppedA.Average(), meanB = croppedB.Average();
        double varA = croppedA.Sum(x => (x - meanA) * (x - meanA)) / (croppedA.Length - 1);
        double varB = croppedB.Sum(x => (x - meanB) * (x - meanB)) / (croppedB.Length - 1);
        double denominator = Math.Sqrt(varA / croppedA.Length + varB / croppedB.Length);
        return denominator == 0 ? 0 : (meanA - meanB) / denominator;

        string? Copy(string? value)
        {
            padding.Add(new byte[random.Next(0, 8) * 8]);   // desloca a próxima alocação em 0 a 56 bytes
            return value is null ? null : new string(value.AsSpan());
        }
#pragma warning restore CA5394
    }

    private static double Median(List<double> values) => Percentile(values, 0.5);

    private static double Percentile(List<double> values, double fraction)
    {
        var sorted = values.Order().ToArray();
        return sorted[Math.Clamp((int)(fraction * sorted.Length), 0, sorted.Length - 1)];
    }
}
