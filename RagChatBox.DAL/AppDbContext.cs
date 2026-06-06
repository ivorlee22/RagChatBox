using Microsoft.EntityFrameworkCore;
using RagChatBox.DAL.Entities;

namespace RagChatBox.DAL
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public static bool UsePgVector { get; set; } = false;

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Course> Courses { get; set; } = null!;
        public DbSet<CourseEnrollment> CourseEnrollments { get; set; } = null!;
        public DbSet<Document> Documents { get; set; } = null!;
        public DbSet<DocumentChunk> DocumentChunks { get; set; } = null!;
        public DbSet<ChatSession> ChatSessions { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<RetrievalLog> RetrievalLogs { get; set; } = null!;
        public DbSet<Experiment> Experiments { get; set; } = null!;
        public DbSet<Config> Configs { get; set; } = null!;
        public DbSet<TestQuestion> TestQuestions { get; set; } = null!;
        public DbSet<EvaluationResult> EvaluationResults { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Register pgvector PostgreSQL extension if enabled
            if (UsePgVector)
            {
                modelBuilder.HasPostgresExtension("vector");
            }

            // Course configuration
            modelBuilder.Entity<Course>(entity =>
            {
                entity.ToTable("Course");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
                entity.Property(e => e.CourseType).HasMaxLength(50).IsRequired().HasDefaultValue("teacher");
                entity.Property(e => e.IsVisible).HasDefaultValue(true);
                entity.Property(e => e.CoursePassword).HasMaxLength(255);

                entity.HasIndex(e => e.CreatedBy).HasDatabaseName("IX_Course_CreatedBy");

                entity.HasOne(c => c.CreatedByUser)
                    .WithMany(u => u.CreatedCourses)
                    .HasForeignKey(c => c.CreatedBy)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // CourseEnrollment configuration
            modelBuilder.Entity<CourseEnrollment>(entity =>
            {
                entity.ToTable("CourseEnrollment");
                entity.HasKey(e => e.Id);

                // Prevent duplicate enrollment
                entity.HasIndex(e => new { e.CourseId, e.UserId })
                    .IsUnique()
                    .HasDatabaseName("UQ_CourseEnrollment_CourseId_UserId");

                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_CourseEnrollment_UserId");
                entity.HasIndex(e => e.CourseId).HasDatabaseName("IX_CourseEnrollment_CourseId");

                entity.Property(e => e.Status).HasMaxLength(50).IsRequired().HasDefaultValue("active");

                entity.HasOne(e => e.Course)
                    .WithMany(c => c.Enrollments)
                    .HasForeignKey(e => e.CourseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Enrollments)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Username).HasMaxLength(255).IsRequired();
                entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
                entity.Property(e => e.SubscriptionTier).HasMaxLength(50).IsRequired().HasDefaultValue("Free");
            });

            // Document configuration
            modelBuilder.Entity<Document>(entity =>
            {
                entity.ToTable("Document");
                entity.HasKey(e => e.Id);
                
                // Index for CourseId
                entity.HasIndex(e => e.CourseId).HasDatabaseName("IX_Document_CourseId");

                // Unique index for CourseId + FileName to prevent duplicates per course
                entity.HasIndex(e => new { e.CourseId, e.FileName }).IsUnique().HasDatabaseName("UQ_Document_CourseId_FileName");

                entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.FilePath).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.FileType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
                entity.Property(e => e.UploadedBy).HasMaxLength(255);

                entity.HasOne(d => d.Course)
                    .WithMany(p => p.Documents)
                    .HasForeignKey(d => d.CourseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // DocumentChunk configuration
            modelBuilder.Entity<DocumentChunk>(entity =>
            {
                entity.ToTable("DocumentChunk");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.DocumentId).HasDatabaseName("IX_DocumentChunk_DocumentId");

                // Configure Pgvector columns dynamically based on pgvector support
                if (UsePgVector)
                {
                    entity.Property(e => e.EmbeddingE5).HasColumnType("vector(768)");
                    entity.Property(e => e.EmbeddingOpenAI).HasColumnType("vector(1536)");

                    entity.HasIndex(e => e.EmbeddingE5)
                        .HasMethod("hnsw")
                        .HasOperators("vector_cosine_ops")
                        .HasDatabaseName("IX_DocumentChunk_EmbeddingE5");

                    entity.HasIndex(e => e.EmbeddingOpenAI)
                        .HasMethod("hnsw")
                        .HasOperators("vector_cosine_ops")
                        .HasDatabaseName("IX_DocumentChunk_EmbeddingOpenAI");
                }
                else
                {
                    var vectorConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Pgvector.Vector?, float[]?>(
                        v => v == null ? null : v.ToArray(),
                        f => f == null ? null : new Pgvector.Vector(f)
                    );

                    entity.Property(e => e.EmbeddingE5).HasConversion(vectorConverter).HasColumnType("real[]");
                    entity.Property(e => e.EmbeddingOpenAI).HasConversion(vectorConverter).HasColumnType("real[]");
                }

                entity.HasOne(d => d.Document)
                    .WithMany(p => p.Chunks)
                    .HasForeignKey(d => d.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ChatSession configuration
            modelBuilder.Entity<ChatSession>(entity =>
            {
                entity.ToTable("ChatSession");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.UserId).HasDatabaseName("IX_ChatSession_UserId");
                entity.HasIndex(e => e.CourseId).HasDatabaseName("IX_ChatSession_CourseId");

                entity.Property(e => e.Title).HasMaxLength(255).IsRequired();

                entity.HasOne(d => d.User)
                    .WithMany(p => p.ChatSessions)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Course)
                    .WithMany(p => p.ChatSessions)
                    .HasForeignKey(d => d.CourseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Message configuration
            modelBuilder.Entity<Message>(entity =>
            {
                entity.ToTable("Message");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.SessionId).HasDatabaseName("IX_Message_SessionId");
                entity.Property(e => e.Role).HasMaxLength(50).IsRequired();

                entity.HasOne(d => d.Session)
                    .WithMany(p => p.Messages)
                    .HasForeignKey(d => d.SessionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // RetrievalLog configuration
            modelBuilder.Entity<RetrievalLog>(entity =>
            {
                entity.ToTable("RetrievalLog");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.MessageId).HasDatabaseName("IX_RetrievalLog_MessageId");
                entity.HasIndex(e => e.ChunkId).HasDatabaseName("IX_RetrievalLog_ChunkId");

                entity.HasOne(d => d.Message)
                    .WithMany(p => p.RetrievalLogs)
                    .HasForeignKey(d => d.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Chunk)
                    .WithMany(p => p.RetrievalLogs)
                    .HasForeignKey(d => d.ChunkId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Experiment configuration
            modelBuilder.Entity<Experiment>(entity =>
            {
                entity.ToTable("Experiment");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.CreatedBy).HasDatabaseName("IX_Experiment_CreatedBy");
                entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
                entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).IsRequired();

                entity.HasOne(d => d.User)
                    .WithMany(p => p.Experiments)
                    .HasForeignKey(d => d.CreatedBy)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Config configuration
            modelBuilder.Entity<Config>(entity =>
            {
                entity.ToTable("Config");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.ExperimentId).HasDatabaseName("IX_Config_ExperimentId");
                entity.Property(e => e.EmbeddingModel).HasMaxLength(100).IsRequired();
                entity.Property(e => e.LlmModel).HasMaxLength(100).IsRequired();

                entity.HasOne(d => d.Experiment)
                    .WithMany(p => p.Configs)
                    .HasForeignKey(d => d.ExperimentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TestQuestion configuration
            modelBuilder.Entity<TestQuestion>(entity =>
            {
                entity.ToTable("TestQuestion");
                entity.HasKey(e => e.Id);
            });

            // EvaluationResult configuration
            modelBuilder.Entity<EvaluationResult>(entity =>
            {
                entity.ToTable("EvaluationResult");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.ConfigId).HasDatabaseName("IX_EvaluationResult_ConfigId");
                entity.HasIndex(e => e.QuestionId).HasDatabaseName("IX_EvaluationResult_QuestionId");

                entity.HasOne(d => d.Config)
                    .WithMany(p => p.EvaluationResults)
                    .HasForeignKey(d => d.ConfigId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Question)
                    .WithMany(p => p.EvaluationResults)
                    .HasForeignKey(d => d.QuestionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed default users (BCrypt hashed passwords)
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = "$2a$11$xShTE0VmxMqimkItypBYqeO6Kx6D1Jei4Zu4pfPscOeB9v9kDorQG", // BCrypt hash of "admin"
                    Name = "System Admin",
                    Role = "admin",
                    CreatedAt = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc),
                    SubscriptionTier = "Free"
                },
                new User
                {
                    Id = 2,
                    Username = "student",
                    PasswordHash = "$2a$11$mPGtGkISvNXRmcu0tra3QeYSHAdzB4g85xSTlmJKnDqCTUNV7NW/u", // BCrypt hash of "student"
                    Name = "Sample Student",
                    Role = "student",
                    CreatedAt = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc),
                    SubscriptionTier = "Free"
                },
                new User
                {
                    Id = 3,
                    Username = "teacher",
                    PasswordHash = "$2a$11$4lFfFCFk4hkkjAnrT8ZJ0eP3ERrExJJzOJu2H1uMbcRic9SWbo2/6", // BCrypt hash of "teacher"
                    Name = "Sample Teacher",
                    Role = "teacher",
                    CreatedAt = new DateTime(2026, 5, 26, 0, 0, 0, DateTimeKind.Utc),
                    SubscriptionTier = "Free"
                }
            );
        }
    }
}
