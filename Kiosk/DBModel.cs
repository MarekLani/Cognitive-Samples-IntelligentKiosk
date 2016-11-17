using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelligentKioskSample
{
    public class KioskDBContext : DbContext
    {
        public DbSet<DBSimilarFace> SimilarFaces { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DBSimilarFace>()
                .HasKey(sf => sf.SimilarFaceId);
            modelBuilder.Entity<DBSimilarFace>()
                .HasIndex(sf => sf.FaceId)
                .HasName("Index_FaceId");
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Filename=IntelligentKiosk.db");
        }
    }

    public class DBSimilarFace
    {
        
        public int SimilarFaceId { get; set; }
        public string FaceId { get; set; }
        public string PersonId { get; set; }
        public DateTime CreatedAt { get; set; }

    }
}
