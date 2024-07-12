using System;
using ImageMagick;

namespace Converter.Lemur.Entities
{
    public class Wasteland : IProvince
    {
        public int Id { get; set; }

        public int AzgaarIdProvinceId { get; set; } = 0;
        public string Name { get; set; }
        public MagickColor Color
        {
            get => MagickColor.FromRgba(0, 0, 0, 255); // Always return black
            set { /* Do nothing */ }
        }
        public List<Cell> Cells { get; set; }


    public Wasteland(int id, List<Cell> cells, string name)
    {
        Id = id;
        Cells = cells ?? new List<Cell>(); // Ensure Cells is not null
        Name = name;

        //assign this wastland as the province for all cells
        foreach (var cell in Cells)
        {
            cell.Province = this;
        }
    }

    }
}