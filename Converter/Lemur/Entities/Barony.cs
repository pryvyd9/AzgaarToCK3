using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class Barony : IProvince, ITitle
    {
        private readonly Burg burg;

        public int Id { get; set; }
        public string Name { get; set; }

        public List<Cell> Cells { get; set; } = new List<Cell>();

        // IProvince implementation
        public MagickColor Color { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public Barony(Burg value)
        {
            burg = value;
            Id = burg.id;
            Name = burg.Name;
        }

        public List<Cell> GetAllCells()
        {
            return Cells;
        }
    }
}