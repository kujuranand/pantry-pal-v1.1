using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PantryPal.Core.Models
{
    public sealed class ListSummary
    {
        public int Id { get; set; }                 
        public string Name { get; set; } = "";      // List name
        public DateTime CreatedUtc { get; set; }    // Created Date
        public int ItemCount { get; set; }          // Number of items in the list
        public decimal TotalCost { get; set; }      // total cost, 0 when no items
    }
}