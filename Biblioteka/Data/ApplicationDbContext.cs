using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TwojaNazwaProjektu.Models;

namespace Biblioteka.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<BookCopy> BookCopies => Set<BookCopy>();
    public DbSet<Borrowing> Borrowings => Set<Borrowing>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Precyzja dla kwoty kary finansowej
        builder.Entity<Borrowing>()
            .Property(b => b.FineAmount)
            .HasPrecision(18, 2);

        // Zabezpieczenie przed kaskadowym usuwaniem historii transakcji
        builder.Entity<Borrowing>()
            .HasOne(b => b.BookCopy)
            .WithMany(bc => bc.Borrowings)
            .HasForeignKey(b => b.BookCopyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Reservation>()
            .HasOne(r => r.BookCopy)
            .WithMany(bc => bc.Reservations)
            .HasForeignKey(r => r.BookCopyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}