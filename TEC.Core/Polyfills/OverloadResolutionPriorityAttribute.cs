#if !NET9_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill do atributo do .NET 9+ para o alvo <c>net8.0</c>. O compilador C# 13+ reconhece o atributo pelo nome
/// completo, então a resolução de sobrecarga fica idêntica nos dois TFMs.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
internal sealed class OverloadResolutionPriorityAttribute(int priority) : Attribute
{
    /// <summary>Prioridade (maior vence).</summary>
    public int Priority { get; } = priority;
}
#endif
