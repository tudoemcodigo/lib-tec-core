using System.Globalization;

namespace TEC.Core.Common.Globalization;

/// <summary>
/// Cultura pt-BR usada em todo o componente.
/// </summary>
/// <remarks>
/// Em ambientes com <c>InvariantGlobalization</c> habilitado (comum em imagens Docker enxutas),
/// <c>CultureInfo.GetCultureInfo("pt-BR")</c> lança exceção. Nesse caso é usada uma cultura
/// equivalente montada manualmente, evitando que a aplicação quebre na inicialização.
/// </remarks>
public static class BrazilianCulture
{
    private static readonly Lazy<CultureInfo> LazyInstance = new(Create);

    /// <summary>Instância somente leitura da cultura pt-BR.</summary>
    public static CultureInfo Instance => LazyInstance.Value;

    private static CultureInfo Create()
    {
        try
        {
            return CultureInfo.GetCultureInfo("pt-BR");
        }
        catch (CultureNotFoundException)
        {
            return CreateFallback();
        }
    }

    /// <summary>Monta a cultura pt-BR a partir da cultura invariante (usado quando o ICU não está disponível).</summary>
    internal static CultureInfo CreateFallback()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();

        var number = culture.NumberFormat;
        number.NumberDecimalSeparator = ",";
        number.NumberGroupSeparator = ".";
        number.CurrencySymbol = "R$";
        number.CurrencyDecimalSeparator = ",";
        number.CurrencyGroupSeparator = ".";
        number.CurrencyPositivePattern = 2; // "R$ n"
        number.CurrencyNegativePattern = 9; // "-R$ n"
        number.PercentDecimalSeparator = ",";
        number.PercentGroupSeparator = ".";
        number.PercentPositivePattern = 1;  // "n%"
        number.PercentNegativePattern = 1;  // "-n%"

        string[] months = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro", ""];
        string[] abbreviatedMonths = ["jan.", "fev.", "mar.", "abr.", "mai.", "jun.", "jul.", "ago.", "set.", "out.", "nov.", "dez.", ""];

        var date = culture.DateTimeFormat;
        date.MonthNames = months;
        date.MonthGenitiveNames = months;
        date.AbbreviatedMonthNames = abbreviatedMonths;
        date.AbbreviatedMonthGenitiveNames = abbreviatedMonths;
        date.DayNames = ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];
        date.AbbreviatedDayNames = ["dom.", "seg.", "ter.", "qua.", "qui.", "sex.", "sáb."];
        date.ShortestDayNames = ["D", "S", "T", "Q", "Q", "S", "S"];
        date.ShortDatePattern = "dd/MM/yyyy";
        date.LongDatePattern = "dddd, d 'de' MMMM 'de' yyyy";
        date.ShortTimePattern = "HH:mm";
        date.LongTimePattern = "HH:mm:ss";
        date.FullDateTimePattern = "dddd, d 'de' MMMM 'de' yyyy HH:mm:ss";
        date.MonthDayPattern = "d 'de' MMMM";
        date.YearMonthPattern = "MMMM 'de' yyyy";
        date.FirstDayOfWeek = DayOfWeek.Sunday;
        date.DateSeparator = "/";
        date.TimeSeparator = ":";

        return CultureInfo.ReadOnly(culture);
    }
}
