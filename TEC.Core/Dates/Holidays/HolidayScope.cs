namespace TEC.Core.Dates.Holidays;

/// <summary>
/// Abrangência de um feriado. É deduzida dos campos <see cref="Holiday.Uf"/> e <see cref="Holiday.IbgeCode"/>.
/// </summary>
public enum HolidayScope
{
    /// <summary>Vale em todo o país (sem UF e sem código IBGE).</summary>
    National = 0,

    /// <summary>Vale em uma UF (somente <see cref="Holiday.Uf"/>).</summary>
    State = 1,

    /// <summary>Vale em um município (<see cref="Holiday.IbgeCode"/> informado).</summary>
    Municipal = 2,
}
