using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class Kingdom : ITitle
    {
        public int Id {get; set;}
        public string Name {get; set;}
        public MagickColor? Color {get; set;}
        public List<Cell> Cells {get; set;} = new List<Cell>();

        public List<Duchy> Duchies {get; set;} = new List<Duchy>();

        public Kingdom(int id, string name, MagickColor? color)
        {
            Id = id;
            Name = name;
        }

        public List<Cell> GetAllCells()
        {
            //if no duchies throw exception saying no duchies
            if (Duchies.Count == 0)
            {
                throw new ArgumentException($"Cannot get cells from a kingdom [{Name}] with no duchies.");
            }
            //Get cells by calling Get cells on each duchy object in Duchies

            List<Cell> cells = new List<Cell>();
            foreach (var duchy in Duchies)
            {
                cells.AddRange(duchy.GetAllCells());
            }
            return cells;
        }

        public MagickColor? GetColor()
        {
            return Color ?? Duchies.FirstOrDefault()?.GetColor();
        }
    }
}