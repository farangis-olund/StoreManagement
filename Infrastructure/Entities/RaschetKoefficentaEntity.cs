namespace Infrastructure.Entities;

public class RaschetKoefficentaEntity
{
	public int Id { get; set; }

	// --- Ежедневное погашение ---
	public decimal KoefEzhPogashOstatokNach1 { get; set; }
	public decimal KoefEzhPogashOstatokKon1 { get; set; }
	public int KoefEzhPogashDin1 { get; set; }

	public decimal KoefEzhPogashOstatokNach2 { get; set; }
	public decimal KoefEzhPogashOstatokKon2 { get; set; }
	public int KoefEzhPogashDin2 { get; set; }

	public decimal KoefEzhPogashOstatokNach3 { get; set; }
	public decimal KoefEzhPogashOstatokKon3 { get; set; }
	public int KoefEzhPogashDin3 { get; set; }

	public decimal KoefEzhPogashOstatokNach4 { get; set; }
	public decimal KoefEzhPogashOstatokKon4 { get; set; }
	public int KoefEzhPogashDin4 { get; set; }

	public decimal KoefEzhPogashOstatokNach5 { get; set; }
	public decimal KoefEzhPogashOstatokKon5 { get; set; }
	public int KoefEzhPogashDin5 { get; set; }

	// --- Закуп ---
	public decimal KoefZakupa { get; set; }
	public int KoefZakupaDni { get; set; }

	// --- Запланированный закуп ---
	public decimal KoefZaplanZakup { get; set; }
	public int KoefZaplanZakupDni { get; set; }
}
