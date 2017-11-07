using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveMigrationConsole.Models
{
    [Table("Products")]
    public class Product
    {

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long ProductID { get; set; }


        public long VendorID { get; set; }

        [ForeignKey(nameof(VendorID))]
        [InverseProperty(nameof(Models.Account.VendorProducts))]
        public Account Vendor { get; set; }

    }
}