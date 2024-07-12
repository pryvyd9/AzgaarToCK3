using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class County : ITitle
    {
        public int Id { get; set; }

        public string Name { get; set; }
        public MagickColor? Color { get; set; }
        public List<Cell> Cells { get; set; }

        public Duchy? Duchy { get; set; }
        public List<Barony>? Baronies { get; set; }

        public Barony? Capital { get; set; }


        //constructor
        public County(int id, string name, List<Barony>? baronies = null, Duchy? duchy = null, Barony? capital = null)
        {
            Id = id;
            Name = name;
            if (baronies != null)
            {
                Baronies = baronies;
                Cells = Baronies.SelectMany(barony => barony.GetAllCells()).ToList();
            }
            else
            {
                Cells = new List<Cell>();
            }
            if (duchy != null)
            {
                Duchy = duchy;
            }

        }


        public List<Cell> GetAllCells()
        {
            return Baronies.SelectMany(barony => barony.GetAllCells()).ToList();
        }
    }
}