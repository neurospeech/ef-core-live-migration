using NeuroSpeech.EFCoreLiveMigration;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiveMigrationConsole.Models
{
    [Table("Accounts")]
    public class Account
    {

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long AccountID { get; set; }

        [MaxLength(200)]
        [Index]
        public string AccountName { get; set; }
    }
}