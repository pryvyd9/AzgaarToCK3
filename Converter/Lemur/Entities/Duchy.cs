using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class Duchy(int i, List<Cell> cells, string name) : ITitle
    {

        public int Id { get; set; } = i;

        public string Name { get; set; } = name;
        public MagickColor? Color { get; set; }
        public List<Cell> Cells { get; set; } = cells;
        public List<Barony> Baronies { get; set; } = new List<Barony>();

        public List<Cell> GetAllCells()
        {
            //return directly assigned cells
            return Cells;
        }

        public string Ck3_Id()
        {
            return $"d_{Name}_{Id}";
        }
    }
}