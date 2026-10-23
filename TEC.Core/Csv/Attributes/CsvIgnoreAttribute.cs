namespace TEC.Core.Csv.Attributes;

/// <summary>
/// Indica que a propriedade não deve ser lida nem escrita no CSV.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class CsvIgnoreAttribute : Attribute;
