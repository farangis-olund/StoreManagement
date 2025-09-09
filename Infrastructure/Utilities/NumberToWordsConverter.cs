using System;
using System.Text;

namespace Infrastructure.Utilities
{
	public static class NumberToWordsConverter
	{
		private static readonly string[] Units = { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
		private static readonly string[] Teens = { "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать" };
		private static readonly string[] Tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };
		private static readonly string[] Hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };
		private static readonly string[] Thousands = { "", "тысяча", "тысячи", "тысяч" };
		private static readonly string[] Millions = { "", "миллион", "миллиона", "миллионов" };

		public static string ConvertToRussianWords(decimal number)
		{
			if (number == 0)
				return "Ноль";

			var integralPart = (int)number;
			var fractionalPart = (int)Math.Round((number - integralPart) * 100);

			var sb = new StringBuilder();

			// целая часть прописью
			sb.Append(ConvertIntegralPart(integralPart));

			// дробная часть цифрами
			if (fractionalPart > 0)
			{
				sb.Append(" и ");
				sb.Append(fractionalPart.ToString("00")); // всегда два знака
			}

			var result = sb.ToString().Trim();
			return char.ToUpper(result[0]) + result.Substring(1);
		}

		private static string ConvertIntegralPart(int number)
		{
			if (number == 0)
				return "";

			var sb = new StringBuilder();

			if (number / 1000000 > 0)
			{
				sb.Append(ConvertIntegralPart(number / 1000000));
				sb.Append(" ");
				sb.Append(Millions[GetPluralForm(number / 1000000)]);
				number %= 1000000;
			}

			if (number / 1000 > 0)
			{
				sb.Append(ConvertIntegralPart(number / 1000));
				sb.Append(" ");
				sb.Append(Thousands[GetPluralForm(number / 1000)]);
				number %= 1000;
			}

			if (number / 100 > 0)
			{
				sb.Append(" ");
				sb.Append(Hundreds[number / 100]);
				number %= 100;
			}

			if (number >= 10 && number < 20)
			{
				sb.Append(" ");
				sb.Append(Teens[number - 10]);
				number = 0;
			}
			else if (number >= 20)
			{
				sb.Append(" ");
				sb.Append(Tens[number / 10]);
				number %= 10;
			}

			if (number > 0)
			{
				sb.Append(" ");
				sb.Append(Units[number]);
			}

			return sb.ToString().Trim();
		}

		private static int GetPluralForm(int number)
		{
			if (number % 10 == 1 && number % 100 != 11)
				return 1;
			else if ((number % 10 >= 2 && number % 10 <= 4) && !(number % 100 >= 12 && number % 100 <= 14))
				return 2;
			else
				return 3;
		}
	}

}
