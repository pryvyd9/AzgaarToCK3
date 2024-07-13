using ImageMagick;

namespace Converter.Lemur.Entities
{
    /// <summary>
    /// Province in Crusader Kings 3 that can be a barony, sea zone, or a major river.
    /// </summary>
    public interface IProvince
    {
        /// <summary>
        /// The unique identifier of the province.
        /// </summary>
        public int Id { get; set; }
        public string Name { get; set; }

        public MagickColor Color { get; set; }

        public List<Cell> Cells { get; set; }
    }


    public interface ITitle
    {
        public int Id { get; }

        public string Name { get; set; }

        public MagickColor? Color { get; set; }

        public List<Cell> Cells { get; set; }

        /// <summary>
        /// A way to get all the cells in the title. 
        /// </summary>
        /// <returns></returns>
        public List<Cell> GetAllCells();
        /// <summary>
        /// Get the color of the title. Shoule return the assigned colour,
        /// but of null then look at the first ITitle lower in the hierarchy.
        /// </summary>
        public MagickColor? GetColor();
        
        public string Ck3_Id(){
            return $"x_{Name}_{Id}";
        }
    }
}