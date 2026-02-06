using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Shell.Core;

public interface IScheduler
{    
    public bool ScheduleCallback(Delegate callback, object[] parameters, uint ID, DateTimeOffset execution, bool ManagedCallback = true);
    public bool ScheduleRepeatEvery(Delegate callback, object[] parameters, uint ID, TimeSpan repeatInterval, bool ManagedCallback = true, DateTimeOffset? nextExecution = null);
    public bool ScheduleRepeatSemanal(Delegate callback, object[] parameters, uint ID, SemanalRepeatDay[] repeatDays, bool ManagedCallback = true, DateTimeOffset? nextExecution = null);
    public bool ScheduleRepeatMonthly(Delegate callback, object[] parameters, uint ID, MonthlyRepeatDate[] repeatDays, bool ManagedCallback = true, DateTimeOffset? nextExecution = null);
    public bool ScheduleRepeatYearly(Delegate callback, object[] parameters, uint ID, DateTimeOffset[] repeatDays, bool ManagedCallback = true, DateTimeOffset? nextExecution = null);
}

public struct SemanalRepeatDay(DayOfWeek dayOfWeek, TimeSpan time)
{
    public DayOfWeek DayOfWeek { get; set; } = dayOfWeek;
    public TimeSpan TimeOfDay { get; set; } = time;
}

public struct MonthlyRepeatDate(int dayOfMonth, TimeSpan time)
{
    public int DayOfMonth { get; set; } = dayOfMonth;
    public TimeSpan TimeOfDay { get; set; } = time;

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