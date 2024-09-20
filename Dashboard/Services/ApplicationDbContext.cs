using Dashboard.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Dashboard.Services
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {

        }

        public DbSet<TrainingExamQuestion> TRAINING_EXAM_QUESTION  { get; set; }

        public DbSet<TrainingTestSchedule> TRAINING_TEST_SCHEDULE { get; set; }

        public DbSet<TableMR> TABLE_MR { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TrainingExamQuestion>()
                        .HasKey(q => q.Q_ID);

            modelBuilder.Entity<TrainingTestSchedule>()
                        .HasKey(q => q.SCHEDULE_ID);

            modelBuilder.Entity<TableMR>()
                        .HasKey(q => q.NIK);
        }

    }
}

