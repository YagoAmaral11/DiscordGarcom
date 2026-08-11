using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordGarçom.Containers.Core;

public interface IScheduler
{    
    public bool ScheduleCallback(Delegate callback, object[] parameters, ulong ID, DateTimeOffset execution, bool ManagedCallback = true, bool AllowLateRepeat = false);
    public bool ScheduleRepeatEvery(Delegate callback, object[] parameters, ulong ID, TimeSpan repeatInterval, bool ManagedCallback = true, bool AllowLateRepeat = false, DateTimeOffset? nextExecution = null);
    public bool ScheduleRepeatSemanal(Delegate callback, object[] parameters, ulong ID, SemanalRepeatDay[] repeatDays, bool ManagedCallback = true, bool AllowLateRepeat = false, DateTimeOffset? nextExecution = null);
    public bool ScheduleRepeatMonthly(Delegate callback, object[] parameters, ulong ID, MonthlyRepeatDate[] repeatDays, bool ManagedCallback = true, bool AllowLateRepeat = false, DateTimeOffset ? nextExecution = null);
    public bool ScheduleRepeatYearly(Delegate callback, object[] parameters, ulong ID, DateTimeOffset[] repeatDays, bool ManagedCallback = true, bool AllowLateRepeat = false, DateTimeOffset? nextExecution = null);
}

public struct SemanalRepeatDay(DayOfWeek dayOfWeek, TimeSpan time, TimeZoneInfo timezone = null)
{
    public DayOfWeek DayOfWeek { get; set; } = dayOfWeek;
    public TimeSpan TimeOfDay { get; set; } = time;
    public TimeZoneInfo TimeZone { get; set; } = timezone ??= TimeZoneInfo.Local;
}

public struct MonthlyRepeatDate(int dayOfMonth, TimeSpan time, TimeZoneInfo timezone = null)
{
    public int DayOfMonth { get; set; } = dayOfMonth;
    public TimeSpan TimeOfDay { get; set; } = time;
    public TimeZoneInfo TimeZone { get; set; } = timezone ??= TimeZoneInfo.Local;

    public bool IsValidDay(int month, int year)
    {
        if (DayOfMonth <= DateTime.DaysInMonth(year, month))
            return true;
        return false;
    }

    public bool IsValidTime()
    {
        if (TimeOfDay.TotalHours < 24)
            return true;
        return false;
    }

    public bool IsValid(int month, int year) => IsValidDay(month, year) && IsValidTime();
    
}

public enum ScheduleType
{
    Once, // Executa uma vez em uma data específica
    IntervalRepeat, // Executa repetidamente em um intervalo específico
    SemanalRepeat, // Executa em dias específicas da semana
    MonthlyRepeat, // Executa em dias específicos do mês
    YearlyRepeat, // Executa em datas específicas do ano
}

public class TimeZoneInfoConverter : JsonConverter<TimeZoneInfo>
{
    public override TimeZoneInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var id = reader.GetString();
        return id != null ? TimeZoneInfo.FindSystemTimeZoneById(id) : null;
    }

    public override void Write(Utf8JsonWriter writer, TimeZoneInfo value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Id);
    }
}