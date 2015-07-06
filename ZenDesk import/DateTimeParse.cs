using System;

namespace ZenDesk_import
{
	internal static class DateTimeParse
	{
		private static DateTime _returnDate;

		public static DateTime? ParseNullableDateTime(string value)
		{
			return DateTime.TryParse(value, out _returnDate) ? _returnDate : (DateTime?)null;
		}
	}
}