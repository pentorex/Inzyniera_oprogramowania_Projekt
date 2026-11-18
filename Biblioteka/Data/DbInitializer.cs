using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Biblioteka.Constants;
using Biblioteka.Models;

namespace Biblioteka.Data;

public static class DbInitializer
{
    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

        string[] roles = { UserRoles.Admin, UserRoles.Librarian, UserRoles.Reader };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(
            userManager,
            email: "admin@biblioteka.pl",
            password: "Admin123!",
            firstName: "G³ówny",
            lastName: "Administrator",
            role: UserRoles.Admin);

        await EnsureUserAsync(
            userManager,
            email: "bibliotekarz@biblioteka.pl",
            password: "Bibliotekarz123!",
            firstName: "Marek",
            lastName: "Kowalski",
            role: UserRoles.Librarian);

        var reader1 = await EnsureUserAsync(
            userManager,
            email: "czytelnik@biblioteka.pl",
            password: "Czytelnik123!",
            firstName: "Jan",
            lastName: "Wiœniewski",
            role: UserRoles.Reader);

        var reader2 = await EnsureUserAsync(
            userManager,
            email: "anna.nowak@biblioteka.pl",
            password: "Czytelnik123!",
            firstName: "Anna",
            lastName: "Nowak",
            role: UserRoles.Reader);

        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Name = "Epopeja" },
                new() { Name = "Fantastyka" },
                new() { Name = "Krymina³" },
                new() { Name = "Literatura piêkna" },
                new() { Name = "Science Fiction" },
                new() { Name = "Historia" }
            };

            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }

        if (!await context.Authors.AnyAsync())
        {
            var authors = new List<Author>
            {
                new() { FirstName = "Adam", LastName = "Mickiewicz" },
                new() { FirstName = "Andrzej", LastName = "Sapkowski" },
                new() { FirstName = "Stanis³aw", LastName = "Lem" },
                new() { FirstName = "Remigiusz", LastName = "Mróz" },
                new() { FirstName = "Olga", LastName = "Tokarczuk" }
            };

            context.Authors.AddRange(authors);
            await context.SaveChangesAsync();
        }

        if (!await context.Books.AnyAsync())
        {
            var categoriesByName = await context.Categories.ToDictionaryAsync(c => c.Name);
            var authors = await context.Authors.ToListAsync();

            var mickiewicz = authors.First(a => a.LastName == "Mickiewicz");
            var sapkowski = authors.First(a => a.LastName == "Sapkowski");
            var lem = authors.First(a => a.LastName == "Lem");
            var mroz = authors.First(a => a.LastName == "Mróz");
            var tokarczuk = authors.First(a => a.LastName == "Tokarczuk");

            var books = new List<Book>
            {
                new()
                {
                    Title = "Pan Tadeusz",
                    ISBN = "9788307033419",
                    PublicationYear = 1834,
                    Publisher = "BookPol",
                    Description = "Polska epopeja narodowa osadzona na Litwie w latach 1811–1812.",
                    CoverImageUrl = string.Empty,
                    AuthorId = mickiewicz.Id,
                    Categories = new List<Category>
                    {
                        categoriesByName["Epopeja"],
                        categoriesByName["Literatura piêkna"],
                        categoriesByName["Historia"]
                    }
                },
                new()
                {
                    Title = "Ostatnie ¿yczenie",
                    ISBN = "9788375780635",
                    PublicationYear = 1993,
                    Publisher = "SuperNOWA",
                    Description = "Zbiór opowiadañ otwieraj¹cy sagê o wiedŸminie Geralcie z Rivii.",
                    CoverImageUrl = string.Empty,
                    AuthorId = sapkowski.Id,
                    Categories = new List<Category>
                    {
                        categoriesByName["Fantastyka"]
                    }
                },
                new()
                {
                    Title = "Solaris",
                    ISBN = "9788308049051",
                    PublicationYear = 1961,
                    Publisher = "Wydawnictwo Literackie",
                    Description = "Powieœæ science fiction o kontakcie ludzkoœci z oceaniczn¹ inteligencj¹.",
                    CoverImageUrl = string.Empty,
                    AuthorId = lem.Id,
                    Categories = new List<Category>
                    {
                        categoriesByName["Science Fiction"],
                        categoriesByName["Literatura piêkna"]
                    }
                },
                new()
                {
                    Title = "Kasacja",
                    ISBN = "9788379762538",
                    PublicationYear = 2015,
                    Publisher = "Czwarta Strona",
                    Description = "Pierwszy tom serii kryminalno-prawniczej z Joann¹ Chy³k¹.",
                    CoverImageUrl = string.Empty,
                    AuthorId = mroz.Id,
                    Categories = new List<Category>
                    {
                        categoriesByName["Krymina³"]
                    }
                },
                new()
                {
                    Title = "Ksiêgi Jakubowe",
                    ISBN = "9788308054147",
                    PublicationYear = 2014,
                    Publisher = "Wydawnictwo Literackie",
                    Description = "Powieœæ historyczna uhonorowana Literack¹ Nagrod¹ Nobla.",
                    CoverImageUrl = string.Empty,
                    AuthorId = tokarczuk.Id,
                    Categories = new List<Category>
                    {
                        categoriesByName["Historia"],
                        categoriesByName["Literatura piêkna"]
                    }
                }
            };

            context.Books.AddRange(books);
            await context.SaveChangesAsync();
        }

        if (!await context.BookCopies.AnyAsync())
        {
            var books = await context.Books.ToListAsync();
            var copies = new List<BookCopy>();

            foreach (var book in books)
            {
                for (int i = 1; i <= 3; i++)
                {
                    copies.Add(new BookCopy
                    {
                        BookId = book.Id,
                        InventoryNumber = $"INV-{book.Id:D3}-{i:D2}",
                        Status = BookStatus.Available,
                        Condition = BookCondition.New,
                        AcquisitionDate = DateTime.UtcNow.AddMonths(-2)
                    });
                }
            }

            context.BookCopies.AddRange(copies);
            await context.SaveChangesAsync();
        }

        if (!await context.Borrowings.AnyAsync() && reader1 != null && reader2 != null)
        {
            var copies = await context.BookCopies.OrderBy(c => c.Id).ToListAsync();

            if (copies.Count >= 3)
            {
                copies[0].Status = BookStatus.Borrowed;
                var activeBorrowing = new Borrowing
                {
                    BookCopyId = copies[0].Id,
                    UserId = reader1.Id,
                    BorrowDate = DateTime.UtcNow.AddDays(-5),
                    DueDate = DateTime.UtcNow.AddDays(25),
                    ReturnDate = null,
                    FineAmount = 0m
                };

                copies[1].Status = BookStatus.Borrowed;
                var overdueBorrowing = new Borrowing
                {
                    BookCopyId = copies[1].Id,
                    UserId = reader1.Id,
                    BorrowDate = DateTime.UtcNow.AddDays(-40),
                    DueDate = DateTime.UtcNow.AddDays(-10),
                    ReturnDate = null,
                    FineAmount = 0m
                };

                var returnedBorrowing = new Borrowing
                {
                    BookCopyId = copies[2].Id,
                    UserId = reader2.Id,
                    BorrowDate = DateTime.UtcNow.AddDays(-50),
                    DueDate = DateTime.UtcNow.AddDays(-20),
                    ReturnDate = DateTime.UtcNow.AddDays(-22),
                    FineAmount = 0m
                };

                context.Borrowings.AddRange(activeBorrowing, overdueBorrowing, returnedBorrowing);
                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string firstName,
        string lastName,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                RegistrationDate = DateTime.UtcNow,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        return user;
    }
}