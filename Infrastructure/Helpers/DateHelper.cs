
namespace Infrastructure.Helpers;

public static class DateHelper
{
	/// <summary>
	/// Count weekdays between two dates (inclusive), excluding weekends.
	/// </summary>
	public static int DatesBetweenExcludingWeekends(DateTime start, DateTime end)
	{
		int days = 0;
		for (var dt = start.Date; dt <= end.Date; dt = dt.AddDays(1))
		{
			if (dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday)
				days++;
		}
		return days;
	}

	/// <summary>
	/// Count weekdays from contract start to end of given number of years.
	/// (Access CountWeekdays)
	/// </summary>
	public static int CountWeekdays(DateTime startDate, int yearsToAdd)
	{
		var endDate = new DateTime(startDate.Year + yearsToAdd, startDate.Month, startDate.Day);
		endDate = new DateTime(endDate.Year, 12, 31); // end of year after N years
		return DatesBetweenExcludingWeekends(startDate, endDate);
	}

	/// <summary>
	/// Count weekdays from inputDate until end of the year.
	/// (Access DaysToEndOfYearExcludingWeekends)
	/// </summary>
	public static int DaysToEndOfYearExcludingWeekends(DateTime inputDate)
	{
		var endDate = new DateTime(inputDate.Year, 12, 31);
		return DatesBetweenExcludingWeekends(inputDate, endDate);
	}

	/// <summary>
	/// Days until end of N years (including weekends).
	/// (Access DaysToEndOfTwoYears)
	/// </summary>
	public static int DaysToEndOfYears(DateTime startDate, int years)
	{
		var endDate = startDate.AddYears(years);
		endDate = new DateTime(endDate.Year, 12, 31);
		return (endDate - startDate).Days;
	}

	/// <summary>
	/// Count weekdays from start of the year until inputDate.
	/// (Access DaysExcludingWeekendsFromDate)
	/// </summary>
	public static int DaysExcludingWeekendsFromDate(DateTime inputDate)
	{
		var startDate = new DateTime(inputDate.Year, 1, 1);
		return DatesBetweenExcludingWeekends(startDate, inputDate);
	}

	/// <summary>
	/// Repayment coefficient band (min balance → max balance → days).
	/// Used for EzhigodPogashenie and NeaktivOtEzhigodPogashenie.
	/// </summary>
	public class RepaymentBand
	{
		public decimal OstatokNach { get; }
		public decimal OstatokKon { get; }
		public int Days { get; }

		public RepaymentBand(decimal? nach, decimal? kon, int? days)
		{
			OstatokNach = nach ?? 0;
			OstatokKon = kon ?? 0;
			Days = days ?? 1; // avoid division by zero
		}

		public bool Matches(decimal balance) =>
			balance >= OstatokNach && balance <= OstatokKon;
	}
}
