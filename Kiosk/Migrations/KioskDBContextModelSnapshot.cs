using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using IntelligentKioskSample;

namespace IntelligentKioskSample.Migrations
{
    [DbContext(typeof(KioskDBContext))]
    partial class KioskDBContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.0-rtm-22752");

            modelBuilder.Entity("IntelligentKioskSample.DBSimilarFace", b =>
                {
                    b.Property<int>("SimilarFaceId")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("CreatedAt");

                    b.Property<string>("FaceId");

                    b.Property<string>("PersonId");

                    b.HasKey("SimilarFaceId");

                    b.HasIndex("FaceId")
                        .HasName("Index_FaceId");

                    b.ToTable("SimilarFaces");
                });
        }
    }
}
