using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class Duchy(int i, List<Cell> cells, string name) : ITitle
    {

        public int Id { get; set; } = i;

        public string Name { get; set; } = name;
        public MagickColor? Color { get; set; }
        public List<Cell> Cells { get; set; } = cells;

        public List<Cell> GetAllCells()
        {
            // TODO: Implement when counties are introduced
            // If counties are introduced, aggregate cells from counties
            // if (Counties.Any())
            // {
            //     return Counties.SelectMany(county => county.GetAllCells()).ToList();
            // }

            // Otherwise, return directly assigned cells
            return Cells;
        }
    }
}