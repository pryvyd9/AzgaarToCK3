using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class Barony : IProvince, ITitle
    {
        public Barony(Burg value)
        {   
            //link to burg and back 1:1 relationship
            burg = value;
            value.Barony = this;
            Id = burg.id;
            Name = burg.Name;
    
        }
        public readonly Burg burg;
        public int Id { get; set; }
        public string Name { get; set; }

        public List<Cell> Cells { get; set; } = new List<Cell>();

        public MagickColor Color { get; set; }
        /// <summary>
        /// See <see cref="ConversionManager.GenerateBaronyAdjacency"/> for how this is generated.
        /// </summary>
        public List<Barony>? Neighbors { get; set; }
        public ITitle Parent { get; set; }

        public List<Cell> GetAllCells()
        {
            return Cells;
        }

        //Hash and equality check
        public override int GetHashCode()
        {
            return Id;
        }

        public override bool Equals(object? obj)
        {
            if (obj is Barony other)
            {
                return Id == other.Id;
            }
            return false;
        }

        public MagickColor? GetColor()
        {
            return Color;
        }
        //Get the dominant culture of the barony by counting the number of cells with each culture and returning the most common
        public Culture GetDominantCulture(Map map)
        {
            var cultureCounts = new Dictionary<int, int>();
            foreach (var cell in Cells)
            {
                if (cultureCounts.ContainsKey(cell.Culture))
                {
                    cultureCounts[cell.Culture]++;
                }
                else
                {
                    cultureCounts[cell.Culture] = 1;
                }
            }
            var mostCommon = cultureCounts.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            return map.JsonMap.pack.cultures[mostCommon];
        }

        public Religion GetDominantReligion(Map map)
        {
            var religionCounts = new Dictionary<int, int>();
            foreach (var cell in Cells)
            {
                if (religionCounts.ContainsKey(cell.Religion))
                {
                    religionCounts[cell.Religion]++;
                }
                else
                {
                    religionCounts[cell.Religion] = 1;
                }
            }
            var mostCommon = religionCounts.Aggregate((l, r) => l.Value > r.Value ? l : r).Key;
            return map.JsonMap.pack.religions[mostCommon];
        }

        public Dictionary<ITitle, int> GetNeighbours()
        {
            return Neighbors?.ToDictionary(x => x as ITitle, x => 1) ?? new Dictionary<ITitle, int>();
        }
    }
}