using Microsoft.EntityFrameworkCore;
using System.IO;

namespace FileManagementSystem.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<FileModel> Files { get; set; }
    }
}
